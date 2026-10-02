using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Tests;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Support.UI;

namespace RecoveryTrac.AutomatedTests
{
    [TestFixture]
    public class LoadCollectionFormTests : BaseTest
    {
        private WebDriverWait _wait;

        [SetUp]
        public void SetupFormScenario()
        {
            // Initializes the single RecoveryTrac Flex App instance
            DriverFactory.Initialize("Android_Flex", "flex");
            _wait = new WebDriverWait(DriverFactory.AndroidDriver, TimeSpan.FromSeconds(15));
        }

        [TearDown]
        public void TeardownFormScenario()
        {
            // Clear out the camera injection back buffer to prevent leaking into other tests
            try
            {
                var clearArgs = new Dictionary<string, object> { { "payload", "" } };
                DriverFactory.AndroidDriver.ExecuteScript("mobile: injectEmulatorCameraImage", clearArgs);
            }
            catch { /* Suppress non-blocking cleanup errors */ }

        }

        [Test]
        public async Task Test_LoadCollection_Form_Populates_From_Placard_QR()
        {
            var driver = DriverFactory.AndroidDriver;

            // -------------------------------------------------------------------------
            // STEP 1: Navigate to Forms Option (Bottom Navigation Bar)
            // -------------------------------------------------------------------------
            // Locating the Forms tab item on the footer bar visible in Image 1 and 2
            var formsTab = _wait.Until(d => {
            var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Forms'] || //*[@content-desc='Forms']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });
            formsTab.Click();

            // -------------------------------------------------------------------------
            // STEP 2: Select "Load - Collection" Form from the List
            // -------------------------------------------------------------------------
            // Clicking the entry matching your specific form menu layout (Image 2)
            var loadCollectionMenuOption = _wait.Until(d => {
            var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Load - Collection']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });
            loadCollectionMenuOption.Click();

            // -------------------------------------------------------------------------
            // STEP 3: Tap on the "New" Button
            // -------------------------------------------------------------------------
            // Found on the bottom right action bar (Image 3) when current items list is empty
            var newFormButton = _wait.Until(d => {
            var el = d.FindElement(MobileBy.XPath("//android.widget.Button[@text=' New'] || //android.widget.TextView[@text='New']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });
            newFormButton.Click();

            // Validate that the verification wizard opened cleanly (Image 4)
            var scanHeaderButton = _wait.Until(d => {
            var elements = d.FindElements(MobileBy.XPath("//android.widget.TextView[@text='Scan Truck QR Code'] || //android.widget.Button[contains(@text,'Scan')]"));
                return elements.Count > 0 && elements[0].Displayed ? elements[0] : null;
            });
            
            // -------------------------------------------------------------------------
            // STEP 4: Simulate QR Code Scanning from the Truck Placard
            // -------------------------------------------------------------------------
            // Tap the scan button to activate the system camera layer
            scanHeaderButton.Click();

            // 1. Read the mock placard QR image from your automation framework's test data directory
            string testImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "truck_placard_qr.png");
            
            if (!File.Exists(testImagePath))
            {
                throw new FileNotFoundException($"Missing mock truck placard file needed for camera pipeline synchronization at: {testImagePath}");
            }
            
            byte[] imageBytes = await File.ReadAllBytesAsync(testImagePath);
            string truckPlacardBase64 = Convert.ToBase64String(imageBytes);

            // 2. Real-time background injection into the Android Virtual Camera frame
            var cameraArgs = new Dictionary<string, object> { { "payload", truckPlacardBase64 } };
            driver.ExecuteScript("mobile: injectEmulatorCameraImage", cameraArgs);

            // -------------------------------------------------------------------------
            // STEP 5: Verification & Business Rule Assertions (Image 5)
            // -------------------------------------------------------------------------
            
            // 1. Confirm success notification banner appears at the bottom
            var successToast = _wait.Until(d => {
            var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[contains(@text, 'scanned successfully')]"));
                return el != null && el.Displayed ? el : null;
            });
            
            Assert.That(successToast.Text, Is.EqualTo("The QR code was scanned successfully, and 4 controls were populated."));

            // 2. Locate field elements to assert business telemetry data parsed cleanly
            var inputDriverNameScan = driver.FindElement(MobileBy.XPath("//android.widget.EditText[@text='PAUL JONES'] || //*[contains(@resource-id, 'DriverNameScan')]"));
            var inputTruckNumber = driver.FindElement(MobileBy.XPath("//android.widget.EditText[@text='6L82-TV-1000'] || //*[contains(@resource-id, 'TruckNumber')]"));
            var inputTruckCapacity = driver.FindElement(MobileBy.XPath("//android.widget.EditText[@text='55'] || //*[contains(@resource-id, 'TruckCapacity')]"));
            var inputTruckDriverName = driver.FindElement(MobileBy.XPath("//android.widget.EditText[@text='PAUL JONES'] || //*[contains(@resource-id, 'TruckDriverName')]"));
            var inputLicensePlate = driver.FindElement(MobileBy.XPath("//android.widget.EditText[@text='AB1 2CD'] || //*[contains(@resource-id, 'TruckLicensePlate')]"));

            // 3. Structural Field Assertions
            Assert.Multiple(() =>
            {
                Assert.That(inputDriverNameScan.Text, Is.EqualTo("PAUL JONES"), "Driver Name (Scan) field text mismatch.");
                Assert.That(inputTruckNumber.Text, Is.EqualTo("6L82-TV-1000"), "Truck Number field text mismatch.");
                Assert.That(inputTruckCapacity.Text, Is.EqualTo("55"), "Truck Capacity profile mapping mismatched structural constraints.");
                Assert.That(inputTruckDriverName.Text, Is.EqualTo("PAUL JONES"), "Truck Driver Name baseline reference mismatch.");
                Assert.That(inputLicensePlate.Text, Is.EqualTo("AB1 2CD"), "Truck License Plate profile configuration mismatch.");
            });

            // -------------------------------------------------------------------------
            // STEP 6: Interactive Field Selections: Truck Debris Type
            // -------------------------------------------------------------------------
            // Locate and click the dropdown container directly below the "Truck Debris Type" label
            var truckDebrisDropdown = _wait.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Truck Debris Type *']/following-sibling::android.view.ViewGroup[1] || //*[contains(@resource-id, 'TruckDebrisTypeDropdown')]"));
                return (el != null && el.Displayed) ? el : null;
            });
            truckDebrisDropdown.Click();

            // Handle the interactive modal (Image 1): Check the "Vegetative/Woody" checkbox option
            var vegetativeOption = _wait.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Vegetative/Woody']"));
                return (el != null && el.Displayed) ? el : null;
            });
            vegetativeOption.Click();

            // Click the "Done" action button inside the modal overlay window
            var modalDoneButton = driver.FindElement(MobileBy.XPath("//android.widget.Button[@text='Done'] || //android.widget.TextView[@text='Done']"));
            modalDoneButton.Click();

            // -------------------------------------------------------------------------
            // STEP 7: Interactive Field Selections: Trailer Debris Type
            // -------------------------------------------------------------------------
            // Scroll down if necessary, locate the trailer dropdown container
            var trailerDebrisDropdown = _wait.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Trailer Debris Type *']/following-sibling::android.view.ViewGroup[1] || //*[contains(@resource-id, 'TrailerDebrisTypeDropdown')]"));
                return (el != null && el.Displayed) ? el : null;
            });
            trailerDebrisDropdown.Click();

            // Handle the modal overlay selection: Choose "Construction & Demolition"
            var constructionOption = _wait.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Construction & Demolition']"));
                return (el != null && el.Displayed) ? el : null;
            });
            constructionOption.Click();

