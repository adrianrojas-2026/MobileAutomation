using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Flows;
using MobileAutomation.Tests.Pages.Driver;
using MobileAutomation.Tests.Reporting;
using MobileAutomation.Tests.Utilities;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;

namespace RecoveryTrac.AutomatedTests
{
    // Disposal counterpart of QrBaselineTests: records how the QR disposal leg behaves today, so the BLE disposal tests
    // (R2: Receive Data for the Driver's load on the Flex disposal form, Send Receipt back to the Driver app) have an
    // expected result to compare with. Two QR hops are covered:
    //   Driver load QR  -> Flex 'Load - Disposal' import  (QR-D01, D07)       BLE: Consumer Shared Data / Receive Data
    //   Flex disposal QR -> Driver app receipt            (QR-D02...D06, D08...D10)  BLE: Driver App Receipt Data / Send Receipt
    // Each test saves what it observed under Baselines/ (BaselineRecorder) and only asserts what must hold for QR too.
    // Split between Node2 and Node3 like QrBaselineTests; a fixture of either kind on the same pair waits for
    // DriverFactory's node-pair lock.
    [Category("QR")]
    [Category("QR-Baseline")]
    [Category("QR-Baseline-Disposal")]
    [Parallelizable(ParallelScope.Self)]
    public abstract class QrDisposalBaselineTests : QrBaselineTestBase
    {
        protected QrDisposalBaselineTests(string node)
            : base(node)
        {
        }

        // A collection ticket that is the Driver app's current load, with the load QR the disposal site scans
        private record LoadOnDriver(SubmittedTicket Ticket, IWebDriver DriverApp, DriverHomePage DriverHome, string Load, byte[] LoadQrPng, string LoadQrText);

        private record ReceiptSnapshot(string ReceiptId, string TruckNumber, string DebrisType, string Capacity, string LoadCall);

        // What a scan at Scan Receipt Code led to: a receipt, an alert, or nothing
        private record ReceiptScanOutcome(DriverAlert? Alert, string? ReceiptId, string Url);

        // QR-D01: what the Driver's load QR carries into the Flex disposal form (what BLE Receive Data must import)
        // QR-D02: what the disposal QR carries to the Driver app (what BLE Send Receipt must send unchanged)
        // QR-D03: the disposal QR reopened from the submitted card carries the same payload (QR fallback after a failed send)
        protected void DisposalPayloads_AreFingerprinted_And_ReopenedQrMatches()
        {
            var load = CollectAndScan(TestName);
            var truck = load.Ticket.Draft.Truck;
            var disposal = new LoadDisposalFlow(DriverFactory.DriverFlex, _node);

            disposal.StoreAndStageDriversQr(load.LoadQrPng);
            var ticketsBefore = disposal.OpenNewForm();
            string site = disposal.ScanDriversQrAndSelectSite();
            bool nextEnabled = disposal.Form.IsNextEnabled(timeoutSeconds: 20);
            string importedTruck = disposal.Form.ImportedTruckNumber;

            BaselineRecorder.Save("QR-D01", _node, new
            {
                ticketId = load.Ticket.TicketId,
                load.Load,
                payloadLength = load.LoadQrText.Length,
                payloadSha256 = BaselineRecorder.Sha256(load.LoadQrText),
                payload = load.LoadQrText,
                // Whether the Driver re-shows the collection QR as is, or builds its own load payload
                sameAsCollectionPayload = load.LoadQrText == load.Ticket.QrText,
                disposalSite = site,
                importedTruckNumber = importedTruck,
                nextEnabledAfterImport = nextEnabled
            });

            disposal.AssertImportedAndContinue(site, truck.TruckNumber);
            disposal.CompleteLoadCallAndSubmit();
            var submitted = disposal.VerifySubmittedAndGetQr(ticketsBefore, load.Ticket.TicketId, truck.TruckNumber);
            string disposalText = QrScanFeed.Decode(submitted.QrPng, DriverFactory.DriverFlex);
            Console.WriteLine($"🔳 Disposal ticket {submitted.TicketId} QR payload: {disposalText.Length} characters.");

            BaselineRecorder.Save("QR-D02", _node, new
            {
                collectionTicketId = load.Ticket.TicketId,
                disposalTicketId = submitted.TicketId,
                submitted.QrPromptedOnSubmit,
                card = submitted.Card,
                payloadLength = disposalText.Length,
                payloadSha256 = BaselineRecorder.Sha256(disposalText),
                payload = disposalText,
                containsCollectionTicketId = disposalText.Contains(load.Ticket.TicketId.TrimStart('#'))
            });

            byte[] reopenedPng;
            try
            {
                reopenedPng = disposal.OpenCardQr(submitted.TicketId);
            }
            catch (WebDriverTimeoutException ex)
            {
                Assert.Fail($"The QR code of submitted disposal ticket {submitted.TicketId} could not be reopened from its card: {ex.Message}");
                return;
            }
            string reopenedText = QrScanFeed.Decode(reopenedPng, DriverFactory.DriverFlex);

            BaselineRecorder.Save("QR-D03", _node, new
            {
                disposalTicketId = submitted.TicketId,
                reopenedPayloadSha256 = BaselineRecorder.Sha256(reopenedText),
                reopenedMatchesGenerated = reopenedText == disposalText
            });

            Assert.Multiple(() =>
            {
                Assert.That(load.LoadQrText, Is.Not.Empty, "The Driver app's load QR code decoded to an empty payload.");
                Assert.That(load.LoadQrText, Has.Length.LessThanOrEqualTo(MaxBlePayloadChars),
                    $"The Driver's load QR payload ({load.LoadQrText.Length} characters) is larger than the {MaxBlePayloadChars} characters the BLE transfer supports (FR-4).");
                Assert.That(disposalText, Is.Not.Empty, "The disposal QR code decoded to an empty payload.");
                Assert.That(disposalText, Has.Length.LessThanOrEqualTo(MaxBlePayloadChars),
                    $"The disposal QR payload ({disposalText.Length} characters) is larger than the {MaxBlePayloadChars} characters the BLE transfer supports (FR-4).");
                Assert.That(reopenedText, Is.EqualTo(disposalText),
                    $"The QR reopened from disposal ticket {submitted.TicketId} carries a different payload than the one generated on submit.");
            });
        }

