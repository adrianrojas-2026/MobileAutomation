namespace MobileAutomation.Tests.Utilities;

// What a device offers for the BLE ticket handoff: Android version, BLE hardware, adapter state, app permissions
public record BluetoothReadiness(
    string Udid,
    string AndroidVersion,
    int SdkLevel,
    string Model,
    bool HasBleFeature,
    bool BluetoothOn,
    bool AirplaneModeOn,
    IReadOnlyDictionary<string, AppBluetoothState> Apps);

// Version of an installed app and its Bluetooth runtime permissions (name -> granted)
public record AppBluetoothState(string VersionName, IReadOnlyDictionary<string, bool> BluetoothPermissions);

// Device radios through adb (Appium has no airplane/Bluetooth toggles for Android 12+)
public static class BluetoothHelper
{
    // Android 11+; adb stays connected (USB or emulator), only the device's own radios go off
    public static void SetAirplaneMode(string udid, bool enabled)
    {
        AdbHelper.Run(udid, $"shell cmd connectivity airplane-mode {(enabled ? "enable" : "disable")}");
        Console.WriteLine($"✈️ {udid}: airplane mode {(enabled ? "on" : "off")}.");
    }

    public static bool IsAirplaneModeOn(string udid) => AdbHelper.Read(udid, "shell settings get global airplane_mode_on") == "1";

    public static bool IsBluetoothOn(string udid) => AdbHelper.Read(udid, "shell settings get global bluetooth_on") == "1";

    // An emulator can boot with Bluetooth off (seen on the Pixel_7_Android_13 AVD after a cold boot); the apps then
    // raise Android's "<app> wants to turn on Bluetooth" dialog instead of advertising/scanning. Turn it on up front
    public static void EnsureBluetoothOn(string udid, int timeoutSeconds = 15)
    {
        if (IsBluetoothOn(udid)) return;

        AdbHelper.Run(udid, "shell cmd bluetooth_manager enable");
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (!IsBluetoothOn(udid))
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new InvalidOperationException($"{udid}: Bluetooth is still off {timeoutSeconds} s after 'cmd bluetooth_manager enable'.");
            }
            Thread.Sleep(500);
        }
        Console.WriteLine($"📶 {udid}: Bluetooth was off; turned it on.");
    }

    // Turns the device's Bluetooth adapter on or off, as the user would from quick settings, and waits for it; false
    // when the adapter did not follow
    public static bool SetBluetooth(string udid, bool enabled, int timeoutSeconds = 20)
    {
        AdbHelper.Run(udid, $"shell cmd bluetooth_manager {(enabled ? "enable" : "disable")}");
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (IsBluetoothOn(udid) != enabled)
        {
            if (DateTime.UtcNow >= deadline) return false;
            Thread.Sleep(500);
        }
        Console.WriteLine($"📶 {udid}: Bluetooth {(enabled ? "on" : "off")}.");
        return true;
    }

    // Whether the app has a BLE scan running right now. Android's GATT scanner map counts each app's LE scans as
    // "started / stopped", so more started than stopped means one is running. An app missing from the map never
    // scanned (false). Null when the dump has no scanner map at all (Bluetooth off, or another Android layout).
    // A scan made for the app by Google Play services (Nearby Connections) is counted under GMS, not the app
    public static bool? IsScanning(string udid, string package)
    {
        string dump = AdbHelper.Read(udid, "shell dumpsys bluetooth_manager");
        if (!dump.Contains("GATT Scanner Map", StringComparison.Ordinal)) return null;

        var counts = System.Text.RegularExpressions.Regex.Match(dump,
            $@"^\s*{System.Text.RegularExpressions.Regex.Escape(package)}[^\n]*\n\s*LE scans \(started/stopped\)\s*:\s*(\d+)\s*/\s*(\d+)",
            System.Text.RegularExpressions.RegexOptions.Multiline);
        return counts.Success && int.Parse(counts.Groups[1].Value) > int.Parse(counts.Groups[2].Value);
    }

    public static BluetoothReadiness Probe(string udid, IEnumerable<string> packages)
    {
        var apps = new Dictionary<string, AppBluetoothState>();
        foreach (string package in packages)
        {
            string dump = AdbHelper.Read(udid, $"shell dumpsys package {package}");
            if (!dump.Contains("versionName=", StringComparison.Ordinal)) continue;

            string version = System.Text.RegularExpressions.Regex.Match(dump, @"versionName=(\S+)").Groups[1].Value;
            // Runtime grants are listed as "android.permission.BLUETOOTH_SCAN: granted=false, flags=[...]"
            var permissions = System.Text.RegularExpressions.Regex
                .Matches(dump, @"(android\.permission\.BLUETOOTH\w*): granted=(true|false)")
                .GroupBy(m => m.Groups[1].Value)
                .ToDictionary(g => g.Key, g => g.Any(m => m.Groups[2].Value == "true"));
            apps[package] = new AppBluetoothState(version, permissions);
        }

        return new BluetoothReadiness(
            udid,
            AdbHelper.Read(udid, "shell getprop ro.build.version.release"),
            int.TryParse(AdbHelper.Read(udid, "shell getprop ro.build.version.sdk"), out int sdk) ? sdk : 0,
            AdbHelper.Read(udid, "shell getprop ro.product.model"),
            AdbHelper.Read(udid, "shell pm has-feature android.hardware.bluetooth_le") == "true",
            IsBluetoothOn(udid),
            IsAirplaneModeOn(udid),
            apps);
    }
}
