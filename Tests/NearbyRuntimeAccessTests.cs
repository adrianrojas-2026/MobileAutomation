using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Pages;
using MobileAutomation.Tests.Tests;
using MobileAutomation.Tests.Utilities;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using static MobileAutomation.Tests.Pages.NearbyDriverMenuPage;

namespace RecoveryTrac.AutomatedTests
{
    // R1-C2 FEAT-NDT-6 (Flex Nearby Runtime Access), #10082. Before it searches, Flex checks its Bluetooth access; when
    // it is not ready Flex shows the shared Flex Bluetooth Access flow instead, and starts the search the user asked for
    // only once access is ready. The location conditions (D5, open) are not covered: Android 12+ scans for nearby
    // devices without location.
    // The POC opens its picker and searches straight away, so these fail until R1; each failure names its requirement ID.
    [TestFixture]
    [Category("BLE-R1-C2")]
    [NonParallelizable]
    public class NearbyRuntimeAccessTests : NearbyTestBase
    {
        // The shared Flex Bluetooth Access flow, known by the explanation NDT-6-P03 gives it for a Nearby transfer
        private static bool ShowsAccessFlow(MenuSnapshot s) => s.Open && s.Lines.Any(l => l.Contains(BluetoothAccessText));

        // The access flow's primary action: its enabled button other than Cancel
        private static string? PrimaryAction(MenuSnapshot s) =>
            s.Buttons.FirstOrDefault(b => !b.Disabled && !b.Text.Equals(Cancel, StringComparison.OrdinalIgnoreCase))?.Text;

        // 10082-NDT-6-P01, P02, P03, P08, N01, P09: Bluetooth off when the Flex user taps the Nearby action, then turned on
        [Test]
        public void NDT6_BluetoothOff_ShowsAccessFlow_ThenSearchesOnceOn()
        {
            var (flow, ticketId, flexUdid) = StartFlexOnSubmittedTicket("Nearby runtime access");
            try
            {
                Assert.That(BluetoothHelper.SetBluetooth(flexUdid, false), Is.True, "Setup: Bluetooth did not turn off on the Flex device.");
                var menu = OpenNearby(flow, ticketId);
                var access = menu.WaitFor(ShowsAccessFlow, 10);
                var shown = access ?? menu.Snapshot();
                var whileOff = menu.Recording();
                Console.WriteLine($"📡 With Bluetooth off Flex shows: {shown}.");

                // The user turns Bluetooth on (quick settings) and continues from the access flow when it is still up
                Assert.That(BluetoothHelper.SetBluetooth(flexUdid, true), Is.True, "Bluetooth did not turn back on on the Flex device.");
                string? continuedWith = null;
                if (access != null && menu.WaitFor(s => s.Searching != null, 3) == null && menu.Snapshot() is { } now && ShowsAccessFlow(now))
                {
                    continuedWith = PrimaryAction(now);
                    if (continuedWith != null) menu.ClickButton(continuedWith);
                }
                var searching = menu.WaitFor(s => s.Searching != null, 10);
                Console.WriteLine($"📡 Bluetooth on{(continuedWith != null ? $", '{continuedWith}' tapped" : string.Empty)}: {searching ?? menu.Snapshot()}.");

                BaselineRecorder.Save("NDT-6-P02", Node, new
                {
                    accessFlow = shown.ToString(), searchedWhileOff = whileOff.Log.Any(s => s.Searching != null), continuedWith,
                    searchedOnceOn = searching != null
                });

                Assert.Multiple(() =>
                {
                    Assert.That(access, Is.Not.Null,
                        $"10082-NDT-6-P02 / P03 not met: with Bluetooth off Flex did not show the Bluetooth Access flow with '{BluetoothAccessText}' ({shown}).");
                    Assert.That(whileOff.Log.Any(s => s.Searching != null), Is.False,
                        $"10082-NDT-6-P01 / N01 not met: Flex started searching with Bluetooth off ({whileOff.Log.First(s => s.Searching != null)}).");
                    if (access != null)
                    {
                        Assert.That(access.HasButton(Cancel), Is.True,
                            $"10082-NDT-6-P08 not met: the access flow does not offer '{Cancel}' as its secondary action " +
                            $"(buttons: [{string.Join(", ", access.Buttons.Select(b => b.Text))}]). If one of them is an alternative action the workflow supplies, P04 applies instead.");
                    }
                    // Only a search that follows the access flow counts: one already running with Bluetooth off is N01's failure
                    Assert.That(access != null && searching != null, Is.True,
                        access == null
                            ? "10082-NDT-6-P09 not met: Flex never waited for Bluetooth access (no access flow), so it did not start the search once access was ready."
                            : "10082-NDT-6-P09 not met: once Bluetooth was on, Flex did not start the Nearby search without the Nearby action being tapped again.");
                });
            }
            finally
            {
                BluetoothHelper.SetBluetooth(flexUdid, true);
            }
        }

