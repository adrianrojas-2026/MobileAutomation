using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Tests;
using MobileAutomation.Tests.Pages;
using OpenQA.Selenium.Support.UI;
using MobileAutomation.Tests.Utilities;

namespace RecoveryTrac.AutomatedTests
{
    [TestFixture]
    public class LoadTicketCollectionTests : BaseTest
    {
        private const string FormName = "Collection Load Ticket";

        // ThreadLocal keeps explicit wait instances fully isolated on a per-thread basis
        private readonly ThreadLocal<WebDriverWait> _wait = new();

        [SetUp]
        public void SetupLoadTicketScenario()
        {
            DriverFactory.InitializeDualApps();
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

            try
            {
                var driver = DriverFactory.DriverFlex;
                string evidencePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"Failure_{TestContext.CurrentContext.Test.Name}_{DateTime.Now:yyyyMMdd_HHmmss}");
                ((OpenQA.Selenium.ITakesScreenshot)driver).GetScreenshot().SaveAsFile(evidencePath + ".png");
                TestContext.AddTestAttachment(evidencePath + ".png", "Screen at failure");

                File.WriteAllText(evidencePath + ".txt", $"URL: {driver.Url}{Environment.NewLine}{driver.FindElement(OpenQA.Selenium.By.TagName("body")).Text}");
                TestContext.AddTestAttachment(evidencePath + ".txt", "Page text at failure");
                Console.WriteLine($"📸 Failure evidence saved to: {evidencePath}.png/.txt");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not capture failure evidence: {ex.Message}");
            }
        }

        [OneTimeTearDown]
        public void FinalizeClassDisposal()
        {
            _wait.Dispose();
        }

        [Test]
        public void Test_CreateNewCollectionLoadTicket_And_VerifySubmittedSynced_E2E()
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
                Assert.That(ticketForm.GetFieldValue("Truck Capacity"), Is.EqualTo(truck.TruckCapacity),
                    "Truck Capacity was not auto-filled from the selected truck.");
                Assert.That(ticketForm.GetFieldValue("Truck License Plate"), Is.EqualTo(truck.VehicleTag),
                    "Truck License Plate was not auto-filled from the selected truck's Vehicle Tag.");
                Assert.That(ticketForm.GetFieldValue("Driver Name"), Is.EqualTo(truck.DriverName),
                    "Driver Name was not auto-filled from the selected truck.");
            });

            ticketForm.SelectRadio("Is the above license plate correct?", "Yes");
            ticketForm.EnterFieldValue("New Shared Control", "1");
            ticketForm.ClickNext();

            // -------------------------------------------------------------------------
            // STEP 11: Trailer step
            // -------------------------------------------------------------------------
            ticketForm.WaitForStep("Trailer");
            ticketForm.SelectRadio("Does truck have a trailer attached?", "No");
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
            ticketForm.EnterNotes($"Automated test - {nameof(Test_CreateNewCollectionLoadTicket_And_VerifySubmittedSynced_E2E)} {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            ticketForm.Submit();
            ticketForm.ProceedWithQrGeneration();

            // -------------------------------------------------------------------------
            // STEP 19: Store the generated QR code as a test attachment
            // -------------------------------------------------------------------------
            byte[] qrPng = ticketForm.GetQrCodePng();
            Assert.That(qrPng, Is.Not.Empty, "The generated QR code image is empty.");

            string qrDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "QrCodes");
            Directory.CreateDirectory(qrDirectory);
            string qrPath = Path.Combine(qrDirectory, $"CollectionLoadTicket_{DateTime.Now:yyyyMMdd_HHmmss}.png");
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
                Assert.That(card.GetValueOrDefault("Truck Capacity (cy)"), Is.EqualTo(truck.TruckCapacity),
                    $"Submitted ticket {ticketId} shows an unexpected Truck Capacity.");
                Assert.That(card.GetValueOrDefault("Driver Name"), Is.EqualTo(truck.DriverName),
                    $"Submitted ticket {ticketId} shows an unexpected Driver Name.");
                Assert.That(isSynced, Is.True, $"Submitted ticket {ticketId} did not reach the 'Synced' status.");
            });
        }
    }
}
