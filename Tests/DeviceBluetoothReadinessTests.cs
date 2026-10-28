using Microsoft.Extensions.Configuration;
using MobileAutomation.Tests.Reporting;
using MobileAutomation.Tests.Utilities;

namespace RecoveryTrac.AutomatedTests
{
    // QR-B10 (BLE Day-1 spike): which grid devices / demo phones can take part in the BLE ticket handoff. Needs adb
    // only (no Appium session), so it can run before anything else.
    [TestFixture]
    [Category("BLE-Readiness")]
    public class DeviceBluetoothReadinessTests
    {
        private const string FlexPackage = "com.tetratech.recoverytracflex";
        private const string DriverPackage = "com.tetratech.recoverytracdriver";

        // Android 12 (API 31): the POC's minimum, for the BLUETOOTH_SCAN / ADVERTISE / CONNECT permissions
        private const int MinSdkLevel = 31;

        [SetUp]
        public void StartReportEntry() => QrBaselineReport.StartTest();

        [TearDown]
        public void RecordReportEntry() => QrBaselineReport.RecordTest("all devices");

        [Test]
        public void QR_B10_OnlineDevices_BluetoothReadiness_IsRecorded()
        {
            var devices = AdbHelper.OnlineDevices();
            if (devices.Count == 0) Assert.Ignore("No device is online in adb; start the emulators or connect the demo phones.");

            // Grid_<node>_<app> names for the emulators in appsettings.json; demo phones show up by serial only
            var gridNames = TestConfiguration.Configuration.GetSection("MobileConfigurations").GetChildren()
                .Where(c => c["Udid"] != null)
                .GroupBy(c => c["Udid"]!)
                .ToDictionary(g => g.Key, g => string.Join("/", g.Select(c => c.Key)));

            foreach (string frozen in devices.Where(udid => !AdbHelper.IsResponsive(udid)).ToList())
            {
                Assert.Warn($"{frozen} is online in adb but not responding; restart it to include it.");
                devices = devices.Where(udid => udid != frozen).ToList();
            }

            var probes = devices.Select(udid => BluetoothHelper.Probe(udid, [FlexPackage, DriverPackage])).ToList();
            foreach (var p in probes)
            {
                Console.WriteLine($"📶 {p.Udid} ({gridNames.GetValueOrDefault(p.Udid, "not in grid")}): Android {p.AndroidVersion} (API {p.SdkLevel}), " +
                                  $"BLE={p.HasBleFeature}, BT on={p.BluetoothOn}, airplane={p.AirplaneModeOn}, " +
                                  string.Join(", ", p.Apps.Select(a => $"{a.Key} {a.Value.VersionName} [{string.Join(" ", a.Value.BluetoothPermissions.Select(x => $"{x.Key.Split('.').Last()}={x.Value}"))}]")));
            }

            BaselineRecorder.Save("QR-B10", "all", probes.Select(p => new
            {
                grid = gridNames.GetValueOrDefault(p.Udid, "not in grid"),
                probe = p
            }).ToList());

            foreach (var p in probes.Where(p => !p.HasBleFeature))
            {
                Assert.Warn($"{p.Udid} reports no Bluetooth LE hardware; it can only run the QR flows.");
            }
            // Each app is checked on the device of its grid role: Flex scans for the Driver, the Driver advertises to Flex.
            // Demo phones (not in the grid) are checked for both apps they have installed
            foreach (var p in probes)
            {
                string grid = gridNames.GetValueOrDefault(p.Udid, string.Empty);
                bool inGrid = grid.Length > 0;
                if (!inGrid || grid.Contains("_Flex", StringComparison.Ordinal))
                    WarnIfMissingBlePermissions(p, FlexPackage, "Flex", "BLUETOOTH_SCAN", "BLUETOOTH_CONNECT");
                if (!inGrid || grid.Contains("_Driver", StringComparison.Ordinal))
                    WarnIfMissingBlePermissions(p, DriverPackage, "Driver", "BLUETOOTH_ADVERTISE", "BLUETOOTH_CONNECT");
            }

            Assert.That(probes.Where(p => p.SdkLevel < MinSdkLevel).Select(p => $"{p.Udid} (API {p.SdkLevel})"), Is.Empty,
                $"These devices are below Android 12 (API {MinSdkLevel}), the BLE handoff minimum.");
        }

        // A missing declaration means a pre-BLE build. Android only grants these at the app's first Bluetooth use, so
        // a declared but ungranted permission is a note, not a warning
        private static void WarnIfMissingBlePermissions(BluetoothReadiness probe, string package, string app, params string[] permissions)
        {
            if (!probe.Apps.TryGetValue(package, out var state))
            {
                // Only grid devices must have their role's app; a demo phone may carry just one of the two
                return;
            }

            var missing = permissions.Where(p => !state.BluetoothPermissions.ContainsKey($"android.permission.{p}")).ToList();
            if (missing.Count > 0)
            {
                Assert.Warn($"{probe.Udid}: {app} app {state.VersionName} does not request {string.Join(", ", missing)} yet (pre-BLE build).");
                return;
            }

            var ungranted = permissions.Where(p => !state.BluetoothPermissions[$"android.permission.{p}"]).ToList();
            if (ungranted.Count > 0)
            {
                QrBaselineReport.AddNote($"{probe.Udid}: {app} app {state.VersionName} declares {string.Join(", ", permissions)}; " +
                                         $"{string.Join(", ", ungranted)} not granted yet (Android asks at the first Bluetooth use).");
            }
        }
    }
}
