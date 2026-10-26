using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Tests;
using MobileAutomation.Tests.Pages;
using MobileAutomation.Tests.Pages.Driver;
using OpenQA.Selenium.Support.UI;
using MobileAutomation.Tests.Utilities;

namespace RecoveryTrac.AutomatedTests
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class LoadTicketCollectionTests : BaseTest
    {
        private const string FormName = "Load - Collection";
        private const string DisposalFormName = "Load - Disposal";

        // ThreadLocal keeps explicit wait instances fully isolated on a per-thread basis
        private readonly ThreadLocal<WebDriverWait> _wait = new();

        [SetUp]
        public void SetupLoadTicketScenario()
        {
            // Each test case runs on the node pair it names (Grid_<node>_Flex + Grid_<node>_Driver)
            DriverFactory.InitializeDualApps((string)TestContext.CurrentContext.Test.Arguments[0]!);
            int timeoutSeconds = int.Parse(TestConfiguration.Configuration["TestSettings:ExplicitWaitTimeoutSeconds"] ?? "15");
            _wait.Value = new WebDriverWait(DriverFactory.DriverFlex, TimeSpan.FromSeconds(timeoutSeconds));

            if (DriverFactory.DriverFlex is OpenQA.Selenium.Appium.Android.AndroidDriver androidFlex)
            {
                Console.WriteLine("📱 Mobile Mode: Detecting and dismissing native Android permission popups...");
                PermissionHelper.HandleSystemPermissions(androidFlex);
            }

            // Mobile stays in NATIVE_APP context here: Login/PrivacyPolicy/HomePage use native Android locators.
            // The test switches to the WEBVIEW context right before the Forms flow.
        }

        // Runs before BaseTest.Cleanup() quits the drivers, so the failing screen can still be captured
        [TearDown]
        public void CaptureFailureEvidence()
        {
            if (TestContext.CurrentContext.Result.Outcome.Status != NUnit.Framework.Interfaces.TestStatus.Failed) return;

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            SaveEvidence(DriverFactory.DriverFlex, "Flex", timestamp);
            // Only once the test has started it
            if (DriverFactory.DriverDriver != null) SaveEvidence(DriverFactory.DriverDriver, "DriverApp", timestamp);
        }

        private static void SaveEvidence(OpenQA.Selenium.IWebDriver driver, string app, string timestamp)
        {
            try
            {
                string evidencePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"Failure_{TestContext.CurrentContext.Test.Name}_{timestamp}_{app}");
                ((OpenQA.Selenium.ITakesScreenshot)driver).GetScreenshot().SaveAsFile(evidencePath + ".png");
                TestContext.AddTestAttachment(evidencePath + ".png", $"{app} screen at failure");

                // Url and <body> only exist in a browser/WebView context; the native Android context still gets the
                // screenshot and page source
                try
                {
                    File.WriteAllText(evidencePath + ".txt", MaskPasswords($"URL: {driver.Url}{Environment.NewLine}{driver.FindElement(OpenQA.Selenium.By.TagName("body")).Text}"));
                    TestContext.AddTestAttachment(evidencePath + ".txt", $"{app} page text at failure");
                }
                catch (OpenQA.Selenium.WebDriverException)
                {
                }

                // DOM at failure, so changed form controls can be re-located without re-running the flow
                File.WriteAllText(evidencePath + ".html", MaskPasswords(driver.PageSource));
                TestContext.AddTestAttachment(evidencePath + ".html", $"{app} page source at failure");
                Console.WriteLine($"📸 {app} failure evidence saved to: {evidencePath}.png/.txt/.html");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not capture {app} failure evidence: {ex.Message}");
            }
        }

        // Evidence files get attached to test results, so never write the login password into them. Android's page
        // source exposes a password field's value as its text (password="true"); the literal replace also covers any
        // copy of it in the Web DOM (e.g. an ng-reflect-model attribute) or the page text
        private static string MaskPasswords(string content)
        {
            content = System.Text.RegularExpressions.Regex.Replace(content,
                @"(<[^>]*\bpassword=""true""[^>]*\btext="")[^""]*("")|(<[^>]*\btext="")[^""]*(""[^>]*\bpassword=""true"")",
                m => m.Groups[1].Success ? $"{m.Groups[1].Value}********{m.Groups[2].Value}" : $"{m.Groups[3].Value}********{m.Groups[4].Value}");

            string password = DriverFactory.LoginPassword;
            return string.IsNullOrEmpty(password) ? content : content.Replace(password, "********");
        }

        [OneTimeTearDown]
        public void FinalizeClassDisposal()
        {
            _wait.Dispose();
        }

        // Same E2E flow on every node pair at once; Node2/Node3 log in with their own Flex accounts (TestSettings:Node<N>)
        [TestCase("Node1", TestName = "Test_LoadDisposalCollectionFlow_And_VerifySubmittedSynced_E2E_Node1_A14_A12")]
        [TestCase("Node2", TestName = "Test_LoadDisposalCollectionFlow_And_VerifySubmittedSynced_E2E_Node2_A15_A13")]
        [TestCase("Node3", TestName = "Test_LoadDisposalCollectionFlow_And_VerifySubmittedSynced_E2E_Node3_A17_A16")]
        public void Test_LoadDisposalCollectionFlow_And_VerifySubmittedSynced_E2E(string node)
        {
            var driver = DriverFactory.DriverFlex;
            var wf = _wait.Value!;

            var loginPage = new LoginPage(driver);
            var privacyPolicyPage = new PrivacyPolicyPage(driver);
            var homePage = new HomePage(driver);
            var formsPage = new FormsPage(driver);
            var clockInPage = new ClockInPage(driver);
            var ticketForm = new CollectionLoadTicketFormPage(driver);

            // -------------------------------------------------------------------------
            // STEP 1: Log in (credentials from user-secrets) and clear onboarding screens
            // -------------------------------------------------------------------------
            loginPage.LoginIfNeeded(DriverFactory.LoginEmail, DriverFactory.LoginPassword);

            if (driver is OpenQA.Selenium.Appium.Android.AndroidDriver androidDriver)
            {
                PermissionHelper.HandleSystemPermissions(androidDriver);
            }

            privacyPolicyPage.ClearAllPendingAgreements();
            homePage.CompleteProjectAssignment(wf);

            // Forms page objects use DOM locators; on mobile they run inside the app's WebView (no-op on web)
            if (driver is OpenQA.Selenium.Appium.Android.AndroidDriver)
            {
                DriverFactory.SwitchToWebViewContext(driver);
                Assert.That(((OpenQA.Selenium.Appium.Android.AndroidDriver)driver).Context, Does.StartWith("WEBVIEW"),
                    "Could not switch to the Flex WEBVIEW context. Is Appium running with --allow-insecure=uiautomator2:chromedriver_autodownload?");
            }

            // -------------------------------------------------------------------------
            // STEPS 2-3: Forms > Collection Load Ticket, snapshot already-submitted tickets
            // -------------------------------------------------------------------------
            formsPage.OpenFormsMenu();
            formsPage.OpenForm(FormName);
            var ticketsBefore = formsPage.GetSubmittedTicketIds();

            // -------------------------------------------------------------------------
            // STEPS 4-7: New form, clocking in first when the app requires it
            // -------------------------------------------------------------------------
            formsPage.ClickNew();
            if (clockInPage.ClockInIfPrompted(workingOn: "testing", trackMileage: false))
            {
                formsPage.ClickNew();
            }
            Assert.That(ticketForm.IsOpen(), Is.True, $"The '{FormName}' form did not open after clicking New.");

            // -------------------------------------------------------------------------
            // STEPS 8-10: Truck step - pick the first truck and verify the auto-filled data
            // -------------------------------------------------------------------------
            ticketForm.WaitForStep("Truck");
            var truck = ticketForm.SelectFirstTruck();

            Assert.Multiple(() =>
            {
                Assert.That(ticketForm.GetFieldValue("Truck Number"), Is.EqualTo(truck.TruckNumber),
                    "Truck Number was not auto-filled from the selected truck.");
                Assert.That(truck.TruckCapacity, Is.Not.Empty, "Truck Capacity was not auto-filled from the selected truck.");
                Assert.That(truck.VehicleTag, Is.Not.Empty, "Truck License Plate was not auto-filled from the selected truck.");
                Assert.That(ticketForm.GetFieldValue("Truck Capacity"), Is.EqualTo(truck.TruckCapacity),
                    "Truck Capacity was not auto-filled from the selected truck.");
                Assert.That(ticketForm.GetFieldValue("Truck License Plate"), Is.EqualTo(truck.VehicleTag),
                    "Truck License Plate was not auto-filled from the selected truck's Vehicle Tag.");
                Assert.That(ticketForm.GetFieldValue("Driver Name"), Is.EqualTo(truck.DriverName),
                    "Driver Name was not auto-filled from the selected truck.");
            });

            ticketForm.SelectRadio("Is Driver Name Correct?", "Yes");
            ticketForm.SelectRadio("Is the above information correct?", "Yes");
            if (ticketForm.HasField("New Shared Control"))
            {
                ticketForm.EnterFieldValue("New Shared Control", "1");
            }
            ticketForm.ClickNext();

            // -------------------------------------------------------------------------
            // STEP 11: Trailer step
            // -------------------------------------------------------------------------
            ticketForm.WaitForStep("Trailer");
            ticketForm.SelectRadio("Does the truck have a trailer attached?", "No");
            ticketForm.ClickNext();

            // -------------------------------------------------------------------------
            // STEP 11b: Collection Start step - the debris pile photo is required
            // -------------------------------------------------------------------------
            ticketForm.WaitForStep("Collection Start");
            ticketForm.AddPhoto("Photo of First Debris Pile", Path.Combine(AppContext.BaseDirectory, "TestData", "DebrisPile.png"));
            ticketForm.ClickNext();

            // -------------------------------------------------------------------------
            // STEP 12: Collection Information step
            // -------------------------------------------------------------------------
            ticketForm.WaitForStep("Collection Information");
            Assert.That(ticketForm.GetFieldValue("Truck Number"), Is.EqualTo(truck.TruckNumber),
                "Truck Number on the Collection Information step does not match the selected truck.");
            ticketForm.SelectChoiceOption("Truck Debris Type", "Vegetative/Woody");
            ticketForm.ClickNext();

            // -------------------------------------------------------------------------
            // STEPS 13-15: Location Information step - GPS with map fallback
            // -------------------------------------------------------------------------
            ticketForm.WaitForStep("Location Information");
            ticketForm.MockGpsLocation();
            var (latitude, longitude) = ticketForm.CaptureLocation();
            string streetName = ticketForm.GetFieldValue("Street Name");

            Assert.Multiple(() =>
            {
                Assert.That(latitude, Is.Not.Empty.And.Not.EqualTo("0"), "Latitude was not captured.");
                Assert.That(longitude, Is.Not.Empty.And.Not.EqualTo("0"), "Longitude was not captured.");
                Assert.That(ticketForm.GetFieldValue("Address Number"), Is.Not.Empty, "Address Number was not populated from the location.");
                Assert.That(ticketForm.GetFieldValue("Street Name"), Is.Not.Empty, "Street Name was not populated from the location.");
            });

            ticketForm.SelectRadio("Is the above address info correct?", "Yes");

            // -------------------------------------------------------------------------
            // STEPS 16-18: Review & Submit, then confirm QR generation
            // -------------------------------------------------------------------------
            ticketForm.ClickNext();
            ticketForm.WaitForStep("Review & Submit");
            ticketForm.EnterNotes($"Automated test - {nameof(Test_LoadDisposalCollectionFlow_And_VerifySubmittedSynced_E2E)} {node} {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            ticketForm.Submit();
            ticketForm.ProceedWithQrGeneration();

            // -------------------------------------------------------------------------
            // STEP 19: Store the generated QR code as a test attachment
            // -------------------------------------------------------------------------
            byte[] qrPng = ticketForm.GetQrCodePng();
            Assert.That(qrPng, Is.Not.Empty, "The generated QR code image is empty.");

            string qrDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "QrCodes");
            Directory.CreateDirectory(qrDirectory);
            string qrPath = Path.Combine(qrDirectory, $"CollectionLoadTicket_{node}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(qrPath, qrPng);
            TestContext.AddTestAttachment(qrPath, "Generated Collection Load Ticket QR code");
            Console.WriteLine($"🔳 QR code stored at: {qrPath}");

            ticketForm.CloseQrModal();

            // -------------------------------------------------------------------------
            // STEP 20: The new ticket is listed under Submitted with matching data and Synced
            // -------------------------------------------------------------------------
            var newTickets = formsPage.GetSubmittedTicketIds().Except(ticketsBefore).ToList();
            Assert.That(newTickets, Has.Count.EqualTo(1),
                $"Expected exactly one new submitted ticket, found: [{string.Join(", ", newTickets)}].");

            string ticketId = newTickets[0];
            Console.WriteLine($"🎫 New ticket submitted: {ticketId}");
            bool isSynced = formsPage.WaitUntilSynced(ticketId);
            var card = formsPage.GetSubmittedCardValues(ticketId);

            Assert.Multiple(() =>
            {
                // The 'Load - Collection' card lists Truck Number / Trailer Number / Street Name
                Assert.That(card.GetValueOrDefault("Truck Number"), Is.EqualTo(truck.TruckNumber),
                    $"Submitted ticket {ticketId} shows an unexpected Truck Number.");
                Assert.That(card.GetValueOrDefault("Trailer Number"), Is.EqualTo("---"),
                    $"Submitted ticket {ticketId} should show no trailer, as 'No' was answered on the Trailer step.");
                Assert.That(card.GetValueOrDefault("Street Name"), Is.EqualTo(streetName),
                    $"Submitted ticket {ticketId} shows an unexpected Street Name.");
                Assert.That(isSynced, Is.True, $"Submitted ticket {ticketId} did not reach the 'Synced' status.");
            });

            // -------------------------------------------------------------------------
            // STEP 21: Start the Driver app (second browser / emulator) and clear its onboarding
            // -------------------------------------------------------------------------
            var driverApp = DriverFactory.StartDriverApp();
            if (driverApp is OpenQA.Selenium.Appium.Android.AndroidDriver androidDriverApp)
            {
                PermissionHelper.HandleSystemPermissions(androidDriverApp);
            }

            var driverOnboarding = new DriverOnboardingPage(driverApp);
            driverOnboarding.CompleteOnboardingIfPresent();

            Assert.That(driverOnboarding.IsHomeDisplayed(), Is.True,
                $"The Driver app did not reach Home after GET STARTED > SKIP ({driverOnboarding.DescribeLocation()}).");

            // -------------------------------------------------------------------------
            // STEP 22: Driver app scans the collection QR code generated in step 19
            // -------------------------------------------------------------------------
            // Onboarding runs on native screens; the Driver pages below read the app's DOM, so move into its WebView
            if (driverApp is OpenQA.Selenium.Appium.Android.AndroidDriver)
            {
                DriverFactory.SwitchToWebViewContext(driverApp);
            }

            var driverHome = new DriverHomePage(driverApp);
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
