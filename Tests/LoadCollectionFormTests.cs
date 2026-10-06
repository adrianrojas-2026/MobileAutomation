using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Tests;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Support.UI;

namespace RecoveryTrac.AutomatedTests
{
    [TestFixture]
    public class LoadCollectionFormTests : BaseTest
    {
        // ThreadLocal ensures explicit wait instances remain fully isolated on a per-thread basis
        private readonly ThreadLocal<WebDriverWait> _wait = new();
        private readonly ThreadLocal<WebDriverWait> _waitDriver = new();

        [SetUp]
        public void SetupFormScenario()
        {
            // Initializes the single RecoveryTrac Flex App instance
            DriverFactory.InitializeDualApps();
            _wait.Value = new WebDriverWait(DriverFactory.DriverFlex, TimeSpan.FromSeconds(15));
            _waitDriver.Value = new WebDriverWait(DriverFactory.DriverDriver, TimeSpan.FromSeconds(15));
        }

         // 🚀 NEW: Clears thread-static allocations immediately after this class finishes executing
        [OneTimeTearDown]
        public void FinalizeClassDisposal()
        {
            Console.WriteLine("Disposing class-level ThreadLocal explicit wait resources to prevent engine memory leaks...");
            
             if (_wait != null)
            {   
                _wait.Dispose();
            }

            if (_waitDriver != null)
            {
                _waitDriver.Dispose();
            }
        }

        [TearDown]
        public void TeardownFormScenario()
        {
            // Clear out the camera injection back buffer to prevent leaking into other tests
            try
            {
                var clearArgs = new Dictionary<string, object> { { "payload", "" } };
                DriverFactory.DriverFlex.ExecuteScript("mobile: injectEmulatorCameraImage", clearArgs);
            }
            catch { /* Suppress non-blocking cleanup errors */ }

        }

        [Test]
        public async Task Test_LoadCollection_Form_To_DriverDisposal_Parallel_E2E()
        {
            var driver = DriverFactory.DriverFlex;
            var driverDriver = DriverFactory.DriverDriver;
            
            var wf = _wait.Value!;
            var wd = _waitDriver.Value!;

            // -------------------------------------------------------------------------
            // STEP 1: Navigate to Forms Option (Bottom Navigation Bar)
            // -------------------------------------------------------------------------
            var formsTab = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Forms'] | //*[@content-desc='Forms']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });
            formsTab.Click();

            // -------------------------------------------------------------------------
            // STEP 2: Select "Load - Collection" Form from the List
            // -------------------------------------------------------------------------
            var loadCollectionMenuOption = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Load - Collection']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });
            loadCollectionMenuOption.Click();

            // -------------------------------------------------------------------------
            // STEP 3: Tap on the "New" Button
            // -------------------------------------------------------------------------
            var newFormButton = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.Button[@text='New'] | //android.widget.TextView[@text='New']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });
            newFormButton.Click();

            var scanHeaderButton = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Scan Truck QR Code'] | //android.widget.Button[contains(@text,'Scan')]"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });
            scanHeaderButton.Click();

            // -------------------------------------------------------------------------
            // STEP 4: Simulate QR Code Scanning from the Truck Placard
            // -------------------------------------------------------------------------
            string testImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "truck_placard_qr.png");
            if (!File.Exists(testImagePath))
            {
                throw new FileNotFoundException($"Missing mock truck placard file at: {testImagePath}");
            }
            byte[] imageBytes = await File.ReadAllBytesAsync(testImagePath);
            string truckPlacardBase64 = Convert.ToBase64String(imageBytes);

            var cameraArgs = new Dictionary<string, object> { { "payload", truckPlacardBase64 } };
            driver.ExecuteScript("mobile: injectEmulatorCameraImage", cameraArgs);

