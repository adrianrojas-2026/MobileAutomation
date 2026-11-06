using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Flows;
using MobileAutomation.Tests.Tests;
using MobileAutomation.Tests.Utilities;
using OpenQA.Selenium.Appium.Android;
using static MobileAutomation.Tests.Pages.NearbyDriverMenuPage;

namespace RecoveryTrac.AutomatedTests
{
    // Part B of docs/testing/test-scenarios-ble-ticket-handoff.md: the collection ticket goes from Flex to the Driver
    // app over Bluetooth instead of a QR scan. Same Flex flow as the QR tests up to Submit; then the monitor sends the
    // ticket from its submitted card and the driver receives it. Ends once the Driver app holds the load, as the
    // disposal side of the BLE handoff is still in development.
    // R1 Nearby flow: the Driver app has no 'Receive via Bluetooth' button and is discoverable whenever it is open, and
    // Flex lists it as an identity card (truck details, or 'Driver App' + Identity Code) that the Driver app does not
    // show. All emulators share one simulated Bluetooth, so the card is told apart by difference: every grid Driver app
    // is stopped, and the card that appears once this node's Driver app starts is its own. That needs the other
    // node's Driver app closed, so the node fixtures run one after the other, each logged in with its own Flex account
    // so neither sees the other's tickets (nor Node1's QR E2E, which uses testuser1).
    [TestFixture("Node2", "testuser2")]
    [TestFixture("Node3", "testuser4")]
    [Category("BLE")]
    [NonParallelizable]
    public class BleTicketHandoffTests : NearbyTestBase
    {
        // Check 2: the load shows on the Driver app within 5 s of picking it (indicative on emulators)
        private const int TargetSeconds = 5;

        private readonly string _node;
        private readonly string _account;

        public BleTicketHandoffTests(string node, string account)
        {
            _node = node;
            _account = account;
        }

        protected override string NodeForCurrentTest => _node;
        protected override string? AccountForCurrentTest => _account;