            // Confirm selection via modal Done button again
            var trailerModalDone = driver.FindElement(MobileBy.XPath("//android.widget.Button[@text='Done'] || //android.widget.TextView[@text='Done']"));
            trailerModalDone.Click();

            // -------------------------------------------------------------------------
            // STEP 8: Assert Selection Values and Tap "Next" Button
            // -------------------------------------------------------------------------
            // Validate text fields match updated form allocations (Image 2)
            var selectedTruckDebrisText = driver.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Vegetative/Woody']"));
            var selectedTrailerDebrisText = driver.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Construction & Demolition']"));

            Assert.Multiple(() =>
            {
                Assert.That(selectedTruckDebrisText.Text, Is.EqualTo("Vegetative/Woody"), "Truck debris validation failed.");
                Assert.That(selectedTrailerDebrisText.Text, Is.EqualTo("Construction & Demolition"), "Trailer debris validation failed.");
            });

            // Tap the Next navigation action button on the bottom right footer bar
            var nextFormButton = driver.FindElement(MobileBy.XPath("//android.widget.Button[@text='Next'] || //android.widget.TextView[@text='Next']"));
            nextFormButton.Click();

            // -------------------------------------------------------------------------
            // STEP 9: Simulate GPS Location Injection (New Screen Setup)
            // -------------------------------------------------------------------------
            Console.WriteLine("Injecting GPS Coordinates for Polk City Rd...");
            