        // 10082-NDT-6-E02 (and N01): Bluetooth off, the Flex user cancels the access flow
        [Test]
        public void NDT6_BluetoothOff_CancelAccessFlow_ReturnsWithoutSearching()
        {
            var (flow, ticketId, flexUdid) = StartFlexOnSubmittedTicket("Nearby runtime access");
            var ticketsBefore = flow.Forms.GetSubmittedTicketIds();
            try
            {
                Assert.That(BluetoothHelper.SetBluetooth(flexUdid, false), Is.True, "Setup: Bluetooth did not turn off on the Flex device.");
                var menu = OpenNearby(flow, ticketId);
                var access = menu.WaitFor(ShowsAccessFlow, 10);
                Assert.That(access, Is.Not.Null,
                    $"10082-NDT-6-P02 not met (needed for E02): with Bluetooth off Flex did not show the Bluetooth Access flow ({menu.Snapshot()}).");

                bool canceled = menu.ClickButton(Cancel);
                bool closed = canceled && menu.WaitFor(s => !s.Open, 5) != null;
                // Turning Bluetooth on afterwards must not start the canceled search either
                BluetoothHelper.SetBluetooth(flexUdid, true);
                Thread.Sleep(5000);
                bool? scanning = IsFlexScanning(flexUdid);
                var recording = menu.StopRecording();
                IReadOnlyList<string>? ticketsAfter = null;
                if (closed)
                {
                    try
                    {
                        ticketsAfter = flow.Forms.GetSubmittedTicketIds();
                    }
                    catch (WebDriverTimeoutException ex)
                    {
                        Console.WriteLine($"⚠️ Flex is no longer on the Submitted list after Cancel: {ex.Message}");
                    }
                }
                Console.WriteLine($"📡 Cancel {(canceled ? "tapped" : "not offered")}; access flow closed: {closed}; Flex scanning afterwards: {scanning}.");

                Assert.Multiple(() =>
                {
                    Assert.That(canceled, Is.True,
                        $"10082-NDT-6-P08 / E02 not met: the access flow offers no '{Cancel}' (buttons: [{string.Join(", ", access!.Buttons.Select(b => b.Text))}]).");
                    Assert.That(closed, Is.True, "10082-NDT-6-E02 not met: the access flow did not close after Cancel.");
                    Assert.That(recording.Log.Any(s => s.Searching != null), Is.False,
                        "10082-NDT-6-E02 / N01 not met: Flex started a Nearby search after the access flow was canceled.");
                    Assert.That(scanning, Is.Not.True, "10082-NDT-6-E02 not met: Flex is scanning after the access flow was canceled.");
                    if (closed)
                    {
                        Assert.That(ticketsAfter, Is.EqualTo(ticketsBefore),
                            "10082-NDT-6-E02 not met: after Cancel Flex is not back on the unchanged Submitted list.");
                    }
                });
            }
            finally
            {
                BluetoothHelper.SetBluetooth(flexUdid, true);
            }
        }

        // 10082-NDT-6-P02, E03, N01: Flex without Nearby devices access, and the user refuses it when Android asks
        [Test]
        public void NDT6_NearbyAccessDenied_DoesNotSearch()
        {
            var (flow, ticketId, flexUdid) = StartFlexOnSubmittedTicket("Nearby runtime access", grantNearby: false);
            var flexAndroid = (AndroidDriver)DriverFactory.DriverFlex;
            try
            {
                var menu = OpenNearby(flow, ticketId);
                // R1 shows the access flow first and asks Android from its primary action; the POC asks right away
                bool denied = NearbyDevicesPermission.DenyWhenAsked(flexAndroid, timeoutSeconds: 8);
                MenuSnapshot? access = null;
                if (!denied)
                {
                    access = menu.WaitFor(ShowsAccessFlow, 2);
                    if (access != null && PrimaryAction(access) is { } primary)
                    {
                        menu.ClickButton(primary);
                        denied = NearbyDevicesPermission.DenyWhenAsked(flexAndroid, timeoutSeconds: 10);
                    }
                }
                Assume.That(denied, Is.True, $"Android never asked for Nearby devices access, so E03 cannot be set up ({menu.Snapshot()}).");

                Thread.Sleep(3000);
                bool? scanning = IsFlexScanning(flexUdid);
                var recording = menu.StopRecording();
                bool accessShown = recording.Log.Any(ShowsAccessFlow);
                Console.WriteLine($"📡 Nearby devices access refused; access flow shown: {accessShown}; Flex scanning: {scanning}; now: {recording.Log.LastOrDefault()}.");

                Assert.Multiple(() =>
                {
                    Assert.That(accessShown, Is.True,
                        "10082-NDT-6-P02 not met: without Nearby devices access Flex did not show the shared Bluetooth Access flow before discovery.");
                    Assert.That(recording.Log.Any(s => s.Searching != null), Is.False,
                        $"10082-NDT-6-N01 / E03 not met: Flex started searching without Nearby devices access ({recording.Log.First(s => s.Searching != null)}).");
                    Assert.That(scanning, Is.Not.True, "10082-NDT-6-E03 not met: Flex is scanning after Nearby devices access was refused.");
                });
            }
            finally
            {
                NearbyDevicesPermission.Grant(flexUdid, FlexPackage, NearbyDevicesPermission.Flex);
            }
        }
    }
}