            // -------------------------------------------------------------------------
            // STEP 5: Base Verification & Business Rule Assertions
            // -------------------------------------------------------------------------
            var successToast = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[contains(@text, 'scanned successfully')]"));
                return el != null && el.Displayed ? el : null;
            });
            Assert.That(successToast.Text, Is.EqualTo("The QR code was scanned successfully, and 4 controls were populated."));

            var inputTruckNumber = driver.FindElement(MobileBy.XPath("//android.widget.EditText[contains(@text,'1000') or contains(@text,'9999')]"));
            Assert.That(inputTruckNumber.Text, Is.Not.Null.Or.Empty);

            // -------------------------------------------------------------------------
            // STEP 5b: Assert In-Progress Page Header
            // -------------------------------------------------------------------------
            var inProgressHeader = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[contains(@text, \"In Progress\") and contains(@text, \"Load - Collection\")]"));
                return el != null && el.Displayed ? el : null;
            });
            Assert.That(inProgressHeader.Text, Contains.Substring("Load - Collection"));

            // -------------------------------------------------------------------------
            // STEP 5c: Assert QR-Populated Fields (Driver Name, Capacity, License Plate)
            // -------------------------------------------------------------------------
            var inputDriverName     = driver.FindElement(MobileBy.XPath("//android.widget.EditText[contains(@resource-id, 'DriverName') or contains(@content-desc, 'Driver Name')]"));
            var inputTruckCapacity  = driver.FindElement(MobileBy.XPath("//android.widget.EditText[contains(@resource-id, 'TruckCapacity') or contains(@content-desc, 'Truck Capacity')]"));
            var inputTruckDriverName= driver.FindElement(MobileBy.XPath("//android.widget.EditText[contains(@resource-id, 'TruckDriverName') or contains(@content-desc, 'Truck Driver Name')]"));
            var inputLicensePlate   = driver.FindElement(MobileBy.XPath("//android.widget.EditText[contains(@resource-id, 'LicensePlate') or contains(@content-desc, 'License Plate')]"));

            Assert.Multiple(() =>
            {
                Assert.That(inputDriverName.Text,      Is.Not.Null.And.Not.Empty, "Driver Name was not populated by QR scan.");
                Assert.That(inputTruckCapacity.Text,   Is.Not.Null.And.Not.Empty, "Truck Capacity was not populated by QR scan.");
                Assert.That(inputTruckDriverName.Text, Is.Not.Null.And.Not.Empty, "Truck Driver Name was not populated by QR scan.");
                Assert.That(inputLicensePlate.Text,    Is.Not.Null.And.Not.Empty, "Truck License Plate was not populated by QR scan.");
            });

            // -------------------------------------------------------------------------
            // STEP 5d: Answer "Is Driver Name correct?" → Yes
            // -------------------------------------------------------------------------
            var isDriverNameCorrectYes = wf.Until(d => {
                // Target the Yes radio within the "Is Driver Name correct?" group
                var el = d.FindElement(MobileBy.XPath(
                    "//*[contains(@text, 'Is Driver Name correct')]/following-sibling::*//*[@text='Yes' or @content-desc='Yes']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });
            isDriverNameCorrectYes.Click();

            // -------------------------------------------------------------------------
            // STEP 5e: Answer "Is the above information correct?" → Yes
            // -------------------------------------------------------------------------
            var isInfoCorrectYes = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath(
                    "//*[contains(@text, 'Is the above information correct')]/following-sibling::*//*[@text='Yes' or @content-desc='Yes']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });
            isInfoCorrectYes.Click();

            // -------------------------------------------------------------------------
            // STEP 6: Interactive Field Selections: Truck Debris Type
            // -------------------------------------------------------------------------
            var truckDebrisDropdown = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Truck Debris Type *']/following-sibling::android.view.ViewGroup"));
                return (el != null && el.Displayed) ? el : null;
            });
            truckDebrisDropdown.Click();

            var vegetativeOption = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Vegetative/Woody']"));
                return (el != null && el.Displayed) ? el : null;
            });
            vegetativeOption.Click();

            var modalDoneButton = driver.FindElement(MobileBy.XPath("//android.widget.Button[@text='Done'] | //android.widget.TextView[@text='Done']"));
            wf.Until(d => modalDoneButton.Enabled && modalDoneButton.Displayed);
            modalDoneButton.Click();

            // -------------------------------------------------------------------------
            // STEP 7: Interactive Field Selections: Trailer Debris Type
            // -------------------------------------------------------------------------
            var trailerDebrisDropdown = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Trailer Debris Type *']/following-sibling::android.view.ViewGroup"));
                return (el != null && el.Displayed) ? el : null;
            });
            trailerDebrisDropdown.Click();

            var constructionOption = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Construction & Demolition']"));
                return (el != null && el.Displayed) ? el : null;
            });
            constructionOption.Click();

            var trailerModalDone = driver.FindElement(MobileBy.XPath("//android.widget.Button[@text='Done'] | //android.widget.TextView[@text='Done']"));
            trailerModalDone.Click();

            // -------------------------------------------------------------------------
            // STEP 8: Click "Next" to Navigate to Location Page
            // -------------------------------------------------------------------------
            var nextFormButton = driver.FindElement(MobileBy.XPath("//android.widget.Button[@text='Next'] | //android.widget.TextView[@text='Next']"));
            nextFormButton.Click();

            // -------------------------------------------------------------------------
            // STEP 9: Simulate GPS Location Injection
            // -------------------------------------------------------------------------
            var mockLocation = new Location { Latitude = 28.1586965, Longitude = -81.6926507, Altitude = 0 };
            driver.Location = mockLocation;

            try
            {
                var gpsSyncButton = driver.FindElement(MobileBy.XPath("//android.widget.EditText[@text='28.1586965']/following-sibling::android.view.ViewGroup | //android.widget.ImageView[contains(@content-desc, 'location')]"));
                gpsSyncButton.Click();
            }
            catch { /* Auto-sync handled natively by driver configuration */ }

            // -------------------------------------------------------------------------
            // STEP 10: Assert Location Address and Confirmation Radio Inputs
            // -------------------------------------------------------------------------
            // Wait until the reverse geocode lookup populates the address fields on the screen layout
            var txtAddressNumber = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.EditText[@text='6534'] | //*[contains(@resource-id, 'AddressNumber')]"));
                return el != null && el.Displayed ? el : null;
            });
            
            var txtStreetName = driver.FindElement(MobileBy.XPath("//android.widget.EditText[@text='Polk City Rd'] | //*[contains(@resource-id, 'StreetName')]"));
            var lblInlineAddressText = driver.FindElement(MobileBy.XPath("//android.widget.TextView[contains(@text, '6534 Polk City Rd')]"));

            // Aggregate validation block ensuring telemetry data parsed completely from the injected coordinates
            Assert.Multiple(() =>
            {
                Assert.That(txtAddressNumber.Text, Is.EqualTo("6534"), "Reverse geocoded Address Number field text mismatch.");
                Assert.That(txtStreetName.Text, Is.EqualTo("Polk City Rd"), "Reverse geocoded Street Name field text mismatch.");
                Assert.That(lblInlineAddressText.Text, Contains.Substring("6534 Polk City Rd"), "Visual text description block layout mismatch.");
            });

            // -------------------------------------------------------------------------
            // STEP 11: Tap "Yes" Checkbox Circle & Proceed to Notes Page
            // -------------------------------------------------------------------------
            var radioYesOption = driver.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Yes'] | //android.widget.CheckedTextView[@text='Yes']"));
            radioYesOption.Click();

            var btnNextLocationScreen = driver.FindElement(MobileBy.XPath("//android.widget.Button[@text='Next'] | //android.widget.TextView[@text='Next']"));
            btnNextLocationScreen.Click();

            // -------------------------------------------------------------------------
            // STEP 12: Final Page - Notes & Comments Handling
            // -------------------------------------------------------------------------
            var txtNotesInput = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.EditText[contains(@text, 'Notes & comments')] | //android.widget.EditText"));
                return el != null && el.Displayed ? el : null;
            });

            string mockComment = "Automated test ticket generated via pipeline execution script. Placard QR verified.";
            txtNotesInput.Click();
            txtNotesInput.SendKeys(mockComment);
            
            if (driver.IsKeyboardShown()) { driver.HideKeyboard(); }
            Assert.That(txtNotesInput.Text, Is.EqualTo(mockComment));

            // -------------------------------------------------------------------------
            // STEP 13: Submit the Load - Collection Entry Form
            // -------------------------------------------------------------------------
            var btnSubmitTicket = driver.FindElement(MobileBy.XPath("//android.widget.Button[@text='Submit'] | //android.widget.TextView[@text='Submit']"));
            btnSubmitTicket.Click();

            // -------------------------------------------------------------------------
            // STEP 14: Assert and Intercept the "Generate QR Code" Final Pop-Up Modal
            // -------------------------------------------------------------------------
            var modalTitle = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Generate QR Code'] | //*[contains(@resource-id, 'alertTitle')]"));
                return el != null && el.Displayed ? el : null;
            });

            var modalDescription = driver.FindElement(MobileBy.XPath("//android.widget.TextView[contains(@text, 'will finalize it and make it unmodifiable')]"));
            var checkboxDontShowAgain = driver.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Don\'t show this again'] | //android.widget.CheckBox"));
            var btnCancel = driver.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Cancel'] | //android.widget.Button[@text='Cancel']"));
            var btnProceed = driver.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Proceed'] | //android.widget.Button[@text='Proceed']"));

            Assert.Multiple(() =>
            {
                Assert.That(modalTitle.Text, Is.EqualTo("Generate QR Code"), "Modal alert header copy has changed.");
                Assert.That(modalDescription.Text, Contains.Substring("Generating a QR code for your document will finalize it"), "Modal legal disclaimer body text has been altered.");
                Assert.That(checkboxDontShowAgain.Displayed, Is.True, "'Don't show this again' checkbox selector is missing from the dialog box view.");
                Assert.That(btnCancel.Displayed, Is.True, "'Cancel' secondary termination route is missing.");
                Assert.That(btnProceed.Displayed, Is.True, "'Proceed' execution route confirmation button is missing.");
            });

            btnProceed.Click();
            Console.WriteLine("Pop-up workflow asserted cleanly. Clicked 'Proceed' button.");

            // -------------------------------------------------------------------------
            // 🚀 MERGED PART 2: CROSS-APP VERIFICATION (FORM GENERATION TO DRIVER SCANNING)
            // -------------------------------------------------------------------------
            // Wait for the Ticket QR element to be rendered completely on the Flex screen after Proceed is tapped
            var flexPage = new MobileAutomation.Tests.Pages.FlexApp.FlexAppPage(driver);
            
            // Extracts the newly generated Ticket QR image natively from the Flex app view model buffer (Warning-free)
            string generatedTicketQrBase64 = flexPage.GetOnlyQrCodeAsBase64();

            // Navigate the Driver App to its Scanning Screen
            var btnDriverScanTicket = wd.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.Button[contains(@text, 'Scan Ticket')] | //android.widget.TextView[@text='Scan Ticket']"));
                return (el != null && el.Displayed) ? el : null;
            });
            btnDriverScanTicket.Click();

            // Real-time Camera Frame Injection into the Driver App's context
            Console.WriteLine("Injecting the freshly generated Load-Collection QR into Driver App camera...");
            var ticketCameraArgs = new Dictionary<string, object> { { "payload", generatedTicketQrBase64 } };
            driverDriver.ExecuteScript("mobile: injectEmulatorCameraImage", ticketCameraArgs);

            // Assertions on the Driver App (Disposal Workflow Activation)
            var lblDisposalHeader = wd.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[contains(@text, 'Proceed to Disposal Site')] | //android.widget.TextView[contains(@text, 'Disposal')]"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.Multiple(() =>
            {
                Assert.That(lblDisposalHeader.Text, Contains.Substring("Proceed to Disposal Site"), 
                    "Driver App failed to transition into disposal navigation routing after scanning the ticket.");
                
                var txtTargetRoute = driverDriver.FindElement(MobileBy.XPath("//android.widget.TextView[contains(@text, 'Lucien Way')] | //android.widget.TextView[contains(@text, 'Polk City')]"));
                Assert.That(txtTargetRoute.Displayed, Is.True, "Disposal routing instructions are missing from driver viewport.");
            });

            Console.WriteLine("E2E Synchronization Complete: Ticket successfully processed by Driver App.");
        
        }

    }
}