        // QR-D04: what the Driver app shows after scanning the disposal QR (the receipt BLE Send Receipt must produce)
        // QR-D05: what is stored: the receipt is still listed after a restart, and whether the receipt ended the load
        protected void ReceiptAfterQrScan_IsRecorded_And_SurvivesRestart()
        {
            var load = CollectAndScan(TestName);
            var truck = load.Ticket.Draft.Truck;
            var submitted = SubmitDisposal(load.LoadQrPng, load.Ticket);

            StageOnDriver(load.DriverApp, submitted.QrPng);
            load.DriverHome.ScanReceiptCode();
            var receiptPage = new DriverReceiptPage(load.DriverApp);
            receiptPage.WaitForReceipt();
            var receipt = ReadReceipt(receiptPage);
            string receiptUrl = load.DriverApp.Url;

            var home = DriverAppFlow.Restart(load.DriverApp);
            bool hasLoadAfterRestart = home.HasActiveLoad;
            string loadAfterRestart = hasLoadAfterRestart ? home.CurrentLoad : string.Empty;
            var homeActions = home.VisibleHomeActions();
            home.OpenTab("receipts");
            bool listedAfterRestart = home.IsTextShown(receipt.ReceiptId);

            BaselineRecorder.Save("QR-D04", _node, new
            {
                collectionTicketId = load.Ticket.TicketId,
                disposalTicketId = submitted.TicketId,
                load.Load,
                receipt,
                receiptUrl
            });
            BaselineRecorder.Save("QR-D05", _node, new
            {
                receipt.ReceiptId,
                listedAfterRestart,
                loadAfterRestart,
                homeActionsAfterRestart = homeActions,
                outcome = hasLoadAfterRestart ? "load still active after the receipt" : "receipt ended the load"
            });

            Assert.Multiple(() =>
            {
                // Receipts are numbered after the collection ticket, e.g. RQTE-LC-100N > RQTE-LC-100N.A
                Assert.That(receipt.ReceiptId, Does.StartWith(load.Ticket.TicketId.TrimStart('#')),
                    $"Receipt {receipt.ReceiptId} does not belong to collection ticket {load.Ticket.TicketId}.");
                Assert.That(receipt.TruckNumber, Is.EqualTo(truck.TruckNumber), $"Receipt {receipt.ReceiptId} shows an unexpected Truck Number.");
                Assert.That(receipt.DebrisType, Is.EqualTo("Vegetative/Woody").IgnoreCase, $"Receipt {receipt.ReceiptId} shows an unexpected Debris Type.");
                Assert.That(receipt.Capacity, Is.EqualTo(truck.TruckCapacity), $"Receipt {receipt.ReceiptId} shows an unexpected Capacity.");
                Assert.That(receipt.LoadCall, Is.EqualTo(LoadDisposalFlow.LoadCall), $"Receipt {receipt.ReceiptId} shows an unexpected Load Call.");
                Assert.That(listedAfterRestart, Is.True,
                    $"Receipt {receipt.ReceiptId} is not listed on the Receipts tab after a restart, so it was not stored.");
            });
        }