        // TC-12 (happy path) and TC-13 (payload parity with the QR) for one ticket
        [Test]
        public void BLE_TC12_TC13_CollectionTicket_SentViaBluetooth_IsLoadedOnDriver()
        {
            if (DriverFactory.DriverFlex is not AndroidDriver flexAndroid)
            {
                Assert.Ignore("The Bluetooth handoff needs devices (RunMode=Mobile).");
                return;
            }
            string flexUdid = AdbHelper.GetUdid(flexAndroid)!;
            BluetoothHelper.EnsureBluetoothOn(flexUdid);

            // So Flex needs Nearby devices access the first time it uses Bluetooth, as after a fresh install. Revoking a
            // permission stops the app, so relaunch it before logging in
            NearbyDevicesPermission.Reset(flexUdid, FlexPackage, NearbyDevicesPermission.Flex);
            flexAndroid.ActivateApp(FlexPackage);

            // -------------------------------------------------------------------------
            // STEPS 1-17: log in, fill and submit a 'Load - Collection' ticket (shared with the QR tests)
            // -------------------------------------------------------------------------
            var flow = new CollectionLoadTicketFlow(DriverFactory.DriverFlex, _wait.Value!, _node);
            flow.LoginAndEnterWebView();
            var draft = flow.FillAndSubmitTicket($"Automated test - {nameof(BLE_TC12_TC13_CollectionTicket_SentViaBluetooth_IsLoadedOnDriver)} {_node} {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            // -------------------------------------------------------------------------
            // STEP 18 (BLE): no QR this time - cancel the QR prompt and find the ticket under Submitted
            // -------------------------------------------------------------------------
            flow.TicketForm.DeclineQrGeneration();
            string ticketId = flow.VerifyTicketSubmittedAndSynced(draft);
            string loadNumber = ticketId.TrimStart('#');

            // -------------------------------------------------------------------------
            // STEPS 19-21 (BLE): Flex - Bluetooth icon on the submitted card; Android asks for Nearby devices (Allow),
            // then the Nearby menu searches. Flex checks its access first, which asks Android straight away; its own
            // 'Bluetooth Access Required' alert (Continue) only shows when access is still missing after that, so
            // either can come first
            // -------------------------------------------------------------------------
            PrepareGridDriverApps();
            var menu = OpenNearby(flow, ticketId);
            bool asked = NearbyDevicesPermission.AllowWhenAsked(flexAndroid, timeoutSeconds: 10);
            if (!asked && menu.WaitFor(s => s.Open && s.Lines.Any(l => l.Contains(BluetoothAccessText)), 5) is { } accessAlert)
            {
                Console.WriteLine($"📡 Flex asks for Bluetooth access: {accessAlert}. Tapping {ContinueText}...");
                menu.ClickButton(ContinueText);
                asked = NearbyDevicesPermission.AllowWhenAsked(flexAndroid);
            }
            Assert.Multiple(() =>
            {
                Assert.That(asked, Is.True, $"Android did not ask to allow Flex to find nearby devices the first time Flex used Bluetooth ({menu.Snapshot()}).");
                Assert.That(NearbyDevicesPermission.IsGranted(flexUdid, FlexPackage, NearbyDevicesPermission.Flex), Is.True,
                    "Flex's Nearby devices permission is not granted after tapping Allow.");
            });

            // -------------------------------------------------------------------------
            // STEP 22 (BLE): this node's Driver app opens on Home (discoverable, no button to tap); Flex lists it and
            // the ticket is sent to it
            // -------------------------------------------------------------------------
            var own = FindOwnDriverApp(menu);
            var card = WaitForSelectableCard(menu, own);
            string driverLabel = card.Key;

            own.Home.StartRecordingToasts();
            var sendTimer = System.Diagnostics.Stopwatch.StartNew();
            menu.SelectCard(card.Key);
            // Sent shows 'Sent to <Identity Code>' with Done; a failure shows its reason with Try Again
            var final = menu.WaitFor(s => !s.Open || s.Lines.Any(l => l.StartsWith(SentPrefix, StringComparison.Ordinal)) || s.HasButton(TryAgain), 60);
            double sentSeconds = sendTimer.Elapsed.TotalSeconds;
            string sendResult = final == null
                ? $"no result within 60 s ({menu.Snapshot()})"
                : final.Lines.FirstOrDefault(l => l.StartsWith(SentPrefix, StringComparison.Ordinal))
                  ?? string.Join(" | ", final.Lines.Where(l => l != final.Title && !l.Equals(TryAgain, StringComparison.OrdinalIgnoreCase)
                                                                && !l.Equals("Close", StringComparison.OrdinalIgnoreCase)));
            Console.WriteLine($"📡 Flex: '{sendResult}' after {sentSeconds:N1} s.");
            Assert.That(sendResult, Does.StartWith(SentPrefix), $"Flex could not send ticket {ticketId} to the Driver App '{driverLabel}'.");
            if (card.IdentityCode is { } identityCode)
            {
                Assert.That(sendResult, Is.EqualTo(SentPrefix + identityCode), "Flex reports a different Driver App than the one picked.");
            }

            // -------------------------------------------------------------------------
            // STEP 23 (BLE): the Driver app shows the ticket as its current load, with the ticket's QR
            // -------------------------------------------------------------------------
            string currentLoad = own.Home.WaitForActiveLoad(timeoutSeconds: 30);
            double loadedSeconds = sendTimer.Elapsed.TotalSeconds;
            string? toast = own.Home.ToastMessage(timeoutSeconds: 5);
            string driverPayload = QrScanFeed.Decode(own.Home.GetCurrentLoadQrPng(), DriverFactory.DriverFlex);
            Console.WriteLine($"🚚 Driver app: current load {currentLoad} after {loadedSeconds:N1} s (toast: '{toast}').");

            // Reference: the payload of the same ticket's QR, which the Bluetooth send must carry unchanged
            menu.ClickButton(Done);
            Assert.That(menu.WaitFor(s => !s.Open, 10), Is.Not.Null, $"The Nearby menu did not close after '{Done}' ({menu.Snapshot()}).");
            menu.StopRecording();
            flow.Forms.OpenSubmittedCardQr(ticketId);
            string flexPayload = QrScanFeed.Decode(flow.TicketForm.GetQrCodePng(), DriverFactory.DriverFlex);
            flow.TicketForm.CloseQrModal();

            BaselineRecorder.Save("BLE-TC12", _node, new
            {
                ticketId,
                driverApp = driverLabel,
                driverCard = card.Lines,
                sendResult,
                sentSeconds = Math.Round(sentSeconds, 1),
                loadedSeconds = Math.Round(loadedSeconds, 1),
                currentLoad,
                toast,
                payloadLength = flexPayload.Length,
                flexPayloadSha256 = BaselineRecorder.Sha256(flexPayload),
                driverPayloadSha256 = BaselineRecorder.Sha256(driverPayload),
                payloadMatchesQr = driverPayload == flexPayload
            });

            Assert.Multiple(() =>
            {
                Assert.That(currentLoad, Is.EqualTo(loadNumber),
                    $"The Driver app's current load is not ticket {ticketId} after receiving it via Bluetooth.");
                Assert.That(driverPayload, Is.EqualTo(flexPayload),
                    $"The load received via Bluetooth carries a different payload than ticket {ticketId}'s QR code (TC-13).");
                Assert.That(toast, Is.EqualTo($"Load {loadNumber} received."),
                    "The Driver app did not confirm the received load.");
            });

            if (loadedSeconds > TargetSeconds)
            {
                Assert.Warn($"The load reached the Driver app {loadedSeconds:N1} s after picking it; the target is {TargetSeconds} s (check 2, signed off on the demo phones).");
            }
        }
    }
}
