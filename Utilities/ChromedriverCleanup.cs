using System.Diagnostics;
using System.Text.RegularExpressions;

namespace MobileAutomation.Tests.Utilities;

// Appium starts each WebView's chromedriver on the node's fixed chromedriverPort and first kills any old one on that
// port, but it does that with wmic, which newer Windows 11 builds no longer ship. A chromedriver orphaned by a broken
// run then keeps the port; Appium talks to it instead of its new one and every later session fails with
// "Device emulator-XXXX is already in use". This does the kill Appium can't, for a local Appium server only.
public static class ChromedriverCleanup
{
    // Kills the chromedriver listening on the port, if any; returns whether one was killed
    public static bool KillOnPort(string? appiumServerUrl, int port)
    {
        if (!IsLocal(appiumServerUrl)) return false;

        int? pid = ListeningProcessId(port);
        if (pid == null) return false;

        try
        {
            using var process = Process.GetProcessById(pid.Value);
            // Only ever a chromedriver: the port could be reused by something unrelated
            if (!process.ProcessName.StartsWith("chromedriver", StringComparison.OrdinalIgnoreCase)) return false;

            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
            Console.WriteLine($"🧹 Killed a stale chromedriver (pid {pid}) left on port {port} by an earlier session.");
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Already gone, or not ours to kill
            return false;
        }
    }

    private static bool IsLocal(string? serverUrl) =>
        Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri) && (uri.IsLoopback || uri.Host == Environment.MachineName);

    // netstat is on every Windows build; .NET has no API that maps a listening port to its process
    private static int? ListeningProcessId(int port)
    {
        using var netstat = Process.Start(new ProcessStartInfo("netstat", "-ano -p TCP")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        string output = netstat.StandardOutput.ReadToEnd();
        netstat.WaitForExit(10000);

        // e.g. "  TCP    127.0.0.1:8405    0.0.0.0:0    LISTENING    54644"
        var match = Regex.Match(output, $@"^\s*TCP\s+\S+:{port}\s+\S+\s+LISTENING\s+(\d+)\s*$", RegexOptions.Multiline);
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }
}
