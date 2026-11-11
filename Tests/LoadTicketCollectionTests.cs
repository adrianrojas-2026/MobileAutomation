using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Flows;
using MobileAutomation.Tests.Tests;
using MobileAutomation.Tests.Pages.Driver;
using MobileAutomation.Tests.Utilities;

namespace RecoveryTrac.AutomatedTests
{
    [TestFixture]
    [Category("QR")]
    [Parallelizable(ParallelScope.Children)]
    public class LoadTicketCollectionTests : DualAppTestBase
    {
        // Same E2E flow on every node pair at once; Node2/Node3 log in with their own Flex accounts (TestSettings:Node<N>)
        [TestCase("Node1", TestName = "Test_LoadDisposalCollectionFlow_And_VerifySubmittedSynced_E2E_Node1_A14_A12")]
        [TestCase("Node2", TestName = "Test_LoadDisposalCollectionFlow_And_VerifySubmittedSynced_E2E_Node2_A15_A13")]
        [TestCase("Node3", TestName = "Test_LoadDisposalCollectionFlow_And_VerifySubmittedSynced_E2E_Node3_A17_A16")]
        public void Test_LoadDisposalCollectionFlow_And_VerifySubmittedSynced_E2E(string node)
        {
            var driver = DriverFactory.DriverFlex;
            var flow = new CollectionLoadTicketFlow(driver, _wait.Value!, node);

            // STEPS 1-17: log in, then fill and submit the Load - Collection ticket
            flow.LoginAndEnterWebView();
            var draft = flow.FillAndSubmitTicket(
                $"Automated test - {nameof(Test_LoadDisposalCollectionFlow_And_VerifySubmittedSynced_E2E)} {node} {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            var truck = draft.Truck;

            // STEPS 18-19: confirm QR generation and store the QR code
            byte[] qrPng = flow.GenerateAndStoreQr();

            // STEP 20: the new ticket is listed under Submitted with matching data and Synced
            string ticketId = flow.VerifyTicketSubmittedAndSynced(draft);

            // STEP 21: Driver app on Home, in its WebView
            var (driverApp, driverHome) = DriverAppFlow.StartOnHome();

            // -------------------------------------------------------------------------
            // STEP 22: Driver app scans the collection QR code generated in step 19
            // -------------------------------------------------------------------------
            QrScanFeed.Stage(driverApp, qrPng, rasterizer: driver, DriverFactory.DriverCameraFeedPath);
            driverHome.ScanQrCode();
            string driverLoad = driverHome.WaitForActiveLoad();
            Console.WriteLine($"🚚 Driver app current load: {driverLoad}");

            // The driver's load QR is what the disposal site scans; stage it on the Flex camera
            var disposal = new LoadDisposalFlow(driver, node);
            disposal.StoreAndStageDriversQr(driverHome.GetCurrentLoadQrPng());

            // STEP 23: Back on Flex - All Forms > Load - Disposal > New
            var disposalTicketsBefore = disposal.OpenNewForm();

            // STEP 24: Import Collection Data - scan the driver's QR code, pick a disposal site, Next
            string disposalSite = disposal.ScanDriversQrAndSelectSite();
            disposal.AssertImportedAndContinue(disposalSite, truck.TruckNumber);

            // STEP 25: Truck Container Load Call - confirm imported data, 50% load call, photo, Submit
            disposal.CompleteLoadCallAndSubmit();

            // STEP 26: Disposal ticket is Synced and linked; its QR code is stored for the driver's receipt scan
            var disposalTicket = disposal.VerifySubmittedAndGetQr(disposalTicketsBefore, ticketId, truck.TruckNumber);

            // -------------------------------------------------------------------------
            // STEP 27: Driver app scans the disposal QR code and receives the receipt
            // -------------------------------------------------------------------------
            QrScanFeed.Stage(driverApp, disposalTicket.QrPng, rasterizer: driver, DriverFactory.DriverCameraFeedPath);
            driverHome.ScanReceiptCode();

            var receipt = new DriverReceiptPage(driverApp);
            string receiptId = receipt.WaitForReceipt();
            Console.WriteLine($"🧾 Driver app received receipt: {receiptId}");

            Assert.Multiple(() =>
            {
                // Receipts are numbered after the collection ticket, e.g. RQTE-LC-100N > RQTE-LC-100N.A
                Assert.That(receiptId, Does.StartWith(ticketId.TrimStart('#')),
                    $"Receipt {receiptId} does not belong to collection ticket {ticketId}.");
                Assert.That(receipt.GetValue("Truck Number"), Is.EqualTo(truck.TruckNumber),
                    $"Receipt {receiptId} shows an unexpected Truck Number.");
                Assert.That(receipt.GetValue("Debris Type"), Is.EqualTo("Vegetative/Woody").IgnoreCase,
                    $"Receipt {receiptId} shows an unexpected Debris Type.");
                Assert.That(receipt.GetValue("Capacity"), Is.EqualTo(truck.TruckCapacity),
                    $"Receipt {receiptId} shows an unexpected Capacity.");
                Assert.That(receipt.GetValue("Load Call"), Is.EqualTo(LoadDisposalFlow.LoadCall),
                    $"Receipt {receiptId} shows an unexpected Load Call.");
            });
        }
    }
}
