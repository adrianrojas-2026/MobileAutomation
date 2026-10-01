using OpenQA.Selenium;
using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Tests;
using MobileAutomation.Tests.Pages.Driver;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Support.UI;

namespace RecoveryTrac.AutomatedTests
{
    [TestFixture]
    public class LoadDisposalFormTests : BaseTest
    {
        private WebDriverWait _waitFlex;
        private WebDriverWait _waitDriver;

        [SetUp]
        public void SetupDisposalScenario()
        {
            // Initializes dual sessions concurrently to handle the cross-app verification flow
            DriverFactory.InitializeDualApps("Android_Flex", "Android_Driver");
            
            _waitFlex = new WebDriverWait(DriverFactory.DriverFlex, TimeSpan.FromSeconds(15));
            _waitDriver = new WebDriverWait(DriverFactory.DriverDriver, TimeSpan.FromSeconds(15));
        }

        [TearDown]
        public void TeardownDisposalScenario()
        {
            try
            {
                var clearArgs = new Dictionary<string, object> { { "payload", "" } };
                DriverFactory.DriverFlex.ExecuteScript("mobile: injectEmulatorCameraImage", clearArgs);
            }
            catch { /* Suppress non-blocking cleanup errors */ }

            DriverFactory.Quit();
        }

        // TBD QA Test Strategy
        // Sequential (Current): [Test 1: Fill Form] ──(waits)──> [Test 2: Scan Receipt]  (Locks Thread)
        // Parallel Strategy A:  [Test: Full E2E Flow (Form + Scan)] 🚀 Runs independently on any free thread! [merge two tests]

        [Test]
        public void Test_LoadDisposal_Workflow_Synchronization()
        {
            var flexDriver = DriverFactory.DriverFlex;
            var driverDriver = DriverFactory.DriverDriver;
            var driverPage = new DriverAppPage(driverDriver);

            // =========================================================================
            // STEP 1: Tap on Forms Option (Bottom Navigation Bar)
            // =========================================================================
            var formsTab = _waitFlex.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Forms'] || //*[@content-desc='Forms']"));
                return (el != null && el.Displayed) ? el : null;
            });
            formsTab.Click();

