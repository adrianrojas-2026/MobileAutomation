using System.Diagnostics;
using OpenQA.Selenium.Appium.Android;

namespace MobileAutomation.Tests.Utilities;

// Host-side adb commands for things Appium does not cover (port reverse, seeding the gallery)
public static class AdbHelper
{
    private static string AdbPath
    {
        get
        {
            string sdk = Environment.GetEnvironmentVariable("ANDROID_HOME") ?? Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT") ?? "";
            string adb = Path.Combine(sdk, "platform-tools", "adb.exe");
            return File.Exists(adb) ? adb : "adb";
        }
    }

    // Serial of the device behind an Appium session (e.g. emulator-5556)
    public static string? GetUdid(AndroidDriver driver) =>
        (driver.Capabilities.GetCapability("udid") ?? driver.Capabilities.GetCapability("deviceUDID"))?.ToString();

    // Runs "adb -s <udid> <arguments>" and returns its exit code
    public static int Run(string udid, string arguments, int timeoutMs = 30000)
    {
        using var process = Process.Start(new ProcessStartInfo(AdbPath, $"-s {udid} {arguments}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        process.WaitForExit(timeoutMs);
        return process.HasExited ? process.ExitCode : -1;
    }

    // adb reverse tcp:<port> for each port, so https://localhost:<port> on the device reaches this machine
    public static void ReversePorts(string? udid, IReadOnlyCollection<int> ports)
    {
        if (string.IsNullOrWhiteSpace(udid) || ports.Count == 0) return;

        int failed = ports.Count(port =>
        {
            try
            {
                return Run(udid, $"reverse tcp:{port} tcp:{port}", 10000) != 0;
            }
            catch (Exception)
            {
                return true;
            }
        });
        Console.WriteLine(failed == 0
            ? $"📡 Forwarded {ports.Count} Aspire service ports to {udid}."
            : $"⚠️ Could not forward {failed} of {ports.Count} Aspire service ports to {udid}; the app may not reach the local backend.");
    }

    // A notification shade left open (e.g. by a click-drag on the emulator window) covers the app, and every
    // locator then times out. Collapsing it when it is already closed does nothing.
    public static void CollapseNotificationShade(string? udid)
    {
        if (string.IsNullOrWhiteSpace(udid)) return;
        try
        {
            Run(udid, "shell cmd statusbar collapse", 10000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Could not collapse the notification shade on {udid}: {ex.Message}");
        }
    }

    // Copies an image into the device's Pictures folder as the newest photo, so the photo picker lists it first
    public static string PushToGallery(string udid, string imagePath)
    {
        string devicePath = $"/sdcard/Pictures/RecoveryTracTest_{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(imagePath)}";
        if (Run(udid, $"push \"{Path.GetFullPath(imagePath)}\" {devicePath}") != 0)
        {
            throw new InvalidOperationException($"adb could not push '{imagePath}' to {udid}.");
        }

        // adb push keeps the source file's old timestamp; the picker sorts by date, so make it the latest
        Run(udid, $"shell touch {devicePath}");
        // Index it in MediaStore so the picker can see it (the broadcast covers older Android versions)
        Run(udid, $"shell am broadcast -a android.intent.action.MEDIA_SCANNER_SCAN_FILE -d file://{devicePath}");
        Run(udid, "shell content call --method scan_volume --uri content://media --arg external_primary");
        return devicePath;
    }
}