        // QR-D06: the same disposal QR scanned again after the receipt (BLE Send Receipt re-send reference). Scanned with
        // whatever Home offers after the receipt: Scan Receipt Code if the load is still active, Scan QR Code otherwise
        protected void SameDisposalQrScannedTwice_BehaviorIsRecorded()
        {
            var load = CollectAndScan(TestName);
            var submitted = SubmitDisposal(load.LoadQrPng, load.Ticket);

            StageOnDriver(load.DriverApp, submitted.QrPng);
            load.DriverHome.ScanReceiptCode();
            string firstReceiptId = new DriverReceiptPage(load.DriverApp).WaitForReceipt();

            var home = DriverAppFlow.Restart(load.DriverApp);
            bool loadActiveBeforeRescan = home.HasActiveLoad;
            StageOnDriver(load.DriverApp, submitted.QrPng);
            string scannedWith;
            if (loadActiveBeforeRescan)
            {
                scannedWith = "Scan Receipt Code";
                home.ScanReceiptCode();
            }
            else
            {
                scannedWith = "Scan QR Code";
                home.ScanQrCode(cancelActiveLoad: false);
            }
            var outcome = WaitForReceiptOutcome(load.DriverApp, home, timeoutSeconds: 30);
            bool becameLoad = outcome.ReceiptId == null && home.HasActiveLoad && !loadActiveBeforeRescan;
            string loadAfterRescan = becameLoad ? home.CurrentLoad : string.Empty;

            BaselineRecorder.Save("QR-D06", _node, new
            {
                collectionTicketId = load.Ticket.TicketId,
                disposalTicketId = submitted.TicketId,
                firstReceiptId,
                loadActiveBeforeRescan,
                scannedWith,
                outcome.Alert,
                secondReceiptId = outcome.ReceiptId,
                sameReceiptAgain = outcome.ReceiptId == firstReceiptId,
                routeAfterRescan = outcome.Url,
                loadAfterRescan,
                result = outcome.ReceiptId == firstReceiptId ? "same receipt shown again"
                    : outcome.ReceiptId != null ? "new receipt created"
                    : becameLoad ? "disposal QR taken as a load"
                    : outcome.Alert != null ? "rejected with an alert"
                    : "ignored"
            });

            Assert.That(becameLoad, Is.False,
                $"Scanning disposal ticket {submitted.TicketId}'s QR with Scan QR Code made it a load ('{loadAfterRescan}').");
            Assert.Pass($"Re-scan of disposal {submitted.TicketId} with '{scannedWith}': alert '{outcome.Alert?.Header ?? "none"}', " +
                        $"receipt '{outcome.ReceiptId ?? "none"}' (first {firstReceiptId}).");
        }