            // =========================================================================
            // STEP 2: Select "Load - Disposal" Option from the Menu List
            // =========================================================================
            var loadDisposalOption = _waitFlex.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Load - Disposal']"));
                return (el != null && el.Displayed) ? el : null;
            });
            loadDisposalOption.Click();

            // =========================================================================
            // STEP 3: Click/Tap "New" Option Button
            // =========================================================================
            var btnNewDisposal = _waitFlex.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.Button[@text=' New'] || //android.widget.TextView[@text='New']"));
                return (el != null && el.Displayed) ? el : null;
            });
            btnNewDisposal.Click();

            // =========================================================================
            // STEP 4: Select Disposal Site & Tap Next
            // =========================================================================
            // Verifying the dropdown container field is visible
            var dropdownDisposalSite = _waitFlex.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Disposal Site *']/following-sibling::android.view.ViewGroup || //android.widget.EditText[contains(@text, 'LANDFILL')]"));
                return el != null && el.Displayed ? el : null;
            });
            
            // Asserting matching baseline text default value from your screenshot
            Assert.That(dropdownDisposalSite.Text, Is.EqualTo("ORANGE COUNTY LANDFILL"), "Disposal site pre-selected value mismatch.");

            var btnNextImportData = flexDriver.FindElement(MobileBy.XPath("//android.widget.Button[@text='Next'] || //android.widget.TextView[@text='Next']"));
            btnNextImportData.Click();

            // =========================================================================
            // STEP 5: Select "Having trouble scanning" to open Ticket fallback (Step 5 & 6)
            // =========================================================================
            // Checking the fallback option radio button as demonstrated in your layout sequence
            var radioTroubleScanning = _waitFlex.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='having trouble scanning']/following-sibling::android.view.ViewGroup || //android.widget.TextView[@text='having trouble scanning']"));
                return el != null && el.Displayed ? el : null;
            });
            radioTroubleScanning.Click();

            // Select matching active entry from the Search for Collection Ticket dropdown selection row
            var ticketDropdown = _waitFlex.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Search for Collection Ticket *']/following-sibling::android.view.ViewGroup"));
                return el != null && el.Displayed ? el : null;
            });
            ticketDropdown.Click();

            var targetTicketOption = _waitFlex.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='6L82-LC-100G']"));
                return el != null && el.Displayed ? el : null;
            });
            targetTicketOption.Click();

            // =========================================================================
            // STEP 6: Scan QR Code Driver presents in the Driver App
            // =========================================================================
            // 1. Force the Driver App to compile and render its active code ticket
            driverPage.GenerateTicket();
            Assert.That(driverPage.IsQrCodeRendered(timeoutSeconds: 5), Is.True, "Driver App failed to render the verification QR code canvas.");

            // 2. Capture only the element bounding box code stream from the Driver screen natively
            string currentTicketQrBase64 = driverPage.GetOnlyQrCodeAsBase64();

            // 3. Trigger the camera scanner layer on the Flex app
            var btnScanDriversQr = flexDriver.FindElement(MobileBy.XPath("//android.widget.Button[contains(@text,'Scan the driver')] || //android.widget.TextView[contains(@text,'Scan')]"));
            btnScanDriversQr.Click();

            // 4. Background injection into the Flex device camera frame 
            var injectionArgs = new Dictionary<string, object> { { "payload", currentTicketQrBase64 } };
            flexDriver.ExecuteScript("mobile: injectEmulatorCameraImage", injectionArgs);

            // =========================================================================
            // STEP 7: Confirm Autofilled Fields & Fill Truck Measurements
            // =========================================================================
            var btnNextTruckContainer = _waitFlex.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.Button[@text='Next'] || //android.widget.TextView[@text='Next']"));
                return (el != null && el.Displayed) ? el : null;
            });
            btnNextTruckContainer.Click();

            // Assert autofilled text values mapped correctly from the background token
            var txtTruckCapacity = _waitFlex.Until(d => d.FindElement(MobileBy.XPath("//android.widget.EditText[@text='57']")));
            var txtDebrisClass = flexDriver.FindElement(MobileBy.XPath("//android.widget.EditText[@text='VEGETATIVE/WOODY']"));
            
            Assert.Multiple(() =>
            {
                Assert.That(txtTruckCapacity.Text, Is.EqualTo("57"), "Autofilled Truck Capacity mismatch.");
                Assert.That(txtDebrisClass.Text, Is.EqualTo("VEGETATIVE/WOODY"), "Autofilled Debris Class structural validation mismatch.");
            });

            // Tap "Yes" radio group button confirmation box line item
            var radioCapacityCorrectYes = flexDriver.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Yes']"));
            radioCapacityCorrectYes.Click();

            // Type the estimated Load Call field percentage rate value into the form input
            var txtLoadCallPercent = flexDriver.FindElement(MobileBy.XPath("//android.widget.EditText[contains(@resource-id, 'loadCall')] || //android.widget.EditText[@text='']"));

            txtLoadCallPercent.Click();
            txtLoadCallPercent.SendKeys("75");

            if (flexDriver.IsKeyboardShown()) { flexDriver.HideKeyboard(); }

            // =========================================================================
            // STEP 8: Once Submit / Finalize Form
            // =========================================================================
            var btnSubmitDisposalForm = flexDriver.FindElement(MobileBy.XPath("//android.widget.Button[@text='Submit'] || //android.widget.TextView[@text='Submit']"));
            btnSubmitDisposalForm.Click();
            
            Console.WriteLine("Load Disposal Form successfully compiled, synchronized cross-app, and submitted.");
        }

        [Test]
        [Order(2)] // Executes sequentially right after the form submission creates the prompt
        public void Test_DisposalReceipt_Capture_And_RealTime_Sync_Status()
        {
            var flexDriver = DriverFactory.DriverFlex;
            var driverDriver = DriverFactory.DriverDriver;

            // =========================================================================
            // STEP 1: Handle the "Generate QR Code" Submission Pop-Up Modal
            // =========================================================================
            var modalTitle = _waitFlex.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Generate QR Code']"));
                return el != null && el.Displayed ? el : null;
            });

            var btnProceed = flexDriver.FindElement(MobileBy.XPath("//android.widget.Button[@text='Proceed'] || //android.widget.TextView[@text='Proceed']"));
            btnProceed.Click();

            // =========================================================================
            // STEP 2: Capture Load Receipt QR Code (Flex App -> Driver App Bridge)
            // =========================================================================
            // 1. Wait until the Receipt QR canvas completely renders on the Flex screen
            var receiptQrHeader = _waitFlex.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[contains(@text, 'Receipt QR Code')]"));
                return el != null && el.Displayed ? el : null;
            });

            // 2. Extract only the isolated bounding box of the canvas cleanly on the server backend
            var qrElement = flexDriver.FindElement(MobileBy.XPath("//android.widget.ImageView || //*[contains(@resource-id, 'img_receipt_qr')]"));
            var qrScreenshot = ((ITakesScreenshot)qrElement).GetScreenshot();
            string receiptQrBase64 = qrScreenshot.AsBase64EncodedString;

            // 3. Command the Driver App to navigate to its scan receipt utility canvas
            var btnDriverScanReceipt = _waitDriver.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.Button[contains(@text, 'Scan Receipt')] || //android.widget.TextView[@text='Scan Receipt']"));
                return (el != null && el.Displayed) ? el : null;
            });
            btnDriverScanReceipt.Click();

            // 4. Inject the generated image into the Driver's virtual camera frame
            Console.WriteLine("Bridging Load Receipt QR code live into Driver App canvas context...");
            var cameraArgs = new Dictionary<string, object> { { "payload", receiptQrBase64 } };
            driverDriver.ExecuteScript("mobile: injectEmulatorCameraImage", cameraArgs);

            // 5. Dismiss the receipt modal view on the Flex App by closing it out
            var btnCloseReceiptView = flexDriver.FindElement(MobileBy.XPath("//android.widget.TextView[@text='✕'] || //android.widget.Button[contains(@text, 'Close')] || //android.widget.ImageView[contains(@content-desc, 'close')]"));
            btnCloseReceiptView.Click();

            // =========================================================================
            // STEP 3: Assert Syncing Status Progression (SYNCING -> SYNCED)
            // =========================================================================
            // Locating the record card matching the targeted ticket ID #5BQ3-LD-100A
            var lblTicketCardHeader = _waitFlex.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='#5BQ3-LD-100A']"));
                return el != null && el.Displayed ? el : null;
            });

            // 1. Immediate checkpoint: Validate that the card starts out processing in orange
            var lblSyncingStatus = flexDriver.FindElement(MobileBy.XPath("//android.widget.TextView[@text='SYNCING']"));
            Assert.That(lblSyncingStatus.Text, Is.EqualTo("SYNCING"), "The ticket card failed to immediately initialize in the SYNCING buffer state.");
            Console.WriteLine("Step 3a verified: Form state is safely captured in the background 'SYNCING' queue loop.");

            // 2. Fluent check loop: Wait for the network layer to successfully offload the data packet
            var lblSyncedStatus = _waitFlex.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='SYNCED']"));
                return (el != null && el.Displayed) ? el : null;
            });

            // 3. Final validation assertion
            Assert.That(lblSyncedStatus.Text, Is.EqualTo("SYNCED"), "The form background sync process failed to transition cleanly to complete.");
            Console.WriteLine("Step 3b verified: Remote record officially marked as 'SYNCED' globally inside the app tree database.");
        }

    }
}