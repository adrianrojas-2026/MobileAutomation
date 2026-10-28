using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Flows;
using MobileAutomation.Tests.Pages.Driver;
using MobileAutomation.Tests.Reporting;
using MobileAutomation.Tests.Tests;
using MobileAutomation.Tests.Utilities;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;

namespace RecoveryTrac.AutomatedTests
{
    // Part A of docs/testing/test-scenarios-ble-ticket-handoff.md: records how the QR handoff behaves today, so the
    // BLE handoff tests have an expected result to compare with ("behaves as after a QR scan"). Each test saves what
    // it observed under Baselines/ (BaselineRecorder) and only asserts what must hold for QR as well.
    // Pinned to one node pair and non-parallelizable, so it never shares emulators with LoadTicketCollectionTests.
    [TestFixture("Node3")]
    [Category("QR")]
    [Category("QR-Baseline")]
    [NonParallelizable]
    public class QrBaselineTests : DualAppTestBase
    {
        // Largest payload the BLE transfer logic must carry (FR-4)
        private const int MaxBlePayloadChars = 4200;

        private readonly string _node;

        public QrBaselineTests(string node)
        {
            _node = node;
        }

        protected override string NodeForCurrentTest => _node;

        private record SubmittedTicket(CollectionLoadTicketFlow Flow, CollectionTicketDraft Draft, string TicketId, byte[] QrPng, string QrText);

        private record DriverSnapshot(string CurrentLoad, string LoadQrText, string LoadQrSha256);

        // QR-B01: what the collection QR carries (and therefore what BLE must send unchanged)
        [Test]
        public void QR_B01_B08_CollectionQrPayload_IsFingerprinted_And_ReopenedQrMatches()
        {
            var ticket = SubmitTicket(nameof(QR_B01_B08_CollectionQrPayload_IsFingerprinted_And_ReopenedQrMatches));

            Assert.That(ticket.QrText, Is.Not.Empty, "The collection QR code decoded to an empty payload.");
            Assert.That(ticket.QrText, Has.Length.LessThanOrEqualTo(MaxBlePayloadChars),
                $"A real collection ticket's QR payload ({ticket.QrText.Length} characters) is larger than the {MaxBlePayloadChars} characters the BLE transfer must support.");

            // QR-B08: after the QR modal closes the form is not locked; the QR can be reopened from the submitted card
            // (the QR fallback after a failed Bluetooth send) and carries the same payload
            byte[] reopenedPng;
            try
            {
                ticket.Flow.Forms.OpenSubmittedCardQr(ticket.TicketId);
                reopenedPng = ticket.Flow.TicketForm.GetQrCodePng();
                ticket.Flow.TicketForm.CloseQrModal();
            }
            catch (WebDriverTimeoutException ex)
            {
                Assert.Fail($"The QR code of submitted ticket {ticket.TicketId} could not be reopened from its card: {ex.Message}");
                return;
            }
            string reopenedText = QrScanFeed.Decode(reopenedPng, DriverFactory.DriverFlex);

            BaselineRecorder.Save("QR-B01", _node, new
            {
                ticketId = ticket.TicketId,
                payloadLength = ticket.QrText.Length,
                payloadSha256 = BaselineRecorder.Sha256(ticket.QrText),
                payload = ticket.QrText,
                reopenedPayloadSha256 = BaselineRecorder.Sha256(reopenedText),
                reopenedMatchesGenerated = reopenedText == ticket.QrText
            });

            Assert.That(reopenedText, Is.EqualTo(ticket.QrText),
                $"The QR reopened from submitted ticket {ticket.TicketId} carries a different payload than the one generated on submit.");
        }

