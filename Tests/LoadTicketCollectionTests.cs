using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Flows;
using MobileAutomation.Tests.Tests;
using MobileAutomation.Tests.Pages;
using MobileAutomation.Tests.Pages.Driver;
using MobileAutomation.Tests.Utilities;

namespace RecoveryTrac.AutomatedTests
{
    [TestFixture]
    [Category("QR")]
    [Parallelizable(ParallelScope.Children)]
    public class LoadTicketCollectionTests : DualAppTestBase
    {
        private const string DisposalFormName = "Load - Disposal";

        // Same E2E flow on every node pair at once; Node2/Node3 log in with their own Flex accounts (TestSettings:Node<N>)
        [TestCase("Node1", TestName = "Test_LoadDisposalCollectionFlow_And_VerifySubmittedSynced_E2E_Node1_A14_A12")]
        [TestCase("Node2", TestName = "Test_LoadDisposalCollectionFlow_And_VerifySubmittedSynced_E2E_Node2_A15_A13")]
        [TestCase("Node3", TestName = "Test_LoadDisposalCollectionFlow_And_VerifySubmittedSynced_E2E_Node3_A17_A16")]
        public void Test_LoadDisposalCollectionFlow_And_VerifySubmittedSynced_E2E(string node)
        {
            var driver = DriverFactory.DriverFlex;
            var flow = new CollectionLoadTicketFlow(driver, _wait.Value!, node);
            var formsPage = flow.Forms;
            var clockInPage = flow.ClockIn;
            string qrDirectory = flow.QrDirectory;

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
            byte[] driverQrPng = driverHome.GetCurrentLoadQrPng();
            string driverQrPath = Path.Combine(qrDirectory, $"DriverCurrentLoad_{node}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(driverQrPath, driverQrPng);
            TestContext.AddTestAttachment(driverQrPath, "Driver app current load QR code");
            QrScanFeed.Stage(driver, driverQrPng, rasterizer: driver, DriverFactory.FlexCameraFeedPath);

            // -------------------------------------------------------------------------
            // STEP 23: Back on Flex - All Forms > Load - Disposal > New
            // -------------------------------------------------------------------------
            var disposalForm = new LoadDisposalFormPage(driver);
            formsPage.OpenAllForms();
            formsPage.OpenForm(DisposalFormName);
            var disposalTicketsBefore = formsPage.GetSubmittedTicketIds();
            formsPage.ClickNew();
            if (clockInPage.ClockInIfPrompted(workingOn: "testing", trackMileage: false))
            {
                formsPage.ClickNew();
            }
            Assert.That(disposalForm.IsOpen(), Is.True, $"The '{DisposalFormName}' form did not open after clicking New.");

            // -------------------------------------------------------------------------
            // STEP 24: Import Collection Data - scan the driver's QR code, pick a disposal site, Next
            // -------------------------------------------------------------------------
            disposalForm.WaitForStep("Import Collection Data");
            disposalForm.ScanDriversQrCode();
            string disposalSite = disposalForm.SelectFirstDisposalSite();

            // The step also asks why the QR could not be scanned, so NEXT only enables once the scan imported the load
            Assert.That(disposalForm.IsNextEnabled(timeoutSeconds: 20), Is.True,
                $"NEXT stayed disabled after scanning the driver's QR code and selecting '{disposalSite}'. The collection data was probably not imported.");
            Assert.That(disposalForm.GetFieldValue("Truck Number"), Is.EqualTo(truck.TruckNumber),
                "The Truck Number imported from the driver's QR code does not match the collection ticket's truck.");
            disposalForm.ClickNext();

            // -------------------------------------------------------------------------
            // STEP 25: Truck Container Load Call - confirm imported data, 50% load call, photo, Submit
            // -------------------------------------------------------------------------
            disposalForm.WaitForStep("Truck Container Load Call");
            disposalForm.SelectRadio("Is the above capacity correct?", "Yes");
            disposalForm.SelectRadio("Is the above debris class correct?", "Yes");
            disposalForm.SelectPickerValue("Load Call", "50");
            disposalForm.AddPhoto("Load Call Photo",Path.Combine(AppContext.BaseDirectory, "TestData", "DebrisPile.png"));
            disposalForm.Submit();

            // -------------------------------------------------------------------------
            // STEP 26: Disposal ticket is Synced; generate its QR code and store it for the driver's receipt scan
            // -------------------------------------------------------------------------
            // Some form versions prompt for the QR right after submit (like Load - Collection); otherwise it is
            // opened from the submitted card once the ticket has synced
            bool qrPromptedOnSubmit = disposalForm.IsGenerateQrPromptShown();
            byte[]? disposalQrPng = null;
            if (qrPromptedOnSubmit)
            {
                disposalForm.ProceedWithQrGeneration();
                disposalQrPng = disposalForm.GetQrCodePng();
                disposalForm.CloseQrModal();
            }

            var newDisposalTickets = formsPage.GetSubmittedTicketIds().Except(disposalTicketsBefore).ToList();
            Assert.That(newDisposalTickets, Has.Count.EqualTo(1),
                $"Expected exactly one new submitted disposal ticket, found: [{string.Join(", ", newDisposalTickets)}].");

            string disposalTicketId = newDisposalTickets[0];
            Console.WriteLine($"🎫 New disposal ticket submitted: {disposalTicketId}");
            Assert.That(formsPage.WaitUntilSynced(disposalTicketId), Is.True,
                $"Submitted disposal ticket {disposalTicketId} did not reach the 'Synced' status.");

            var disposalCard = formsPage.GetSubmittedCardValues(disposalTicketId);
            Assert.Multiple(() =>
            {
                Assert.That(disposalCard.GetValueOrDefault("Collection Document Number"), Is.EqualTo(ticketId.TrimStart('#')),
                    $"Disposal ticket {disposalTicketId} is not linked to collection ticket {ticketId}.");
                Assert.That(disposalCard.GetValueOrDefault("Truck Number"), Is.EqualTo(truck.TruckNumber),
                    $"Disposal ticket {disposalTicketId} shows an unexpected Truck Number.");
            });

            if (!qrPromptedOnSubmit)
            {
                formsPage.OpenSubmittedCardQr(disposalTicketId);
                disposalQrPng = disposalForm.GetQrCodePng();
                disposalForm.CloseQrModal();
            }
            Assert.That(disposalQrPng, Is.Not.Null.And.Not.Empty, "The disposal ticket QR code image is empty.");

            string disposalQrPath = Path.Combine(qrDirectory, $"LoadDisposal_{node}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(disposalQrPath, disposalQrPng!);
            TestContext.AddTestAttachment(disposalQrPath, "Generated Load - Disposal QR code");
            Console.WriteLine($"🔳 Disposal QR code stored at: {disposalQrPath}");

            // -------------------------------------------------------------------------
            // STEP 27: Driver app scans the disposal QR code and receives the receipt
            // -------------------------------------------------------------------------
            QrScanFeed.Stage(driverApp, disposalQrPng!, rasterizer: driver, DriverFactory.DriverCameraFeedPath);
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
                Assert.That(receipt.GetValue("Load Call"), Is.EqualTo("50"),
                    $"Receipt {receiptId} shows an unexpected Load Call.");
            });
        }
    }
}
