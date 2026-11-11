using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Flows;
using MobileAutomation.Tests.Pages.Driver;
using MobileAutomation.Tests.Utilities;
using OpenQA.Selenium;

namespace RecoveryTrac.AutomatedTests
{
    // Regression tests for getting a receipt on the Driver app over QR (QR-R01...R06). Where QrDisposalBaselineTests
    // records what the disposal leg does, these assert that getting a receipt keeps working as it does today: the
    // receipt and its fields, where it is listed, and what the app says when the scan is a duplicate or the wrong code.
    // Each test saves what it saw under Baselines/: QR-R01's receipt is the expected result a receipt sent over
    // Bluetooth (R2 Send Receipt) must match field for field.
    //
    // Expected texts are the Driver app's (Driver-App src/app/qr-code/receipt/receipt-qr.service.ts and
    // pages/scanner/scanner.page.ts, branch feature/10082-nearby-r1). A build with the receipt / active load
    // validation (feature/10722) checks the load before the duplicate, so QR-R03's message would change there.
    // Split between Node2 and Node3 like the QR baselines; a fixture on the same pair waits for DriverFactory's
    // node-pair lock.
    [Category("QR")]
    [Category("QR-Receipt")]
    [Parallelizable(ParallelScope.Self)]
    public abstract class QrReceiptRegressionTests : QrBaselineTestBase
    {
        protected QrReceiptRegressionTests(string node)
            : base(node)
        {
        }

        public const string ReceiptSaved = "Receipt saved successfully.";
        public const string TypeMismatch = "QR Code Type Mismatch: Please scan a receipt QR code.";
        public const string DuplicateLoadHeader = "Possible Duplicate Load";
        public const string DuplicateLoadMessage = "Receipts are already saved for this load.";

        public static string DuplicateReceipt(string receiptId) => $"Receipt {receiptId} has already been scanned.";

        // A receipt as the Driver app showed it right after the scan, with the toast that came with it and the device's
        // clock before the scan (the receipt date must fall between that and the time it was read)
        private record ShownReceipt(string Id, string RouteId, string DateText, IReadOnlyDictionary<string, string> Fields,
            string? Toast, DateTime ScanStartedAt, DateTime ReadAt);

        // What a scan at Scan Receipt Code led to when it gave no receipt: the toast, and where the app went
        private record FailedScan(string? Toast, string Route, string? ReceiptId);

        // QR-R01: the receipt from Home's Scan Receipt Code. Saved with its toast, opened on its own page with the
        // load's receipt number, today's date and the disposal ticket's values, and the load is over: Home shows No
        // Active Load and offers Scan QR Code again
        protected void ReceiptFromHome_IsSaved_AndEndsTheLoad()
        {
            var load = CollectAndScan(TestName);
            var disposal = SubmitDisposal(load.LoadQrPng, load.Ticket);

            var receipt = ScanReceipt(load.DriverApp, disposal, () => load.DriverHome.ScanReceiptCode());
            string backRoute = new DriverReceiptPage(load.DriverApp).GoBack();
            var home = GoHome(load.DriverApp, load.DriverHome, backRoute);
            string currentLoad = home.CurrentLoad;
            bool scanQrCodeShown = home.IsScanQrCodeShown(10);
            var homeActions = home.VisibleHomeActions();

            BaselineRecorder.Save("QR-R01", _node, new
            {
                collectionTicketId = load.Ticket.TicketId,
                disposalTicketId = disposal.TicketId,
                load.Load,
                receipt = new { receipt.Id, receipt.RouteId, receipt.DateText, receipt.Fields },
                receipt.Toast,
                backRoute,
                currentLoadAfterReceipt = currentLoad,
                homeActionsAfterReceipt = homeActions
            });

            Assert.Multiple(() =>
            {
                AssertReceipt(receipt, load);
                Assert.That(backRoute, Does.StartWith("/home"), $"Back from receipt {receipt.Id} did not return to Home.");
                Assert.That(home.HasActiveLoad, Is.False,
                    $"Load {load.Load} is still the current load ('{currentLoad}') after its receipt {receipt.Id} was saved.");
                Assert.That(scanQrCodeShown, Is.True,
                    $"Home does not offer Scan QR Code after receipt {receipt.Id} ended the load (it shows: {string.Join(", ", homeActions)}).");
            });
        }