        // QR-D07: what the Flex disposal form does with a code that is not a load, and with a real load payload cut in
        // half or with one character changed (Receive Data must reject damaged data, FR-5 / Check 3 reference). The
        // same form then imports the real load QR and is submitted, so bad scans must not break it
        protected void DisposalImport_NonLoadAndDamagedQr_HandlingIsRecorded()
        {
            if (DriverFactory.DriverFlex is not AndroidDriver) Assert.Ignore("Staging arbitrary QR text needs a device (RunMode=Mobile).");
            var load = CollectAndScan(TestName);
            var truck = load.Ticket.Draft.Truck;
            var disposal = new LoadDisposalFlow(DriverFactory.DriverFlex, _node);

            var ticketsBefore = disposal.OpenNewForm();
            disposal.Form.WaitForStep(LoadDisposalFlow.ImportStep);
            string site = disposal.Form.SelectFirstDisposalSite();

            var variants = new Dictionary<string, string> { ["not-a-load"] = $"RT-QA-NOT-A-LOAD-{Guid.NewGuid():N}" };
            foreach (var (name, payload) in DamagedVariants(load.LoadQrText)) variants[name] = payload;

            var results = new Dictionary<string, object>();
            var imported = new List<string>();
            foreach (var (name, payload) in variants)
            {
                string truckBefore = disposal.Form.ImportedTruckNumber;
                disposal.StageDriversQrText(payload);
                IonicToasts.StartRecording(DriverFactory.DriverFlex);
                disposal.Form.ScanDriversQrCode();
                string? alert = disposal.Form.CaptureAndCloseAlert(timeoutSeconds: 5);
                string? toast = IonicToasts.Message(DriverFactory.DriverFlex, timeoutSeconds: 3);
                bool nextEnabled = disposal.Form.IsNextEnabled(timeoutSeconds: 8);
                string truckAfter = disposal.Form.ImportedTruckNumber;
                string siteAfter = disposal.Form.GetChoiceValue("Disposal Site");

                if (nextEnabled || truckAfter != truckBefore) imported.Add(name);
                results[name] = new { payloadLength = payload.Length, alert, toast, nextEnabled, truckBefore, truckAfter, siteAfter };
            }

            // The real load QR in the same form, then the rest of the disposal as in the E2E
            disposal.StageDriversQr(load.LoadQrPng);
            disposal.Form.ScanDriversQrCode();
            disposal.Form.SelectFirstDisposalSite();
            disposal.AssertImportedAndContinue(site, truck.TruckNumber);
            disposal.CompleteLoadCallAndSubmit();
            var submitted = disposal.VerifySubmittedAndGetQr(ticketsBefore, load.Ticket.TicketId, truck.TruckNumber);

            BaselineRecorder.Save("QR-D07", _node, new
            {
                collectionTicketId = load.Ticket.TicketId,
                load.Load,
                payloadLength = load.LoadQrText.Length,
                disposalSite = site,
                results,
                importedVariants = imported,
                disposalTicketAfterBadScans = submitted.TicketId
            });

            foreach (string name in imported.Where(n => n != "not-a-load"))
            {
                QrBaselineReport.AddNote($"Finding: damaged load payload '{name}' was imported by the Flex disposal form: the QR import has no integrity check of its own, so BLE Receive Data must add one (FR-5).");
            }
            Assert.That(imported, Does.Not.Contain("not-a-load"),
                "The Flex disposal form imported a QR code that is not a Driver load.");
        }

