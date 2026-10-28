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

    // Mock location with a given accuracy through the Appium Settings app's location service (what newer
    // UiAutomator2 drivers do for "mobile: setGeolocation"). Returns whether the device now reports a mocked fix
    public static bool MockLocation(string udid, double latitude, double longitude, double accuracyMeters)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        Run(udid, "shell appops set io.appium.settings android:mock_location allow");
        // The service registers its test providers when it starts, so restart it after mock locations were allowed
        Run(udid, "shell am stopservice -n io.appium.settings/.LocationService");
        Run(udid, "shell am force-stop io.appium.settings");
        int exitCode = Run(udid, "shell am start-foreground-service --user 0 -n io.appium.settings/.LocationService " +
                                 $"--es longitude {longitude.ToString(culture)} --es latitude {latitude.ToString(culture)} " +
                                 $"--es altitude 0 --es accuracy {accuracyMeters.ToString(culture)}");
        if (exitCode != 0) return false;

        Thread.Sleep(2000); // The service publishes its first fix shortly after starting
        return Read(udid, "shell dumpsys location").Contains(" mock]", StringComparison.Ordinal);
    }

    // Runs "adb -s <udid> <arguments>" and returns its trimmed standard output
    public static string Read(string udid, string arguments, int timeoutMs = 30000)
    {
        using var process = Process.Start(new ProcessStartInfo(AdbPath, $"-s {udid} {arguments}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        // Read before waiting: a full output pipe would otherwise block adb from exiting
        var output = process.StandardOutput.ReadToEndAsync();
        process.WaitForExit(timeoutMs);
        return output.Wait(timeoutMs) ? output.Result.Trim() : string.Empty;
    }

    // A frozen emulator stays "device" in adb devices but never answers a shell command; Appium then waits out its adb
    // timeouts on every command. This asks for an answer within a few seconds instead
    public static bool IsResponsive(string udid, int timeoutSeconds = 10)
    {
        using var process = Process.Start(new ProcessStartInfo(AdbPath, $"-s {udid} shell echo ok")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        var output = process.StandardOutput.ReadToEndAsync();
        if (!process.WaitForExit(timeoutSeconds * 1000))
        {
            process.Kill(entireProcessTree: true);
            return false;
        }
        return output.Wait(1000) && output.Result.Trim() == "ok";
    }

    // Serials of the devices adb currently sees as online
    public static IReadOnlyList<string> OnlineDevices()
    {
        using var process = Process.Start(new ProcessStartInfo(AdbPath, "devices")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(10000);
        return output.Split('\n', StringSplitOptions.TrimEntries)
            .Select(line => line.Split('\t'))
            .Where(parts => parts.Length == 2 && parts[1] == "device")
            .Select(parts => parts[0])
            .ToList();
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