        // QR-R02: the receipt on the Receipts tab after a restart. Listed for today with its number and date, the count
        // one higher than before; search by number finds it and a search with no match shows the empty state; the
        // card opens the same receipt and Back returns to the list
        protected void ReceiptsTab_ListsSearchesAndReopensTheReceipt()
        {
            var load = CollectAndScan(TestName);
            var disposal = SubmitDisposal(load.LoadQrPng, load.Ticket);

            load.DriverHome.OpenTab("receipts");
            int countBefore = new DriverReceiptsPage(load.DriverApp).ShownCount();
            load.DriverHome.OpenTab("home");

            var receipt = ScanReceipt(load.DriverApp, disposal, () => load.DriverHome.ScanReceiptCode());

            // From storage, not from the page that was just open
            var home = DriverAppFlow.Restart(load.DriverApp);
            home.OpenTab("receipts");
            var list = new DriverReceiptsPage(load.DriverApp);
            int countAfter = list.ShownCount();
            var card = list.Card(receipt.Id);

            list.Search(receipt.Id);
            var searchResults = list.CardsShown();
            string noMatch = $"RT-QA-NO-MATCH-{Guid.NewGuid():N}"[..24];
            list.Search(noMatch);
            string? emptyState = list.EmptyStateMessage();
            string today = list.TodayLabel();
            list.ClearFiltersFromEmptyState();
            int countAfterClear = list.ShownCount();

            list.Open(receipt.Id);
            var reopened = new DriverReceiptPage(load.DriverApp);
            reopened.WaitForReceipt();
            string reopenedId = reopened.Id;
            string reopenedDate = reopened.DateText;
            var reopenedFields = reopened.Fields();
            string backRoute = reopened.GoBack();

            BaselineRecorder.Save("QR-R02", _node, new
            {
                collectionTicketId = load.Ticket.TicketId,
                disposalTicketId = disposal.TicketId,
                receipt.Id,
                countBefore,
                countAfter,
                card,
                searchResults,
                noMatchSearch = noMatch,
                emptyState,
                countAfterClear,
                reopenedId,
                reopenedDate,
                backRoute
            });

            Assert.Multiple(() =>
            {
                Assert.That(countAfter, Is.EqualTo(countBefore + 1),
                    $"The Receipts tab counted {countBefore} receipt(s) for today before receipt {receipt.Id} and {countAfter} after it.");
                Assert.That(card, Is.Not.Null, $"Receipt {receipt.Id} is not listed on the Receipts tab after a restart.");
                Assert.That(card?.Date, Is.EqualTo(receipt.DateText), $"Receipt {receipt.Id}'s card shows a different date than the receipt.");
                Assert.That(searchResults.Select(c => c.Id), Does.Contain(receipt.Id), $"Searching for {receipt.Id} did not find it.");
                Assert.That(searchResults.Select(c => c.Id), Has.All.Contains(receipt.Id).IgnoreCase,
                    $"Searching for {receipt.Id} also listed receipts whose number doesn't contain it.");
                Assert.That(emptyState, Does.StartWith($"No receipts found for {today}."),
                    $"A search with no match ('{noMatch}') did not show the 'No receipts found' empty state.");
                Assert.That(emptyState, Does.Contain("clearing the search filter"),
                    "The empty state after a search doesn't suggest clearing the search filter.");
                Assert.That(countAfterClear, Is.EqualTo(countAfter), "Clear Filters did not bring back today's receipts.");
                Assert.That(reopenedId, Is.EqualTo(receipt.Id), $"The card of receipt {receipt.Id} opened receipt {reopenedId}.");
                Assert.That(reopenedDate, Is.EqualTo(receipt.DateText), $"Receipt {receipt.Id} shows a different date when reopened from the list.");
                Assert.That(reopenedFields, Is.EquivalentTo(receipt.Fields),
                    $"Receipt {receipt.Id} reopened from the list shows different fields than right after the scan.");
                Assert.That(backRoute, Does.StartWith("/receipts"), $"Back from receipt {receipt.Id} did not return to the Receipts tab.");
            });
        }