        // QR-D08: what the Driver app does at Scan Receipt Code with a code that is not a receipt, and with the real
        // disposal payload cut in half or with one character changed (Send Receipt must reject damaged data). The real
        // disposal QR is scanned last and must still give the receipt
        protected void ReceiptScan_NonReceiptAndDamagedQr_HandlingIsRecorded()
        {
            if (DriverFactory.DriverFlex is not AndroidDriver) Assert.Ignore("Staging arbitrary QR text needs a device (RunMode=Mobile).");
            var load = CollectAndScan(TestName);
            var submitted = SubmitDisposal(load.LoadQrPng, load.Ticket);
            string disposalText = QrScanFeed.Decode(submitted.QrPng, DriverFactory.DriverFlex);

            var variants = new Dictionary<string, string> { ["not-a-receipt"] = $"RT-QA-NOT-A-RECEIPT-{Guid.NewGuid():N}" };
            foreach (var (name, payload) in DamagedVariants(disposalText)) variants[name] = payload;

            var driverApp = load.DriverApp;
            var home = load.DriverHome;
            var results = new Dictionary<string, object>();
            var receipted = new Dictionary<string, string>();
            foreach (var (name, payload) in variants)
            {
                // Scan Receipt Code is only offered with a load: a receipt from the previous variant may have ended it
                bool loadRestored = !home.HasActiveLoad;
                if (loadRestored) ScanCollectionQr(driverApp, home, load.Ticket.QrPng);

                QrScanFeed.StageText(driverApp, payload);
                home.ScanReceiptCode();
                var outcome = WaitForReceiptOutcome(driverApp, home, timeoutSeconds: 20);
                home = DriverAppFlow.Restart(driverApp);
                bool hasLoadAfterRestart = home.HasActiveLoad;

                if (outcome.ReceiptId != null) receipted[name] = outcome.ReceiptId;
                results[name] = new
                {
                    payloadLength = payload.Length,
                    loadRestored,
                    outcome.Alert,
                    outcome.ReceiptId,
                    routeAfterScan = outcome.Url,
                    loadAfterRestart = hasLoadAfterRestart ? home.CurrentLoad : string.Empty
                };
            }

            // The real disposal QR last
            if (!home.HasActiveLoad) ScanCollectionQr(driverApp, home, load.Ticket.QrPng);
            StageOnDriver(driverApp, submitted.QrPng);
            home.ScanReceiptCode();
            string receiptId = new DriverReceiptPage(driverApp).WaitForReceipt();

            BaselineRecorder.Save("QR-D08", _node, new
            {
                collectionTicketId = load.Ticket.TicketId,
                disposalTicketId = submitted.TicketId,
                payloadLength = disposalText.Length,
                results,
                receiptAfterBadScans = receiptId
            });

            foreach (var (name, id) in receipted.Where(r => r.Key != "not-a-receipt"))
            {
                QrBaselineReport.AddNote($"Finding: damaged disposal payload '{name}' gave receipt '{id}': the Driver app's receipt scan has no integrity check of its own, so BLE Send Receipt must add one (FR-5).");
            }
            Assert.Multiple(() =>
            {
                Assert.That(receipted.Keys, Does.Not.Contain("not-a-receipt"),
                    $"The Driver app opened receipt '{receipted.GetValueOrDefault("not-a-receipt")}' for a QR code that is not a disposal ticket.");
                Assert.That(receiptId, Does.StartWith(load.Ticket.TicketId.TrimStart('#')),
                    $"After the bad scans, disposal {submitted.TicketId} gave receipt {receiptId}, which does not belong to collection ticket {load.Ticket.TicketId}.");
            });
        }

        // QR-D09: the disposal QR of another load scanned by a driver holding load A (BLE: Send Receipt picks the wrong
        // Driver App). Load B is collected first, its load QR kept for the disposal, then the driver switches to load A
        protected void DisposalQrOfAnotherLoad_BehaviorIsRecorded()
        {
            var first = SubmitTicket(TestName + " A");
            var second = SubmitTicket(TestName + " B", first.Flow);
            var (driverApp, driverHome) = DriverAppFlow.StartOnHome();

            string loadB = ScanCollectionQr(driverApp, driverHome, second.QrPng);
            byte[] loadBQrPng = driverHome.GetCurrentLoadQrPng();
            // ScanQrCode abandons load B before scanning ticket A
            string loadA = ScanCollectionQr(driverApp, driverHome, first.QrPng);

            var disposalB = SubmitDisposal(loadBQrPng, second);

            StageOnDriver(driverApp, disposalB.QrPng);
            driverHome.ScanReceiptCode();
            var outcome = WaitForReceiptOutcome(driverApp, driverHome, timeoutSeconds: 30);
            var home = DriverAppFlow.Restart(driverApp);
            string loadAfter = home.HasActiveLoad ? home.CurrentLoad : string.Empty;

            string? receiptOwner = outcome.ReceiptId == null ? null
                : IsReceiptOf(outcome.ReceiptId, loadA) ? "A"
                : IsReceiptOf(outcome.ReceiptId, loadB) ? "B"
                : "other";
            BaselineRecorder.Save("QR-D09", _node, new
            {
                ticketA = first.TicketId,
                ticketB = second.TicketId,
                loadA,
                loadB,
                disposalTicketB = disposalB.TicketId,
                outcome.Alert,
                outcome.ReceiptId,
                receiptOwner,
                routeAfterScan = outcome.Url,
                loadAfterScan = loadAfter,
                result = receiptOwner == "B" ? "receipt for load B accepted while load A is active"
                    : receiptOwner != null ? $"receipt for load {receiptOwner}"
                    : outcome.Alert != null ? "rejected with an alert"
                    : "ignored"
            });

            if (receiptOwner == "B")
            {
                QrBaselineReport.AddNote($"Finding: with load {loadA} active, the Driver app accepted disposal {disposalB.TicketId} of load {loadB} as receipt {outcome.ReceiptId}: QR doesn't check the receipt matches the current load, so BLE Send Receipt to the wrong Driver App would be accepted too.");
            }
            Assert.That(receiptOwner, Is.Not.EqualTo("A"),
                $"Disposal {disposalB.TicketId} of load {loadB} became receipt {outcome.ReceiptId} of load {loadA}.");
        }

