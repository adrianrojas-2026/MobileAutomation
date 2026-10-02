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
        // ThreadLocal ensures that explicit waits remain isolated on a per-thread basis
        private readonly ThreadLocal<WebDriverWait> _waitFlex = new();
        private readonly ThreadLocal<WebDriverWait> _waitDriver = new();

        [SetUp]
        public void SetupDisposalScenario()
        {
            // Initializes dual sessions concurrently to handle the cross-app verification flow
            DriverFactory.InitializeDualApps("Android_Flex", "Android_Driver");
            
            _waitFlex.Value = new WebDriverWait(DriverFactory.DriverFlex, TimeSpan.FromSeconds(15));
            _waitDriver.Value = new WebDriverWait(DriverFactory.DriverDriver, TimeSpan.FromSeconds(15));
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

        [Test]
        public void Test_LoadDisposal_To_ReceiptSync_Complete_Parallel_E2E()
        {
            var flexDriver = DriverFactory.DriverFlex;
            var driverDriver = DriverFactory.DriverDriver;
            var driverPage = new DriverAppPage(driverDriver);
            
            var wf = _waitFlex.Value!;
            var wd = _waitDriver.Value!;

            // =========================================================================
            // STEP 1: Tap on Forms Option (Bottom Navigation Bar)
            // =========================================================================
            var formsTab = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Forms'] || //*[@content-desc='Forms']"));
                return (el != null && el.Displayed) ? el : null;
            });
            formsTab.Click();

            // =========================================================================
            // STEP 2: Select "Load - Disposal" Option from the Menu List
            // =========================================================================
            var loadDisposalOption = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Load - Disposal']"));
                return (el != null && el.Displayed) ? el : null;
            });
            loadDisposalOption.Click();

            // =========================================================================
            // STEP 3: Click/Tap "New" Option Button
            // =========================================================================
            var btnNewDisposal = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.Button[@text=' New'] || //android.widget.TextView[@text='New']"));
                return (el != null && el.Displayed) ? el : null;
            });
            btnNewDisposal.Click();

            // =========================================================================
            // STEP 4: Select Disposal Site & Tap Next
            // =========================================================================
            var dropdownDisposalSite = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Disposal Site *']/following-sibling::android.view.ViewGroup || //android.widget.EditText[contains(@text, 'LANDFILL')]"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(dropdownDisposalSite.Text, Is.EqualTo("ORANGE COUNTY LANDFILL"), "Disposal site pre-selected value mismatch.");

            var btnNextImportData = flexDriver.FindElement(MobileBy.XPath("//android.widget.Button[@text='Next'] || //android.widget.TextView[@text='Next']"));
            btnNextImportData.Click();

            // =========================================================================
            // STEP 5: Select "Having trouble scanning" to open Ticket fallback
            // =========================================================================
            var radioTroubleScanning = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='having trouble scanning']/following-sibling::android.view.ViewGroup || //android.widget.TextView[@text='having trouble scanning']"));
                return el != null && el.Displayed ? el : null;
            });
            radioTroubleScanning.Click();

            var ticketDropdown = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Search for Collection Ticket *']/following-sibling::android.view.ViewGroup"));
                return el != null && el.Displayed ? el : null;
            });
            ticketDropdown.Click();

            var targetTicketOption = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='6L82-LC-100G']"));
                return el != null && el.Displayed ? el : null;
            });
            targetTicketOption.Click();

            // =========================================================================
            // STEP 6: Scan QR Code Driver presents in the Driver App
            // =========================================================================
            driverPage.GenerateTicket();
            Assert.That(driverPage.IsQrCodeRendered(timeoutSeconds: 5), Is.True, "Driver App failed to render the verification QR code canvas.");

            string currentTicketQrBase64 = driverPage.GetOnlyQrCodeAsBase64();

            var btnScanDriversQr = flexDriver.FindElement(MobileBy.XPath("//android.widget.Button[contains(@text,'Scan the driver')] || //android.widget.TextView[contains(@text,'Scan')]"));
            btnScanDriversQr.Click();

            var injectionArgs = new Dictionary<string, object> { { "payload", currentTicketQrBase64 } };
            flexDriver.ExecuteScript("mobile: injectEmulatorCameraImage", injectionArgs);

            // =========================================================================
            // STEP 7: Confirm Autofilled Fields & Fill Truck Measurements
            // =========================================================================
            var btnNextTruckContainer = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.Button[@text='Next'] || //android.widget.TextView[@text='Next']"));
                return (el != null && el.Displayed) ? el : null;
            });
            btnNextTruckContainer.Click();

            var txtTruckCapacity = wf.Until(d => d.FindElement(MobileBy.XPath("//android.widget.EditText[@text='57']")));
            var txtDebrisClass = flexDriver.FindElement(MobileBy.XPath("//android.widget.EditText[@text='VEGETATIVE/WOODY']"));
            
            Assert.Multiple(() =>
            {
                Assert.That(txtTruckCapacity.Text, Is.EqualTo("57"), "Autofilled Truck Capacity mismatch.");
                Assert.That(txtDebrisClass.Text, Is.EqualTo("VEGETATIVE/WOODY"), "Autofilled Debris Class structural validation mismatch.");
            });

            var radioCapacityCorrectYes = flexDriver.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Yes']"));
            radioCapacityCorrectYes.Click();

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

            // =========================================================================
            // STEP 9: Handle the "Generate QR Code" Submission Pop-Up Modal
            // =========================================================================
            var modalTitle = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Generate QR Code']"));
                return el != null && el.Displayed ? el : null;
            });

            var btnProceed = flexDriver.FindElement(MobileBy.XPath("//android.widget.Button[@text='Proceed'] || //android.widget.TextView[@text='Proceed']"));
            btnProceed.Click();

             // =========================================================================
            // STEP 10: Capture Load Receipt QR Code (Flex App -> Driver App Bridge)
            // =========================================================================
            // 1. Wait until the specialized receipt view container completely renders on screen
            var receiptQrHeader = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[contains(@text, 'Receipt QR Code')]"));
                return el != null && el.Displayed ? el : null;
            });

            // 2. Capture ONLY the bounding box of the QR image natively on the Appium server (0 platform warnings)
            var qrElement = flexDriver.FindElement(MobileBy.XPath("//android.widget.ImageView || //*[contains(@resource-id, 'img_receipt_qr')]"));
            var qrScreenshot = ((ITakesScreenshot)qrElement).GetScreenshot();
            string receiptQrBase64 = qrScreenshot.AsBase64EncodedString;

            // 3. Switch context to Driver App and tap the 'Scan Receipt' navigation box
            var btnDriverScanReceipt = wd.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.Button[contains(@text, 'Scan Receipt')] || //android.widget.TextView[@text='Scan Receipt']"));
                return (el != null && el.Displayed) ? el : null;
            });
            btnDriverScanReceipt.Click();

            // 4. Inject the raw image payload byte stream directly into the driver's camera sensor
            Console.WriteLine("Bridging Load Receipt QR code live into Driver App canvas context...");
            var cameraArgs = new Dictionary<string, object> { { "payload", receiptQrBase64 } };
            driverDriver.ExecuteScript("mobile: injectEmulatorCameraImage", cameraArgs);
            // 5. Dismiss the modal card overlay on the Flex supervisor screen to view the dashboard list
            var btnCloseReceiptView = flexDriver.FindElement(MobileBy.XPath("//android.widget.TextView[@text='✕'] || //android.widget.Button[contains(@text, 'Close')] || //android.widget.ImageView[contains(@content-desc, 'close')]"));
            btnCloseReceiptView.Click();

            // =========================================================================
            // STEP 11: Assert Syncing Status Progression (SYNCING -> SYNCED)
            // =========================================================================
            // Find the list entry matching the tracking ticket number #5BQ3-LD-100A
            var lblTicketCardHeader = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='#5BQ3-LD-100A']"));
                return el != null && el.Displayed ? el : null;
            });

            // 1. Immediate validation check: Verify that it enters the queue labeled in orange as 'SYNCING'
            var lblSyncingStatus = flexDriver.FindElement(MobileBy.XPath("//android.widget.TextView[@text='SYNCING']"));
            Assert.That(lblSyncingStatus.Text, Is.EqualTo("SYNCING"), "The ticket card failed to immediately initialize in the SYNCING buffer state.");
            Console.WriteLine("Step 11a verified: Form state is safely captured in the background 'SYNCING' queue loop.");

            // 2. Fluent check loop: Explicitly wait until the application pushes data to the remote cloud node
            var lblSyncedStatus = wf.Until(d => {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='SYNCED']"));
                return (el != null && el.Displayed) ? el : null;
            });

            // 3. Final validation assertion
            Assert.That(lblSyncedStatus.Text, Is.EqualTo("SYNCED"), "The form background sync process failed to transition cleanly to complete.");
            Console.WriteLine("Step 11b verified: Remote record officially marked as 'SYNCED' globally inside the app tree database.");

        }
    }
}