        // QR-R03: the same disposal QR scanned again, for the driver's next load. The receipt is not saved twice: the
        // app says the receipt has already been scanned, the next load stays current, and the receipt is listed once
        protected void SameReceiptScannedForTheNextLoad_IsRejectedAsDuplicate()
        {
            var first = SubmitTicket(TestName + " A");
            var second = SubmitTicket(TestName + " B", first.Flow);
            var (driverApp, driverHome) = DriverAppFlow.StartOnHome();

            string loadA = ScanCollectionQr(driverApp, driverHome, first.QrPng);
            byte[] loadAQrPng = driverHome.GetCurrentLoadQrPng();
            var disposalA = SubmitDisposal(loadAQrPng, first);
            var receiptA = ScanReceipt(driverApp, disposalA, driverHome.ScanReceiptCode);
            var home = GoHome(driverApp, driverHome, new DriverReceiptPage(driverApp).GoBack());

            string loadB = ScanCollectionQr(driverApp, home, second.QrPng);
            StageOnDriver(driverApp, disposalA.QrPng);
            home.StartRecordingToasts();
            home.ScanReceiptCode();
            var rescan = WaitForFailedScan(driverApp);
            string loadAfter = home.CurrentLoad;

            home.OpenTab("receipts");
            var list = new DriverReceiptsPage(driverApp);
            list.Search(receiptA.Id);
            int listedTimes = list.CardsShown().Count(c => c.Id == receiptA.Id);

            BaselineRecorder.Save("QR-R03", _node, new
            {
                ticketA = first.TicketId,
                ticketB = second.TicketId,
                loadA,
                loadB,
                disposalTicketA = disposalA.TicketId,
                receiptA = receiptA.Id,
                rescan,
                loadAfterRescan = loadAfter,
                receiptAListedTimes = listedTimes
            });

            Assert.Multiple(() =>
            {
                Assert.That(IsReceiptOf(receiptA.Id, loadA), Is.True, $"Receipt {receiptA.Id} does not belong to load {loadA}.");
                Assert.That(rescan.ReceiptId, Is.Null,
                    $"Scanning disposal {disposalA.TicketId} again opened receipt {rescan.ReceiptId} instead of rejecting the duplicate.");
                Assert.That(rescan.Toast, Is.EqualTo(DuplicateReceipt(receiptA.Id)),
                    $"Scanning disposal {disposalA.TicketId} again did not say receipt {receiptA.Id} was already scanned.");
                Assert.That(rescan.Route, Does.StartWith("/home"), "The app did not go back to Home after the duplicate receipt scan.");
                Assert.That(loadAfter, Is.EqualTo(loadB), $"Load {loadB} is no longer current after the duplicate receipt scan.");
                Assert.That(listedTimes, Is.EqualTo(1), $"Receipt {receiptA.Id} is listed {listedTimes} times.");
            });
        }

        // QR-R04: a collection QR at Scan Receipt Code is the wrong kind of code: the app says so, goes back to Home and
        // the load stays current, with Scan Receipt Code still offered
        protected void CollectionQrAtScanReceiptCode_IsRejectedAsWrongType()
        {
            var load = CollectAndScan(TestName);

            StageOnDriver(load.DriverApp, load.Ticket.QrPng);
            load.DriverHome.StartRecordingToasts();
            load.DriverHome.ScanReceiptCode();
            var scan = WaitForFailedScan(load.DriverApp);
            string loadAfter = load.DriverHome.CurrentLoad;
            var homeActions = load.DriverHome.VisibleHomeActions();

            BaselineRecorder.Save("QR-R04", _node, new
            {
                collectionTicketId = load.Ticket.TicketId,
                load.Load,
                scan,
                loadAfterScan = loadAfter,
                homeActionsAfterScan = homeActions
            });

            Assert.Multiple(() =>
            {
                Assert.That(scan.ReceiptId, Is.Null, $"The collection QR of ticket {load.Ticket.TicketId} gave receipt {scan.ReceiptId}.");
                Assert.That(scan.Toast, Is.EqualTo(TypeMismatch), "The app did not say a collection QR is the wrong kind of code for a receipt.");
                Assert.That(scan.Route, Does.StartWith("/home"), "The app did not go back to Home after the wrong code.");
                Assert.That(loadAfter, Is.EqualTo(load.Load), $"Load {load.Load} is no longer current after scanning the wrong code.");
                Assert.That(homeActions, Has.Some.EqualTo("Scan Receipt Code").IgnoreCase,
                    $"Home no longer offers Scan Receipt Code after the wrong code (it shows: {string.Join(", ", homeActions)}).");
            });
        }