        // Receipts are numbered after their load: RQTE-LC-100N > RQTE-LC-100N.A
        private static bool IsReceiptOf(string receiptId, string load) =>
            receiptId == load || receiptId.StartsWith(load + ".", StringComparison.OrdinalIgnoreCase);

        // QR-D10: disposal and receipt with both devices in airplane mode (disposal sites often have no signal). The
        // collection runs online; the disposal import, submit, disposal QR and receipt scan run offline, and the
        // disposal ticket must sync once back online. Debug builds still reach the local backend through adb reverse,
        // so sync status is only checked once back online (as in QR-B09)
        protected void DisposalAndReceipt_WorkInAirplaneMode()
        {
            if (DriverFactory.DriverFlex is not AndroidDriver flexAndroid)
            {
                Assert.Ignore("Airplane mode needs a device (RunMode=Mobile).");
                return;
            }

            var load = CollectAndScan(TestName);
            var disposal = new LoadDisposalFlow(DriverFactory.DriverFlex, _node);

            string[] udids = [AdbHelper.GetUdid(flexAndroid)!, AdbHelper.GetUdid((AndroidDriver)load.DriverApp)!];
            SubmittedDisposal submitted;
            string offlineReceiptId;
            try
            {
                foreach (string udid in udids) BluetoothHelper.SetAirplaneMode(udid, true);
                Assert.That(udids.All(BluetoothHelper.IsAirplaneModeOn), Is.True, "Airplane mode could not be turned on.");

                submitted = SubmitDisposal(load.LoadQrPng, load.Ticket, disposal, waitForSync: false);
                StageOnDriver(load.DriverApp, submitted.QrPng);
                load.DriverHome.ScanReceiptCode();
                offlineReceiptId = new DriverReceiptPage(load.DriverApp).WaitForReceipt();
            }
            finally
            {
                foreach (string udid in udids) BluetoothHelper.SetAirplaneMode(udid, false);
            }

            bool synced = disposal.Forms.WaitUntilSynced(submitted.TicketId);
            BaselineRecorder.Save("QR-D10", _node, new
            {
                collectionTicketId = load.Ticket.TicketId,
                disposalTicketId = submitted.TicketId,
                submitted.QrPromptedOnSubmit,
                offlineReceiptId,
                syncedOnceOnline = synced
            });

            Assert.Multiple(() =>
            {
                Assert.That(offlineReceiptId, Does.StartWith(load.Ticket.TicketId.TrimStart('#')),
                    $"Offline receipt {offlineReceiptId} does not belong to collection ticket {load.Ticket.TicketId}.");
                Assert.That(synced, Is.True,
                    $"Disposal ticket {submitted.TicketId}, submitted in airplane mode, did not reach 'Synced' once back online.");
            });
        }

        // Submits a collection ticket, starts the Driver app and scans the ticket's QR, returning the load and its QR
        private LoadOnDriver CollectAndScan(string testName)
        {
            var ticket = SubmitTicket(testName);
            var (driverApp, driverHome) = DriverAppFlow.StartOnHome();
            string load = ScanCollectionQr(driverApp, driverHome, ticket.QrPng);
            Console.WriteLine($"🚚 Driver app current load: {load}");

            byte[] loadQrPng = driverHome.GetCurrentLoadQrPng();
            string loadQrText = QrScanFeed.Decode(loadQrPng, DriverFactory.DriverFlex);
            Console.WriteLine($"🔳 Driver load {load} QR payload: {loadQrText.Length} characters.");
            return new LoadOnDriver(ticket, driverApp, driverHome, load, loadQrPng, loadQrText);
        }

