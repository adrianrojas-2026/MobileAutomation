using System.Diagnostics;
using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Flows;
using MobileAutomation.Tests.Pages;
using MobileAutomation.Tests.Tests;
using MobileAutomation.Tests.Utilities;
using OpenQA.Selenium.Appium.Android;

namespace RecoveryTrac.AutomatedTests
{
    // Part B of docs/testing/test-scenarios-ble-ticket-handoff.md: the collection ticket goes from Flex to the Driver
    // app over Bluetooth instead of a QR scan. Same Flex flow as the QR tests up to Submit; then the monitor sends the
    // ticket from its submitted card and the driver receives it. Ends once the Driver app holds the load, as the
    // disposal side of the BLE handoff is still in development.
    // The node pairs run in parallel, each logged in with its own Flex account so neither sees the other's tickets
    // (nor Node1's QR E2E, which uses testuser1).
    // All emulators share one simulated Bluetooth, so each Flex also lists the other node's Driver app; it picks its
    // own by the truck code that Driver app shows.
    [TestFixture("Node2", "testuser2")]
    [TestFixture("Node3", "testuser4")]
    [Category("BLE")]
    [Parallelizable(ParallelScope.Self)]
    public class BleTicketHandoffTests : DualAppTestBase
    {
        private const string FlexPackage = "com.tetratech.recoverytracflex";

        // Check 2: the load shows on the Driver app within 5 s of picking the truck (indicative on emulators)
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

            // So Android asks for Nearby devices when Flex first uses Bluetooth, as after a fresh install. Revoking a
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
            // STEP 19 (BLE): Driver app - Receive via Bluetooth and read the truck code it advertises
            // -------------------------------------------------------------------------
            var (driverApp, driverHome) = DriverAppFlow.StartOnHome();
            driverHome.CancelStaleLoadIfPresent();
            driverHome.ReceiveViaBluetooth();
            if (driverApp is AndroidDriver driverAndroid)
            {
                // Only on a device that never granted it; the Driver advertises for 2 minutes from here
                NearbyDevicesPermission.AllowWhenAsked(driverAndroid, timeoutSeconds: 5);
            }
            string truckCode = driverHome.WaitForTruckCode();
            Console.WriteLine($"🚚 Driver app is waiting for the monitor as {truckCode}.");

            // -------------------------------------------------------------------------
            // STEPS 20-21 (BLE): Flex - Bluetooth icon on the submitted card, allow Nearby devices
            // -------------------------------------------------------------------------
            flow.Forms.OpenSubmittedCardBluetooth(ticketId);
            bool asked = NearbyDevicesPermission.AllowWhenAsked(flexAndroid);
            Assert.Multiple(() =>
            {
                Assert.That(asked, Is.True, "Android did not ask to allow Flex to find nearby devices the first time Flex used Bluetooth.");
                Assert.That(NearbyDevicesPermission.IsGranted(flexUdid, FlexPackage, NearbyDevicesPermission.Flex), Is.True,
                    "Flex's Nearby devices permission is not granted after tapping Allow.");
            });

            var picker = new BleTruckPickerPage(DriverFactory.DriverFlex);
            Assert.That(picker.IsOpen(), Is.True, $"The 'Send via Bluetooth' picker did not open for ticket {ticketId}.");

            // -------------------------------------------------------------------------
            // STEP 22 (BLE): Flex lists the Driver app by its truck code; send the ticket to it
            // -------------------------------------------------------------------------
            picker.WaitForTruck(truckCode);
            driverHome.StartRecordingToasts();
            var sendTimer = Stopwatch.StartNew();
            picker.SelectTruck(truckCode);
            string sendResult = picker.WaitForSendResult();
            double sentSeconds = sendTimer.Elapsed.TotalSeconds;
            Console.WriteLine($"📡 Flex: '{sendResult}' after {sentSeconds:N1} s.");
            Assert.That(sendResult, Is.EqualTo($"Sent to {truckCode}"), $"Flex could not send ticket {ticketId} to {truckCode}.");

            // -------------------------------------------------------------------------
            // STEP 23 (BLE): the Driver app shows the ticket as its current load, with the ticket's QR
            // -------------------------------------------------------------------------
            string currentLoad = driverHome.WaitForActiveLoad(timeoutSeconds: 30);
            double loadedSeconds = sendTimer.Elapsed.TotalSeconds;
            string? toast = driverHome.ToastMessage(timeoutSeconds: 5);
            string driverPayload = QrScanFeed.Decode(driverHome.GetCurrentLoadQrPng(), DriverFactory.DriverFlex);
            Console.WriteLine($"🚚 Driver app: current load {currentLoad} after {loadedSeconds:N1} s (toast: '{toast}').");

            // Reference: the payload of the same ticket's QR, which the Bluetooth send must carry unchanged
            picker.Close();
            flow.Forms.OpenSubmittedCardQr(ticketId);
            string flexPayload = QrScanFeed.Decode(flow.TicketForm.GetQrCodePng(), DriverFactory.DriverFlex);
            flow.TicketForm.CloseQrModal();

            BaselineRecorder.Save("BLE-TC12", _node, new
            {
                ticketId,
                truckCode,
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
                Assert.Warn($"The load reached the Driver app {loadedSeconds:N1} s after picking the truck; the target is {TargetSeconds} s (check 2, signed off on the demo phones).");
            }
        }
    }
}