        // QR-R05: the collection QR of a load that already has its receipt, scanned again with Scan QR Code. The app warns
        // it is a possible duplicate load and doesn't make it the current load again
        protected void CollectionQrAfterItsReceipt_IsFlaggedAsDuplicateLoad()
        {
            var load = CollectAndScan(TestName);
            var disposal = SubmitDisposal(load.LoadQrPng, load.Ticket);
            var receipt = ScanReceipt(load.DriverApp, disposal, () => load.DriverHome.ScanReceiptCode());
            var home = GoHome(load.DriverApp, load.DriverHome, new DriverReceiptPage(load.DriverApp).GoBack());

            StageOnDriver(load.DriverApp, load.Ticket.QrPng);
            home.ScanQrCode();
            var alert = home.CaptureAndDismissAlert(30);
            // The scanner goes back to Home once the alert is closed
            Thread.Sleep(2000);
            string loadAfter = home.CurrentLoad;
            bool hasLoadAfter = home.HasActiveLoad;

            BaselineRecorder.Save("QR-R05", _node, new
            {
                collectionTicketId = load.Ticket.TicketId,
                load.Load,
                receipt.Id,
                alert,
                loadAfterRescan = loadAfter
            });

            Assert.Multiple(() =>
            {
                Assert.That(IsReceiptOf(receipt.Id, load.Load), Is.True, $"Receipt {receipt.Id} does not belong to load {load.Load}.");
                Assert.That(alert?.Header, Is.EqualTo(DuplicateLoadHeader),
                    $"Scanning ticket {load.Ticket.TicketId}'s collection QR after its receipt did not warn of a possible duplicate load.");
                Assert.That(alert?.Message, Does.Contain(DuplicateLoadMessage),
                    "The possible duplicate load alert doesn't say the load already has receipts.");
                Assert.That(hasLoadAfter, Is.False, $"Load {load.Load} became the current load again ('{loadAfter}') after its receipt.");
            });
        }

        // QR-R06: the receipt from the Receipts tab's scan icon while a load is active: the same receipt as from Home
        protected void ReceiptFromReceiptsTab_MatchesReceiptFromHome()
        {
            var load = CollectAndScan(TestName);
            var disposal = SubmitDisposal(load.LoadQrPng, load.Ticket);

            load.DriverHome.OpenTab("receipts");
            var list = new DriverReceiptsPage(load.DriverApp);
            list.WaitUntilLoaded();
            var receipt = ScanReceipt(load.DriverApp, disposal, list.ScanFromHeader);
            string backRoute = new DriverReceiptPage(load.DriverApp).GoBack();

            BaselineRecorder.Save("QR-R06", _node, new
            {
                collectionTicketId = load.Ticket.TicketId,
                disposalTicketId = disposal.TicketId,
                load.Load,
                receipt = new { receipt.Id, receipt.RouteId, receipt.DateText, receipt.Fields },
                receipt.Toast,
                backRoute
            });

            Assert.Multiple(() => AssertReceipt(receipt, load));
        }

        // Stages the disposal QR, records toasts, scans with the given action (Home's Scan Receipt Code or the Receipts
        // tab's icon) and reads the receipt the app opens
        private static ShownReceipt ScanReceipt(IWebDriver driverApp, SubmittedDisposal disposal, Action scan)
        {
            var page = new DriverReceiptPage(driverApp);
            StageOnDriver(driverApp, disposal.QrPng);
            IonicToasts.StartRecording(driverApp);
            DateTime startedAt = page.DeviceNow();
            scan();
            page.WaitForReceipt();

            var shown = new ShownReceipt(page.Id, page.RouteId, page.DateText, page.Fields(),
                IonicToasts.Message(driverApp, timeoutSeconds: 10), startedAt, page.DeviceNow());
            Console.WriteLine($"🧾 Driver app receipt {shown.Id} ({shown.DateText}), {shown.Fields.Count} fields, toast '{shown.Toast ?? "none"}'.");
            return shown;
        }

        // What must hold for every receipt of a load: shown under its own number, today, with the disposal ticket's values
        // and the success toast. Call inside Assert.Multiple
        private static void AssertReceipt(ShownReceipt receipt, LoadOnDriver load)
        {
            var truck = load.Ticket.Draft.Truck;
            string Field(string label) => receipt.Fields.GetValueOrDefault(label) ?? "(no such field)";
            DateTime? date = DriverReceiptPage.ParseDate(receipt.DateText);
            var earliest = receipt.ScanStartedAt.AddSeconds(-receipt.ScanStartedAt.Second).AddMinutes(-1);
            var latest = receipt.ReadAt.AddMinutes(1);

            Assert.That(receipt.Toast, Is.EqualTo(ReceiptSaved), $"Receipt {receipt.Id} was opened without the '{ReceiptSaved}' toast.");
            Assert.That(IsReceiptOf(receipt.Id, load.Load), Is.True, $"Receipt {receipt.Id} does not belong to load {load.Load}.");
            Assert.That(receipt.RouteId, Is.EqualTo(receipt.Id), $"Receipt {receipt.Id} was opened under another number ({receipt.RouteId}).");
            // Receipts are numbered after the collection ticket, e.g. RQTE-LC-100N > RQTE-LC-100N.A
            Assert.That(Field("Receipt Identifier"), Does.StartWith(load.Ticket.TicketId.TrimStart('#')),
                $"Receipt {receipt.Id}'s Receipt Identifier does not belong to collection ticket {load.Ticket.TicketId}.");
            Assert.That(Field("Truck Number"), Is.EqualTo(truck.TruckNumber), $"Receipt {receipt.Id} shows an unexpected Truck Number.");
            Assert.That(Field("Debris Type"), Is.EqualTo("Vegetative/Woody").IgnoreCase, $"Receipt {receipt.Id} shows an unexpected Debris Type.");
            Assert.That(Field("Capacity"), Is.EqualTo(truck.TruckCapacity), $"Receipt {receipt.Id} shows an unexpected Capacity.");
            Assert.That(Field("Load Call"), Is.EqualTo(LoadDisposalFlow.LoadCall), $"Receipt {receipt.Id} shows an unexpected Load Call.");
            Assert.That(date, Is.Not.Null, $"Receipt {receipt.Id}'s date '{receipt.DateText}' is not in the format {DriverReceiptPage.DateFormat}.");
            Assert.That(date, Is.InRange(earliest, latest),
                $"Receipt {receipt.Id} is dated {receipt.DateText}, not when it was scanned (device clock {receipt.ScanStartedAt:MM/dd/yyyy hh:mm tt}).");
        }

