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
            AdbHelper.Read(udid, "shell settings get global bluetooth_on") == "1",
            IsAirplaneModeOn(udid),
            apps);
    }
}