        // QR-B02 / QR-B03: what the Driver app shows and stores after a QR scan (FR-6 reference)
        [Test]
        public void QR_B02_B03_DriverLoadAfterQrScan_IsRecorded_And_SurvivesRestart()
        {
            var ticket = SubmitTicket(nameof(QR_B02_B03_DriverLoadAfterQrScan_IsRecorded_And_SurvivesRestart));
            var (driverApp, driverHome) = DriverAppFlow.StartOnHome();

            QrScanFeed.Stage(driverApp, ticket.QrPng, rasterizer: DriverFactory.DriverFlex, DriverFactory.DriverCameraFeedPath);
            driverHome.ScanQrCode();
            driverHome.WaitForActiveLoad();
            var afterScan = Snapshot(driverHome);

            var restartedHome = DriverAppFlow.Restart(driverApp);
            var afterRestart = Snapshot(restartedHome);

            BaselineRecorder.Save("QR-B02", _node, new
            {
                ticketId = ticket.TicketId,
                ticketPayloadSha256 = BaselineRecorder.Sha256(ticket.QrText),
                // G6: whether the Driver's current load number is the Flex ticket number
                currentLoadIsTicketId = afterScan.CurrentLoad == ticket.TicketId.TrimStart('#'),
                duplicateLoadAlertShown = driverHome.DuplicateLoadAlertShown,
                afterScan,
                afterRestart
            });

            Assert.Multiple(() =>
            {
                Assert.That(afterRestart.CurrentLoad, Is.EqualTo(afterScan.CurrentLoad),
                    "The Driver app's current load changed after a restart, so the scanned load was not stored.");
                Assert.That(afterRestart.LoadQrText, Is.EqualTo(afterScan.LoadQrText),
                    "The Driver app's current load QR changed after a restart.");
            });
        }

        // QR-B04: a second collection ticket while a load is active - the expected result for demo check 4
        [Test]
        public void QR_B04_SecondTicketWhileLoadActive_BehaviorIsRecorded()
        {
            var first = SubmitTicket(nameof(QR_B04_SecondTicketWhileLoadActive_BehaviorIsRecorded) + " #1");
            var second = SubmitTicket(nameof(QR_B04_SecondTicketWhileLoadActive_BehaviorIsRecorded) + " #2", first.Flow);
            var (driverApp, driverHome) = DriverAppFlow.StartOnHome();

            QrScanFeed.Stage(driverApp, first.QrPng, rasterizer: DriverFactory.DriverFlex, DriverFactory.DriverCameraFeedPath);
            driverHome.ScanQrCode();
            string firstLoad = driverHome.WaitForActiveLoad();
            var homeActions = driverHome.VisibleHomeActions();

            // A driver holding a load scans the next ticket with whatever scan button Home offers
            QrScanFeed.Stage(driverApp, second.QrPng, rasterizer: DriverFactory.DriverFlex, DriverFactory.DriverCameraFeedPath);
            string scannedWith;
            if (driverHome.IsScanQrCodeShown(3))
            {
                scannedWith = "Scan QR Code";
                driverHome.ScanQrCode(cancelActiveLoad: false);
            }
            else
            {
                scannedWith = "Scan Receipt Code";
                driverHome.ScanReceiptCode();
            }
            var alert = driverHome.CaptureAndDismissAlert(15);
            string routeAfterScan = driverApp.Url;

            var restartedHome = DriverAppFlow.Restart(driverApp);
            string loadAfterRestart = restartedHome.CurrentLoad;

            BaselineRecorder.Save("QR-B04", _node, new
            {
                firstTicketId = first.TicketId,
                secondTicketId = second.TicketId,
                firstLoad,
                homeActionsWithActiveLoad = homeActions,
                scannedWith,
                alert,
                routeAfterScan,
                loadAfterRestart,
                outcome = loadAfterRestart == firstLoad ? "kept first load"
                    : restartedHome.HasActiveLoad ? "replaced by second load"
                    : "no active load"
            });

            Assert.That(restartedHome.HasActiveLoad, Is.True,
                $"Scanning a second ticket ({second.TicketId}) with '{scannedWith}' left the Driver app with no active load (first load: {firstLoad}).");
        }

        // QR-B05: the same collection QR scanned again after the load was cancelled (BLE re-send reference)
        [Test]
        public void QR_B05_SameQrScannedTwice_BehaviorIsRecorded()
        {
            var ticket = SubmitTicket(nameof(QR_B05_SameQrScannedTwice_BehaviorIsRecorded));
            var (driverApp, driverHome) = DriverAppFlow.StartOnHome();

            QrScanFeed.Stage(driverApp, ticket.QrPng, rasterizer: DriverFactory.DriverFlex, DriverFactory.DriverCameraFeedPath);
            driverHome.ScanQrCode();
            string firstLoad = driverHome.WaitForActiveLoad();

            // ScanQrCode cancels the active load first, then the same code is scanned again
            QrScanFeed.Stage(driverApp, ticket.QrPng, rasterizer: DriverFactory.DriverFlex, DriverFactory.DriverCameraFeedPath);
            driverHome.ScanQrCode();
            var (alert, hasLoad) = WaitForScanOutcome(driverHome, timeoutSeconds: 30);
            string loadAfterRescan = hasLoad ? driverHome.CurrentLoad : string.Empty;

            BaselineRecorder.Save("QR-B05", _node, new
            {
                ticketId = ticket.TicketId,
                firstLoad,
                alert,
                loadAfterRescan,
                sameLoadAgain = loadAfterRescan == firstLoad
            });

            Assert.Pass($"Re-scan of {ticket.TicketId}: alert '{alert?.Header ?? "none"}', load after re-scan '{loadAfterRescan}'.");
        }