        // After a scan that gives no receipt the scanner shows a toast and goes back to Home; returns what it showed
        private static FailedScan WaitForFailedScan(IWebDriver driverApp, int timeoutSeconds = 30)
        {
            var receiptPage = new DriverReceiptPage(driverApp);
            var deadline = DateTime.Now.AddSeconds(timeoutSeconds);
            string? toast = null;
            while (DateTime.Now < deadline)
            {
                if (receiptPage.IsShown) break;
                toast ??= IonicToasts.Message(driverApp, timeoutSeconds: 1);
                if (toast != null && new Uri(driverApp.Url).AbsolutePath.StartsWith("/home", StringComparison.OrdinalIgnoreCase)) break;
            }
            string? receiptId = receiptPage.IsShown ? receiptPage.Id : null;
            var scan = new FailedScan(toast, new Uri(driverApp.Url).AbsolutePath, receiptId);
            Console.WriteLine($"🔔 Driver app after the scan: toast '{scan.Toast ?? "none"}', route {scan.Route}, receipt {scan.ReceiptId ?? "none"}.");
            return scan;
        }

        // Home after leaving a receipt: Back normally lands there; otherwise the Home tab is opened
        private static DriverHomePage GoHome(IWebDriver driverApp, DriverHomePage home, string currentRoute)
        {
            if (!currentRoute.StartsWith("/home", StringComparison.OrdinalIgnoreCase)) home.OpenTab("home");
            return new DriverHomePage(driverApp);
        }
    }

    // Each test runs one collection and one disposal (~6 min); QR-R03 two collections, QR-R04 only a collection
    [TestFixture("Node2")]
    public class QrReceiptRegressionNode2Tests(string node) : QrReceiptRegressionTests(node)
    {
        // QR-R01: the receipt from Home, its fields, and the load it ends
        [Test]
        public void QR_R01_ReceiptFromHome_IsSaved_AndEndsTheLoad() => ReceiptFromHome_IsSaved_AndEndsTheLoad();

        // QR-R03: the same disposal QR again for the next load is a duplicate receipt
        [Test]
        public void QR_R03_SameReceiptScannedForTheNextLoad_IsRejectedAsDuplicate() => SameReceiptScannedForTheNextLoad_IsRejectedAsDuplicate();

        // QR-R04: a collection QR at Scan Receipt Code is the wrong kind of code
        [Test]
        public void QR_R04_CollectionQrAtScanReceiptCode_IsRejectedAsWrongType() => CollectionQrAtScanReceiptCode_IsRejectedAsWrongType();
    }

    [TestFixture("Node3")]
    public class QrReceiptRegressionNode3Tests(string node) : QrReceiptRegressionTests(node)
    {
        // QR-R02: the receipt on the Receipts tab after a restart: listed, searched, reopened
        [Test]
        public void QR_R02_ReceiptsTab_ListsSearchesAndReopensTheReceipt() => ReceiptsTab_ListsSearchesAndReopensTheReceipt();

        // QR-R05: a collection QR whose load already has its receipt is flagged as a possible duplicate load
        [Test]
        public void QR_R05_CollectionQrAfterItsReceipt_IsFlaggedAsDuplicateLoad() => CollectionQrAfterItsReceipt_IsFlaggedAsDuplicateLoad();

        // QR-R06: the receipt from the Receipts tab's scan icon matches the one from Home
        [Test]
        public void QR_R06_ReceiptFromReceiptsTab_MatchesReceiptFromHome() => ReceiptFromReceiptsTab_MatchesReceiptFromHome();
    }
}