            // Explicitly mock the device telemetry framework using Appium Location objects
            var mockLocation = new Location { Latitude = 28.1586965, Longitude = -81.6926507, Altitude = 0 };
            driver.Location = mockLocation;

            // Optional: If your app requires a physical click on the location-refresh icon (top right crosshair icon)
            try
            {
                var gpsSyncButton = driver.FindElement(MobileBy.XPath("//android.widget.EditText[@text='28.1586965']/following-sibling::android.view.ViewGroup || //android.widget.ImageView[contains(@content-desc, 'location')]"));
                gpsSyncButton.Click();
            }
            catch { /* Suppress if the driver.Location command triggered the sync automatically */ }

            // -------------------------------------------------------------------------
            // STEP 10: Assert Location Address and Confirmation Radio Inputs
            // -------------------------------------------------------------------------
            // Wait until the geocoded reverse lookup populates the visible fields cleanly
            var txtAddressNumber = _wait.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.EditText[@text='6534']"));
                return el != null && el.Displayed ? el : null;
            });
            
            var txtStreetName = driver.FindElement(MobileBy.XPath("//android.widget.EditText[@text='Polk City Rd']"));
            var lblInlineAddressText = driver.FindElement(MobileBy.XPath("//android.widget.TextView[contains(@text, '6534 Polk City Rd')]"));

            Assert.Multiple(() =>
            {
                Assert.That(txtAddressNumber.Text, Is.EqualTo("6534"), "Address Number failed to match reverse geocode.");
                Assert.That(txtStreetName.Text, Is.EqualTo("Polk City Rd"), "Street Name failed to match reverse geocode.");
                Assert.That(lblInlineAddressText.Text, Contains.Substring("6534 Polk City Rd"), "Visual text description block mismatch.");
            });

            // =========================================================================
            // STEP 11: Tap "Yes" Checkbox Circle & Proceed to Notes Page
            // =========================================================================
            // Locate the 'Yes' option for "Is the above address info correct?" (Image 5 from previous step)
            var radioYesOption = _wait.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Yes'] || //android.widget.CheckedTextView[@text='Yes'] || //*[contains(@content-desc, 'Yes')]"));
                return (el != null && el.Displayed) ? el : null;
            });
            radioYesOption.Click();

            // Click the Next button on the bottom footer bar (Context: Location Information screen)
            var btnNextLocationScreen = driver.FindElement(MobileBy.XPath("//android.widget.Button[@text='Next'] || //android.widget.TextView[@text='Next']"));
            btnNextLocationScreen.Click();

            // =========================================================================
            // STEP 12: Final Page - Notes & Comments Handling (Review & Submit Screen)
            // =========================================================================
            // Locate the input box by checking for the placeholder text "Notes & comments..."
            var txtNotesInput = _wait.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.EditText[contains(@text, 'Notes & com')] || //android.widget.EditText[contains(@text, 'Notes & comments')] || //android.widget.EditText"));
                return el != null && el.Displayed ? el : null;
            });

            string mockComment = "Automated test ticket generated via pipeline execution script. Placard QR verified.";
            
            txtNotesInput.Click();
            txtNotesInput.SendKeys(mockComment);
            
            // Critical Step: Hide the soft keyboard so it doesn't block the 'Submit' button on the screen layout
            if (driver.IsKeyboardShown()) 
            { 
                driver.HideKeyboard(); 
            }

            // Verify that your note text bound successfully to the field element
            Assert.That(txtNotesInput.Text, Is.EqualTo(mockComment), "The custom notes text failed to bind to the form field.");

                        // =========================================================================
            // STEP 13: Submit the Load - Collection Entry Form
            // =========================================================================
            // Defensive check: If the keyboard is still active from step 12, hide it to uncover the footer actions
            if (driver.IsKeyboardShown()) 
            { 
                driver.HideKeyboard(); 
            }

            // Locate and click the primary 'Submit' action button on the bottom right footer bar
            var btnSubmitTicket = _wait.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.Button[@text='Submit'] || //android.widget.TextView[@text='Submit'] || //*[contains(@content-desc, 'Submit')]"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });
            btnSubmitTicket.Click();


            // =========================================================================
            // STEP 14: Assert and Intercept the "Generate QR Code" Final Pop-Up Modal
            // =========================================================================
            // Wait for the modal dialog layout to overlay the current view stream cleanly
            var modalTitle = _wait.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Generate QR Code'] || //*[contains(@resource-id, 'alertTitle')]"));
                return el != null && el.Displayed ? el : null;
            });

            // Locate the structural sub-elements inside the alert pop-up frame
            var modalDescription = driver.FindElement(MobileBy.XPath("//android.widget.TextView[contains(@text, 'will finalize it and make it unmodifiable')]"));
            var checkboxDontShowAgain = driver.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Don\'t show this again'] || //android.widget.CheckBox"));
            var btnCancel = driver.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Cancel'] || //android.widget.Button[@text='Cancel']"));
            var btnProceed = driver.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Proceed'] || //android.widget.Button[@text='Proceed']"));

            // Aggregate validation block ensuring copy consistency and button layout presence
            Assert.Multiple(() =>
            {
                Assert.That(modalTitle.Text, Is.EqualTo("Generate QR Code"), "Modal alert header copy has changed.");
                Assert.That(modalDescription.Text, Contains.Substring("Generating a QR code for your document will finalize it"), "Modal legal disclaimer body text has been altered.");
                Assert.That(checkboxDontShowAgain.Displayed, Is.True, "'Don't show this again' checkbox selector is missing from the dialog box view.");
                Assert.That(btnCancel.Displayed, Is.True, "'Cancel' secondary termination route is missing.");
                Assert.That(btnProceed.Displayed, Is.True, "'Proceed' execution route confirmation button is missing.");
            });

            // Tap 'Proceed' to securely lock down the form modifications and trigger the backend save sequence
            btnProceed.Click();
            Console.WriteLine("Pop-up workflow asserted cleanly. Clicked 'Proceed' button.");

        }
    
        [Test]
        [Order(2)] // Ensures this runs immediately after the form is generated
        public void Test_Driver_Scans_Generated_Ticket_QR_And_Proceeds_To_Disposal()
        {
            // --- STEP 1: Initialize both app contexts simultaneously ---
            // Activates Flex App (with the generated QR on screen) and opens the Driver App
            DriverFactory.InitializeDualApps("Android_Flex", "Android_Driver");
            
            var flexDriver = DriverFactory.DriverFlex;
            var driverDriver = DriverFactory.DriverDriver;
            var waitDriver = new WebDriverWait(driverDriver, TimeSpan.FromSeconds(15));

            // --- STEP 2: Capture the newly generated QR Code from the Flex screen ---
            // Uses your optimized cropping technique to grab only the clean bounding box of the QR image
            // Instantiate the page context for the Flex app screen
            var flexPage = new MobileAutomation.Tests.Pages.FlexApp.FlexAppPage(flexDriver);
            // Use the native cross-platform element screenshot method
            string generatedTicketQrBase64 = flexPage.GetOnlyQrCodeAsBase64();

            // --- STEP 3: Navigate the Driver App to its Scanning Screen ---
            var btnDriverScanTicket = waitDriver.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.Button[contains(@text, 'Scan Ticket')] || //android.widget.TextView[@text='Scan Ticket']"));
                return (el != null && el.Displayed) ? el : null;
            });
            btnDriverScanTicket.Click();

            // --- STEP 4: Real-time Injection into the Driver's Camera Frame ---
            Console.WriteLine("Injecting the freshly generated Load-Collection QR into Driver App camera...");
            var cameraArgs = new Dictionary<string, object> { { "payload", generatedTicketQrBase64 } };
            driverDriver.ExecuteScript("mobile: injectEmulatorCameraImage", cameraArgs);

            // --- STEP 5: Assertions on the Driver App (Disposal Workflow Activation) ---
            // Wait for the Driver App to process the ticket code and transition to the map/disposal view
            var lblDisposalHeader = waitDriver.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[contains(@text, 'Proceed to Disposal Site')] || //android.widget.TextView[contains(@text, 'Disposal')]"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.Multiple(() =>
            {
                Assert.That(lblDisposalHeader.Text, Contains.Substring("Proceed to Disposal Site"), 
                    "Driver App failed to transition into disposal navigation routing after scanning the ticket.");
                
                // Assert any inline truck metadata shown on the driver screen matches the ticket entries
                var txtTargetRoute = driverDriver.FindElement(MobileBy.XPath("//android.widget.TextView[contains(@text, 'Lucien Way')] || //android.widget.TextView[contains(@text, 'Polk City')]"));
                Assert.That(txtTargetRoute.Displayed, Is.True, "Disposal routing instructions are missing from driver viewport.");
            });

            Console.WriteLine("E2E Synchronization Complete: Ticket successfully processed by Driver App.");
        }

    }
}