        // QR-B06: text that is not a ticket must not become a load (FR-5 reference)
        [Test]
        public void QR_B06_NonTicketQr_IsRejected_And_NothingIsSaved()
        {
            var (driverApp, driverHome) = DriverAppFlow.StartOnHome();
            if (driverApp is not AndroidDriver) Assert.Ignore("Staging arbitrary QR text needs a device (RunMode=Mobile).");

            driverHome.CancelStaleLoadIfPresent();
            var observed = ScanTextAndRestart(driverApp, ref driverHome, $"RT-QA-NOT-A-TICKET-{Guid.NewGuid():N}");
            BaselineRecorder.Save("QR-B06", _node, observed);

            Assert.Multiple(() =>
            {
                Assert.That(observed.HadLoadAfterScan, Is.False, "The Driver app turned a non-ticket QR code into a load.");
                Assert.That(observed.HadLoadAfterRestart, Is.False, "The Driver app stored a load from a non-ticket QR code.");
            });
        }

        // QR-B07: a real ticket payload, truncated or with one character changed. A camera can't produce these (QR
        // error correction), but a Bluetooth transfer could, so this records whether the app has its own integrity
        // check. Known QR decoding bugs are out of the POC scope, so a saved load passes with a note in the report.
        [Test]
        public void QR_B07_DamagedTicketPayload_HandlingIsRecorded()
        {
            var ticket = SubmitTicket(nameof(QR_B07_DamagedTicketPayload_HandlingIsRecorded));
            var (driverApp, driverHome) = DriverAppFlow.StartOnHome();
            if (driverApp is not AndroidDriver) Assert.Ignore("Staging arbitrary QR text needs a device (RunMode=Mobile).");

            var variants = new Dictionary<string, string>
            {
                ["truncated-half"] = ticket.QrText[..(ticket.QrText.Length / 2)],
                ["one-char-altered"] = AlterOneCharacter(ticket.QrText)
            };

            var results = new Dictionary<string, ScanObservation>();
            foreach (var (name, payload) in variants)
            {
                driverHome.CancelStaleLoadIfPresent();
                results[name] = ScanTextAndRestart(driverApp, ref driverHome, payload);
            }

            BaselineRecorder.Save("QR-B07", _node, new
            {
                ticketId = ticket.TicketId,
                payloadLength = ticket.QrText.Length,
                results
            });

            foreach (var (name, result) in results.Where(r => r.Value.HadLoadAfterRestart))
            {
                QrBaselineReport.AddNote($"Finding: damaged payload '{name}' was stored as load '{result.LoadAfterRestart}': the QR path has no integrity check of its own, so BLE must add one (FR-5).");
            }
        }

        // QR-B09: QR handoff with both devices in airplane mode (demo conditions). Flex logs in and submits online;
        // QR generation and the Driver scan run offline. Debug builds still reach the local backend through adb
        // reverse (loopback), so sync status is not a reliable offline signal and is only checked once back online.
        [Test]
        public void QR_B09_QrHandoff_WorksInAirplaneMode()
        {
            if (DriverFactory.DriverFlex is not AndroidDriver flexAndroid)
            {
                Assert.Ignore("Airplane mode needs a device (RunMode=Mobile).");
                return;
            }

            var flow = new CollectionLoadTicketFlow(DriverFactory.DriverFlex, _wait.Value!, _node);
            flow.LoginAndEnterWebView();
            var draft = flow.FillAndSubmitTicket(Notes(nameof(QR_B09_QrHandoff_WorksInAirplaneMode)));
            var (driverApp, driverHome) = DriverAppFlow.StartOnHome();

            string[] udids = [AdbHelper.GetUdid(flexAndroid)!, AdbHelper.GetUdid((AndroidDriver)driverApp)!];
            string offlineLoad;
            try
            {
                foreach (string udid in udids) BluetoothHelper.SetAirplaneMode(udid, true);
                Assert.That(udids.All(BluetoothHelper.IsAirplaneModeOn), Is.True, "Airplane mode could not be turned on.");

                byte[] qrPng = flow.GenerateAndStoreQr();
                QrScanFeed.Stage(driverApp, qrPng, rasterizer: DriverFactory.DriverFlex, DriverFactory.DriverCameraFeedPath);
                driverHome.ScanQrCode();
                offlineLoad = driverHome.WaitForActiveLoad();
            }
            finally
            {
                foreach (string udid in udids) BluetoothHelper.SetAirplaneMode(udid, false);
            }

            string ticketId = flow.VerifyTicketSubmittedAndSynced(draft);
            BaselineRecorder.Save("QR-B09", _node, new { ticketId, offlineLoad });
        }

