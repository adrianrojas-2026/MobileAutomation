using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Tests;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium;

namespace RecoveryTrac.AutomatedTests
{
    /// <summary>
    /// Atomic regression tests derived from the Load Collection E2E scenario.
    /// Each test covers exactly one concern and can run independently.
    /// SetUp navigates only as far as the step under test requires.
    /// </summary>
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class LoadCollectionFormRegressionTests : BaseTest
    {
        private readonly ThreadLocal<WebDriverWait> _wait = new();

        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        [SetUp]
        public void SetupScenario()
        {
            DriverFactory.InitializeDualApps();
            _wait.Value = new WebDriverWait(DriverFactory.DriverFlex, TimeSpan.FromSeconds(15));
        }

        [OneTimeTearDown]
        public void FinalizeClassDisposal()
        {
            Console.WriteLine("Disposing ThreadLocal wait resources...");
            _wait?.Dispose();
        }

        [TearDown]
        public void ClearCameraBuffer()
        {
            try
            {
                if (DriverFactory.DriverFlex is IJavaScriptExecutor jsExecutor)
                {
                    var clearArgs = new Dictionary<string, object> { { "payload", "" } };
                    jsExecutor.ExecuteScript("mobile: injectEmulatorCameraImage", clearArgs);
                }

            }
            catch { /* Suppress non-blocking cleanup errors */ }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Shared navigation helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Navigates the Flex App to the Load-Collection form list page.</summary>
        private void NavigateToLoadCollectionList()
        {
            var wf = _wait.Value!;
            var driver = DriverFactory.DriverFlex;

            var formsTab = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Forms'] | //*[@content-desc='Forms']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });
            formsTab.Click();

            var loadCollectionOption = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Load - Collection']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });
            loadCollectionOption.Click();
        }

        /// <summary>Navigates to the new form page and opens the QR scan overlay.</summary>
        private void NavigateToNewFormQrScan()
        {
            NavigateToLoadCollectionList();

            var wf = _wait.Value!;

            var newFormButton = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.Button[@text='New'] | //android.widget.TextView[@text='New']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });
            newFormButton.Click();

            var scanButton = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Scan Truck QR Code'] | //android.widget.Button[contains(@text,'Scan')]"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });
            scanButton.Click();
        }

        /// <summary>Injects the truck placard QR image into the emulator camera.</summary>
        private async Task InjectTruckPlacardQr()
        {
            string testImagePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "TestData", "truck_placard_qr.png");

            if (!File.Exists(testImagePath))
                throw new FileNotFoundException($"Missing mock truck placard file at: {testImagePath}");

            byte[] imageBytes = await File.ReadAllBytesAsync(testImagePath);
            string base64 = Convert.ToBase64String(imageBytes);

            var cameraArgs = new Dictionary<string, object> { { "payload", base64 } };
            // 💡 Declare and instantiate jsExecutor right inside the type check expression
            if (DriverFactory.DriverFlex is IJavaScriptExecutor jsExecutor)
            {
                jsExecutor.ExecuteScript("mobile: injectEmulatorCameraImage", cameraArgs);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LC-001 — Forms tab navigates to the Load-Collection list
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("Navigation")]
        public void TC_LC_001_FormsTab_DisplaysLoadCollectionOption()
        {
            var wf = _wait.Value!;

            var formsTab = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Forms'] | //*[@content-desc='Forms']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });
            formsTab.Click();

            var loadCollectionOption = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Load - Collection']"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(loadCollectionOption.Displayed, Is.True,
                "Load - Collection option should be visible after tapping the Forms tab.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LC-002 — Load-Collection list page shows the In Progress header
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("Navigation")]
        public void TC_LC_002_LoadCollectionListPage_ShowsInProgressHeader()
        {
            NavigateToLoadCollectionList();

            var wf = _wait.Value!;

            var header = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[contains(@text,'In Progress') and contains(@text,'Load - Collection')]"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(header.Text, Contains.Substring("Load - Collection"),
                "In Progress header should reference 'Load - Collection'.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LC-003 — New button opens the QR scan overlay
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("Navigation")]
        public void TC_LC_003_NewButton_OpensScanOverlay()
        {
            NavigateToLoadCollectionList();

            var wf = _wait.Value!;
            var driver = DriverFactory.DriverFlex;

            var newFormButton = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.Button[@text='New'] | //android.widget.TextView[@text='New']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });
            newFormButton.Click();

            var scanButton = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Scan Truck QR Code'] | //android.widget.Button[contains(@text,'Scan')]"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(scanButton.Displayed, Is.True,
                "Scan Truck QR Code button should appear after tapping New.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LC-004 — QR scan success toast shows correct message
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("QRScan")]
        public async Task TC_LC_004_QrScan_ShowsSuccessToast()
        {
            NavigateToNewFormQrScan();
            await InjectTruckPlacardQr();

            var wf = _wait.Value!;

            var successToast = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[contains(@text,'scanned successfully')]"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(successToast.Text,
                Is.EqualTo("The QR code was scanned successfully, and 4 controls were populated."),
                "Success toast text mismatch after QR scan.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LC-005 — QR scan populates Truck Number field
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("QRScan")]
        public async Task TC_LC_005_QrScan_PopulatesTruckNumber()
        {
            NavigateToNewFormQrScan();
            await InjectTruckPlacardQr();

            var wf = _wait.Value!;

            var truckNumber = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.EditText[contains(@resource-id,'TruckNumber') or contains(@content-desc,'Truck Number')]"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(truckNumber.Text, Is.Not.Null.And.Not.Empty,
                "Truck Number field should be populated after a successful QR scan.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LC-006 — QR scan populates Driver Name field
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("QRScan")]
        public async Task TC_LC_006_QrScan_PopulatesDriverName()
        {
            NavigateToNewFormQrScan();
            await InjectTruckPlacardQr();

            var wf = _wait.Value!;
            // Wait for the QR toast to confirm data binding has completed
            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[contains(@text,'scanned successfully')]"));
                return el != null && el.Displayed ? el : null;
            });

            var driverName = DriverFactory.DriverFlex.FindElement(MobileBy.XPath(
                "//android.widget.EditText[contains(@resource-id,'DriverName') or contains(@content-desc,'Driver Name')]"));

            Assert.That(driverName.Text, Is.Not.Null.And.Not.Empty,
                "Driver Name field should be populated after a successful QR scan.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LC-007 — QR scan populates Truck Capacity field
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("QRScan")]
        public async Task TC_LC_007_QrScan_PopulatesTruckCapacity()
        {
            NavigateToNewFormQrScan();
            await InjectTruckPlacardQr();

            var wf = _wait.Value!;
            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[contains(@text,'scanned successfully')]"));
                return el != null && el.Displayed ? el : null;
            });