        // Steps 23-26 of the E2E: a Load - Disposal ticket for the load whose QR is given, submitted, with its QR
        private SubmittedDisposal SubmitDisposal(byte[] driversQrPng, SubmittedTicket collection,
            LoadDisposalFlow? disposal = null, bool waitForSync = true)
        {
            disposal ??= new LoadDisposalFlow(DriverFactory.DriverFlex, _node);
            string truckNumber = collection.Draft.Truck.TruckNumber;

            disposal.StoreAndStageDriversQr(driversQrPng);
            var ticketsBefore = disposal.OpenNewForm();
            string site = disposal.ScanDriversQrAndSelectSite();
            disposal.AssertImportedAndContinue(site, truckNumber);
            disposal.CompleteLoadCallAndSubmit();
            return disposal.VerifySubmittedAndGetQr(ticketsBefore, collection.TicketId, truckNumber, waitForSync);
        }

        // After a scan at Scan Receipt Code the app either opens a receipt, shows an alert, or does nothing; returns
        // whichever came first
        private static ReceiptScanOutcome WaitForReceiptOutcome(IWebDriver driverApp, DriverHomePage driverHome, int timeoutSeconds)
        {
            var receiptPage = new DriverReceiptPage(driverApp);
            var deadline = DateTime.Now.AddSeconds(timeoutSeconds);
            DriverAlert? alert = null;
            while (DateTime.Now < deadline)
            {
                if (receiptPage.IsShown) break;
                alert = driverHome.CaptureAndDismissAlert(1);
                if (alert != null) break;
            }
            string? receiptId = receiptPage.IsShown ? receiptPage.GetValue("Receipt Identifier") : null;
            return new ReceiptScanOutcome(alert, receiptId, driverApp.Url);
        }

        private static ReceiptSnapshot ReadReceipt(DriverReceiptPage receipt) => new(
            receipt.GetValue("Receipt Identifier"),
            receipt.GetValue("Truck Number"),
            receipt.GetValue("Debris Type"),
            receipt.GetValue("Capacity"),
            receipt.GetValue("Load Call"));
    }

    // Each test runs a collection ticket and a disposal ticket (~6 min; D09 runs two collections). Estimated, not
    // measured yet: rebalance once a run gives real durations, as for QrBaselineTests
    [TestFixture("Node2")]
    public class QrDisposalBaselineNode2Tests(string node) : QrDisposalBaselineTests(node)
    {
        // QR-D01 / D02 / D03: what the load QR and the disposal QR carry, and the disposal QR reopened from its card
        [Test]
        public void QR_D01_D02_D03_DisposalPayloads_AreFingerprinted_And_ReopenedQrMatches() => DisposalPayloads_AreFingerprinted_And_ReopenedQrMatches();

        // QR-D06: the same disposal QR scanned again after the receipt (Send Receipt re-send reference)
        [Test]
        public void QR_D06_SameDisposalQrScannedTwice_BehaviorIsRecorded() => SameDisposalQrScannedTwice_BehaviorIsRecorded();

        // QR-D08: non-receipt and damaged disposal codes at Scan Receipt Code (Send Receipt FR-5 reference)
        [Test]
        public void QR_D08_ReceiptScan_NonReceiptAndDamagedQr_HandlingIsRecorded() => ReceiptScan_NonReceiptAndDamagedQr_HandlingIsRecorded();

        // QR-D10: disposal and receipt with this node's two devices in airplane mode
        [Test]
        public void QR_D10_DisposalAndReceipt_WorkInAirplaneMode() => DisposalAndReceipt_WorkInAirplaneMode();
    }

    [TestFixture("Node3")]
    public class QrDisposalBaselineNode3Tests(string node) : QrDisposalBaselineTests(node)
    {
        // QR-D04 / D05: the receipt after a disposal QR scan, and what is stored after a restart
        [Test]
        public void QR_D04_D05_ReceiptAfterQrScan_IsRecorded_And_SurvivesRestart() => ReceiptAfterQrScan_IsRecorded_And_SurvivesRestart();

        // QR-D07: non-load and damaged load codes at the Flex disposal import (Receive Data FR-5 reference)
        [Test]
        public void QR_D07_DisposalImport_NonLoadAndDamagedQr_HandlingIsRecorded() => DisposalImport_NonLoadAndDamagedQr_HandlingIsRecorded();

        // QR-D09: a disposal QR of another load scanned by a driver holding load A (wrong Driver App reference)
        [Test]
        public void QR_D09_DisposalQrOfAnotherLoad_BehaviorIsRecorded() => DisposalQrOfAnotherLoad_BehaviorIsRecorded();
    }
}