        private record ScanObservation(DriverAlert? Alert, bool HadLoadAfterScan, string LoadAfterScan, bool HadLoadAfterRestart, string LoadAfterRestart);

        // Scans the given text, records what the app did, then restarts it to see whether anything was stored
        private ScanObservation ScanTextAndRestart(IWebDriver driverApp, ref DriverHomePage driverHome, string qrText)
        {
            QrScanFeed.StageText(driverApp, qrText);
            driverHome.ScanQrCode(cancelActiveLoad: false);
            var (alert, hasLoad) = WaitForScanOutcome(driverHome, timeoutSeconds: 15);
            string loadAfterScan = hasLoad ? driverHome.CurrentLoad : string.Empty;

            driverHome = DriverAppFlow.Restart(driverApp);
            bool hasLoadAfterRestart = driverHome.HasActiveLoad;
            return new ScanObservation(alert, hasLoad, loadAfterScan, hasLoadAfterRestart,
                hasLoadAfterRestart ? driverHome.CurrentLoad : string.Empty);
        }

        // After a scan the app either shows an alert or takes the load (or does nothing); returns whichever came first
        private static (DriverAlert? Alert, bool HasLoad) WaitForScanOutcome(DriverHomePage driverHome, int timeoutSeconds)
        {
            var deadline = DateTime.Now.AddSeconds(timeoutSeconds);
            DriverAlert? alert = null;
            while (DateTime.Now < deadline)
            {
                alert ??= driverHome.CaptureAndDismissAlert(1);
                if (driverHome.HasActiveLoad) return (alert, true);
                if (alert != null) break;
            }
            return (alert, driverHome.HasActiveLoad);
        }

        // Logs in on the first call, then fills, submits and syncs a ticket and decodes its QR
        private SubmittedTicket SubmitTicket(string testName, CollectionLoadTicketFlow? loggedInFlow = null)
        {
            var flow = loggedInFlow;
            if (flow == null)
            {
                flow = new CollectionLoadTicketFlow(DriverFactory.DriverFlex, _wait.Value!, _node);
                flow.LoginAndEnterWebView();
            }

            var draft = flow.FillAndSubmitTicket(Notes(testName));
            byte[] qrPng = flow.GenerateAndStoreQr();
            string ticketId = flow.VerifyTicketSubmittedAndSynced(draft);
            string qrText = QrScanFeed.Decode(qrPng, DriverFactory.DriverFlex);
            Console.WriteLine($"🔳 Ticket {ticketId} QR payload: {qrText.Length} characters.");
            return new SubmittedTicket(flow, draft, ticketId, qrPng, qrText);
        }

        private static DriverSnapshot Snapshot(DriverHomePage driverHome)
        {
            string loadQrText = QrScanFeed.Decode(driverHome.GetCurrentLoadQrPng(), DriverFactory.DriverFlex);
            return new DriverSnapshot(driverHome.CurrentLoad, loadQrText, BaselineRecorder.Sha256(loadQrText));
        }

        private string Notes(string testName) => $"Automated test - {testName} {_node} {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

        // Changes the first letter or digit from the middle of the payload onwards to a different one of the same kind
        private static string AlterOneCharacter(string payload)
        {
            int index = Enumerable.Range(payload.Length / 2, payload.Length - payload.Length / 2)
                .Concat(Enumerable.Range(0, payload.Length / 2))
                .First(i => char.IsAsciiLetterOrDigit(payload[i]));
            char c = payload[index];
            char altered = char.IsAsciiDigit(c) ? (char)('0' + (c - '0' + 1) % 10)
                : char.IsAsciiLetterUpper(c) ? (char)('A' + (c - 'A' + 1) % 26)
                : (char)('a' + (c - 'a' + 1) % 26);
            return string.Concat(payload.AsSpan(0, index), altered.ToString(), payload.AsSpan(index + 1));
        }
    }
}