            var truckCapacity = DriverFactory.DriverFlex.FindElement(MobileBy.XPath(
                "//android.widget.EditText[contains(@resource-id,'TruckCapacity') or contains(@content-desc,'Truck Capacity')]"));

            Assert.That(truckCapacity.Text, Is.Not.Null.And.Not.Empty,
                "Truck Capacity field should be populated after a successful QR scan.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LC-008 — QR scan populates Truck Driver Name field
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("QRScan")]
        public async Task TC_LC_008_QrScan_PopulatesTruckDriverName()
        {
            NavigateToNewFormQrScan();
            await InjectTruckPlacardQr();

            var wf = _wait.Value!;
            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[contains(@text,'scanned successfully')]"));
                return el != null && el.Displayed ? el : null;
            });

            var truckDriverName = DriverFactory.DriverFlex.FindElement(MobileBy.XPath(
                "//android.widget.EditText[contains(@resource-id,'TruckDriverName') or contains(@content-desc,'Truck Driver Name')]"));

            Assert.That(truckDriverName.Text, Is.Not.Null.And.Not.Empty,
                "Truck Driver Name field should be populated after a successful QR scan.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LC-009 — QR scan populates Truck License Plate field
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("QRScan")]
        public async Task TC_LC_009_QrScan_PopulatesTruckLicensePlate()
        {
            NavigateToNewFormQrScan();
            await InjectTruckPlacardQr();

            var wf = _wait.Value!;
            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[contains(@text,'scanned successfully')]"));
                return el != null && el.Displayed ? el : null;
            });

            var licensePlate = DriverFactory.DriverFlex.FindElement(MobileBy.XPath(
                "//android.widget.EditText[contains(@resource-id,'LicensePlate') or contains(@content-desc,'Truck License Plate')]"));

            Assert.That(licensePlate.Text, Is.Not.Null.And.Not.Empty,
                "Truck License Plate field should be populated after a successful QR scan.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LC-010 — "Is Driver Name correct?" radio — Yes is selectable
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("RadioButtons")]
        public async Task TC_LC_010_IsDriverNameCorrect_YesRadio_IsSelectableAndEnabled()
        {
            NavigateToNewFormQrScan();
            await InjectTruckPlacardQr();

            var wf = _wait.Value!;
            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[contains(@text,'scanned successfully')]"));
                return el != null && el.Displayed ? el : null;
            });

            var yesRadio = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//*[contains(@text,'Is Driver Name correct')]/following-sibling::*//*[@text='Yes' or @content-desc='Yes']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });

            Assert.That(yesRadio.Enabled, Is.True,
                "'Yes' radio for 'Is Driver Name correct?' should be enabled.");
            yesRadio.Click();
            // Re-find to check selected state
            var yesAfterClick = DriverFactory.DriverFlex.FindElement(MobileBy.XPath(
                "//*[contains(@text,'Is Driver Name correct')]/following-sibling::*//*[@text='Yes' or @content-desc='Yes']"));
            Assert.That(yesAfterClick.Selected || yesAfterClick.GetAttribute("checked") == "true", Is.True,
                "'Yes' radio should reflect a selected state after tapping.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LC-011 — "Is Driver Name correct?" radio — No is selectable
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("RadioButtons")]
        public async Task TC_LC_011_IsDriverNameCorrect_NoRadio_IsSelectableAndEnabled()
        {
            NavigateToNewFormQrScan();
            await InjectTruckPlacardQr();

            var wf = _wait.Value!;
            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[contains(@text,'scanned successfully')]"));
                return el != null && el.Displayed ? el : null;
            });

            var noRadio = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//*[contains(@text,'Is Driver Name correct')]/following-sibling::*//*[@text='No' or @content-desc='No']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });

            Assert.That(noRadio.Enabled, Is.True,
                "'No' radio for 'Is Driver Name correct?' should be enabled.");
            noRadio.Click();
            var noAfterClick = DriverFactory.DriverFlex.FindElement(MobileBy.XPath(
                "//*[contains(@text,'Is Driver Name correct')]/following-sibling::*//*[@text='No' or @content-desc='No']"));
            Assert.That(noAfterClick.Selected || noAfterClick.GetAttribute("checked") == "true", Is.True,
                "'No' radio should reflect a selected state after tapping.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LC-012 — "Is the above information correct?" radio — Yes is selectable
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("RadioButtons")]
        public async Task TC_LC_012_IsInfoCorrect_YesRadio_IsSelectableAndEnabled()
        {
            NavigateToNewFormQrScan();
            await InjectTruckPlacardQr();

            var wf = _wait.Value!;
            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[contains(@text,'scanned successfully')]"));
                return el != null && el.Displayed ? el : null;
            });

            var yesRadio = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//*[contains(@text,'Is the above information correct')]/following-sibling::*//*[@text='Yes' or @content-desc='Yes']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });

            Assert.That(yesRadio.Enabled, Is.True,
                "'Yes' radio for 'Is the above information correct?' should be enabled.");
            yesRadio.Click();
            var yesAfterClick = DriverFactory.DriverFlex.FindElement(MobileBy.XPath(
                "//*[contains(@text,'Is the above information correct')]/following-sibling::*//*[@text='Yes' or @content-desc='Yes']"));
            Assert.That(yesAfterClick.Selected || yesAfterClick.GetAttribute("checked") == "true", Is.True,
                "'Yes' radio should reflect a selected state after tapping.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LC-013 — "Is the above information correct?" radio — No is selectable
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("RadioButtons")]
        public async Task TC_LC_013_IsInfoCorrect_NoRadio_IsSelectableAndEnabled()
        {
            NavigateToNewFormQrScan();
            await InjectTruckPlacardQr();

            var wf = _wait.Value!;
            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[contains(@text,'scanned successfully')]"));
                return el != null && el.Displayed ? el : null;
            });

            var noRadio = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//*[contains(@text,'Is the above information correct')]/following-sibling::*//*[@text='No' or @content-desc='No']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });

            Assert.That(noRadio.Enabled, Is.True,
                "'No' radio for 'Is the above information correct?' should be enabled.");
            noRadio.Click();
            var noAfterClick = DriverFactory.DriverFlex.FindElement(MobileBy.XPath(
                "//*[contains(@text,'Is the above information correct')]/following-sibling::*//*[@text='No' or @content-desc='No']"));
            Assert.That(noAfterClick.Selected || noAfterClick.GetAttribute("checked") == "true", Is.True,
                "'No' radio should reflect a selected state after tapping.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LC-014 — Radio buttons are mutually exclusive within each group
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("RadioButtons")]
        public async Task TC_LC_014_RadioGroups_AreMutuallyExclusive()
        {
            NavigateToNewFormQrScan();
            await InjectTruckPlacardQr();

            var wf = _wait.Value!;
            var driver = DriverFactory.DriverFlex;

            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[contains(@text,'scanned successfully')]"));
                return el != null && el.Displayed ? el : null;
            });

            // Select Yes for "Is Driver Name correct?"
            var yesRadio = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//*[contains(@text,'Is Driver Name correct')]/following-sibling::*//*[@text='Yes' or @content-desc='Yes']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });
            yesRadio.Click();

            // Now select No — Yes must deselect
            var noRadio = driver.FindElement(MobileBy.XPath(
                "//*[contains(@text,'Is Driver Name correct')]/following-sibling::*//*[@text='No' or @content-desc='No']"));
            noRadio.Click();

            var yesAfterNo = driver.FindElement(MobileBy.XPath(
                "//*[contains(@text,'Is Driver Name correct')]/following-sibling::*//*[@text='Yes' or @content-desc='Yes']"));

            Assert.Multiple(() =>
            {
                Assert.That(yesAfterNo.Selected || yesAfterNo.GetAttribute("checked") == "true", Is.False,
                    "'Yes' should be deselected after tapping 'No' in the same radio group.");
                Assert.That(noRadio.Selected || noRadio.GetAttribute("checked") == "true", Is.True,
                    "'No' should be selected.");
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LC-015 — Truck Debris Type dropdown shows selectable options
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("Dropdowns")]
        public async Task TC_LC_015_TruckDebrisType_Dropdown_ShowsOptions()
        {
            NavigateToNewFormQrScan();
            await InjectTruckPlacardQr();

            var wf = _wait.Value!;
            var driver = DriverFactory.DriverFlex;

            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[contains(@text,'scanned successfully')]"));
                return el != null && el.Displayed ? el : null;
            });

            var dropdown = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Truck Debris Type *']/following-sibling::android.view.ViewGroup"));
                return (el != null && el.Displayed) ? el : null;
            });
            dropdown.Click();

            var vegetativeOption = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Vegetative/Woody']"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(vegetativeOption.Displayed, Is.True,
                "'Vegetative/Woody' should be visible in the Truck Debris Type dropdown.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LC-016 — Truck Debris Type accepts a valid selection
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("Dropdowns")]
        public async Task TC_LC_016_TruckDebrisType_AcceptsValidSelection()
        {
            NavigateToNewFormQrScan();
            await InjectTruckPlacardQr();

            var wf = _wait.Value!;
            var driver = DriverFactory.DriverFlex;

            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[contains(@text,'scanned successfully')]"));
                return el != null && el.Displayed ? el : null;
            });

            var dropdown = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Truck Debris Type *']/following-sibling::android.view.ViewGroup"));
                return (el != null && el.Displayed) ? el : null;
            });
            dropdown.Click();

            var vegetativeOption = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Vegetative/Woody']"));
                return el != null && el.Displayed ? el : null;
            });
            vegetativeOption.Click();

            var doneButton = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.Button[@text='Done'] | //android.widget.TextView[@text='Done']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });
            doneButton.Click();

            // After Done the selected value should be reflected in the dropdown label
            var selectedLabel = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Vegetative/Woody']"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(selectedLabel.Text, Is.EqualTo("Vegetative/Woody"),
                "Truck Debris Type should display the selected value after closing the picker.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LC-017 — Trailer Debris Type dropdown shows selectable options
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("Dropdowns")]
        public async Task TC_LC_017_TrailerDebrisType_Dropdown_ShowsOptions()
        {
            NavigateToNewFormQrScan();
            await InjectTruckPlacardQr();

            var wf = _wait.Value!;
            var driver = DriverFactory.DriverFlex;

            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[contains(@text,'scanned successfully')]"));
                return el != null && el.Displayed ? el : null;
            });

            var dropdown = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Trailer Debris Type *']/following-sibling::android.view.ViewGroup"));
                return (el != null && el.Displayed) ? el : null;
            });
            dropdown.Click();

            var constructionOption = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Construction & Demolition']"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(constructionOption.Displayed, Is.True,
                "'Construction & Demolition' should be visible in the Trailer Debris Type dropdown.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LC-018 — Trailer Debris Type accepts a valid selection
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("Dropdowns")]
        public async Task TC_LC_018_TrailerDebrisType_AcceptsValidSelection()
        {
            NavigateToNewFormQrScan();
            await InjectTruckPlacardQr();

            var wf = _wait.Value!;
            var driver = DriverFactory.DriverFlex;

            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[contains(@text,'scanned successfully')]"));
                return el != null && el.Displayed ? el : null;
            });

            var dropdown = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Trailer Debris Type *']/following-sibling::android.view.ViewGroup"));
                return (el != null && el.Displayed) ? el : null;
            });
            dropdown.Click();

            var constructionOption = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Construction & Demolition']"));
                return el != null && el.Displayed ? el : null;
            });
            constructionOption.Click();

            var doneButton = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.Button[@text='Done'] | //android.widget.TextView[@text='Done']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });
            doneButton.Click();

            var selectedLabel = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Construction & Demolition']"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(selectedLabel.Text, Is.EqualTo("Construction & Demolition"),
                "Trailer Debris Type should display the selected value after closing the picker.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LC-019 — Next button is present and navigates away from the form page
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("Navigation")]
        public async Task TC_LC_019_NextButton_NavigatesToLocationPage()
        {
            NavigateToNewFormQrScan();
            await InjectTruckPlacardQr();

            var wf = _wait.Value!;
            var driver = DriverFactory.DriverFlex;

            // Wait for QR data binding
            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[contains(@text,'scanned successfully')]"));
                return el != null && el.Displayed ? el : null;
            });

            // Confirm both radio questions — required before Next
            var driverNameYes = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//*[contains(@text,'Is Driver Name correct')]/following-sibling::*//*[@text='Yes' or @content-desc='Yes']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });
            driverNameYes.Click();

            var infoCorrectYes = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//*[contains(@text,'Is the above information correct')]/following-sibling::*//*[@text='Yes' or @content-desc='Yes']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });
            infoCorrectYes.Click();

            // Select both debris types (required fields)
            var truckDropdown = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Truck Debris Type *']/following-sibling::android.view.ViewGroup"));
                return (el != null && el.Displayed) ? el : null;
            });
            truckDropdown.Click();
            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Vegetative/Woody']"));
                return el != null && el.Displayed ? el : null;
            }).Click();
            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.Button[@text='Done'] | //android.widget.TextView[@text='Done']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            }).Click();

            var trailerDropdown = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Trailer Debris Type *']/following-sibling::android.view.ViewGroup"));
                return (el != null && el.Displayed) ? el : null;
            });
            trailerDropdown.Click();
            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Construction & Demolition']"));
                return el != null && el.Displayed ? el : null;
            }).Click();
            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.Button[@text='Done'] | //android.widget.TextView[@text='Done']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            }).Click();

            // Tap Next
            var nextButton = driver.FindElement(MobileBy.XPath(
                "//android.widget.Button[@text='Next'] | //android.widget.TextView[@text='Next']"));
            nextButton.Click();

            // The Location page is confirmed when the GPS/address section appears
            var locationIndicator = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.EditText[contains(@resource-id,'AddressNumber')] | //android.widget.EditText[@text='6534']"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(locationIndicator.Displayed, Is.True,
                "The Location page should be visible after tapping Next on the form page.");
        }
    }
}