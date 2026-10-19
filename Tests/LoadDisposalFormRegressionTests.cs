using OpenQA.Selenium;
using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Tests;
using MobileAutomation.Tests.Pages.Driver;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Appium.Android;

namespace RecoveryTrac.AutomatedTests
{
    /// <summary>
    /// Atomic regression tests derived from the Load Disposal E2E scenario
    /// (Test_LoadDisposal_To_ReceiptSync_Complete_Parallel_E2E).
    /// Each test covers exactly one concern and navigates only as far as needed.
    /// Both drivers are always initialized to match the existing dual-app setup pattern.
    /// </summary>
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    public class LoadDisposalFormRegressionTests : BaseTest
    {
        private readonly ThreadLocal<WebDriverWait> _waitFlex   = new();
        private readonly ThreadLocal<WebDriverWait> _waitDriver = new();

        // ─────────────────────────────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        [SetUp]
        public void SetupScenario()
        {
            DriverFactory.InitializeDualApps();
            _waitFlex.Value   = new WebDriverWait(DriverFactory.DriverFlex,   TimeSpan.FromSeconds(15));
            _waitDriver.Value = new WebDriverWait(DriverFactory.DriverDriver, TimeSpan.FromSeconds(15));
        }

        [OneTimeTearDown]
        public void FinalizeClassDisposal()
        {
            Console.WriteLine("Disposing ThreadLocal wait resources...");
            _waitFlex?.Dispose();
            _waitDriver?.Dispose();
        }

        [TearDown]
        public void ClearCameraBuffer()
        {
            try
            {
                // 💡 Safely type check that the driver supports script execution prior to invoking emulated hardware parameters
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

        /// <summary>Navigates the Flex App to the Load-Disposal form list page.</summary>
        private void NavigateToLoadDisposalList()
        {
            var wf = _waitFlex.Value!;

            var formsTab = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Forms'] | //*[@content-desc='Forms']"));
                return (el != null && el.Displayed) ? el : null;
            });
            formsTab.Click();

            var loadDisposalOption = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Load - Disposal']"));
                return (el != null && el.Displayed) ? el : null;
            });
            loadDisposalOption.Click();
        }

        /// <summary>Navigates to the new Disposal form (Disposal Site page).</summary>
        private void NavigateToNewDisposalForm()
        {
            NavigateToLoadDisposalList();

            var wf = _waitFlex.Value!;

            var btnNew = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.Button[@text='New'] | //android.widget.TextView[@text='New']"));
                return (el != null && el.Displayed) ? el : null;
            });
            btnNew.Click();
        }

        /// <summary>
        /// Navigates past the Disposal Site page (asserts pre-selection, taps Next)
        /// to reach the Ticket Scan page.
        /// </summary>
        private void NavigateToTicketScanPage()
        {
            NavigateToNewDisposalForm();

            var wf      = _waitFlex.Value!;
            var flex    = DriverFactory.DriverFlex;

            // Wait for the disposal site page to load
            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Disposal Site *']/following-sibling::android.view.ViewGroup " +
                    "| //android.widget.EditText[contains(@text,'LANDFILL')]"));
                return el != null && el.Displayed ? el : null;
            });

            flex.FindElement(MobileBy.XPath(
                "//android.widget.Button[@text='Next'] | //android.widget.TextView[@text='Next']")).Click();
        }

        /// <summary>
        /// Uses the "having trouble scanning" fallback path to select ticket 6L82-LC-100G,
        /// stopping just after the ticket is selected (before the Driver QR injection step).
        /// </summary>
        private void SelectTicketViaTroubleScanning()
        {
            NavigateToTicketScanPage();

            var wf = _waitFlex.Value!;

            var troubleLink = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='having trouble scanning']/following-sibling::android.view.ViewGroup " +
                    "| //android.widget.TextView[@text='having trouble scanning']"));
                return el != null && el.Displayed ? el : null;
            });
            troubleLink.Click();

            var ticketDropdown = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Search for Collection Ticket *']/following-sibling::android.view.ViewGroup"));
                return el != null && el.Displayed ? el : null;
            });
            ticketDropdown.Click();

            var targetTicket = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='6L82-LC-100G']"));
                return el != null && el.Displayed ? el : null;
            });
            targetTicket.Click();
        }

        /// <summary>
        /// Completes the Driver App QR injection and taps Next, leaving the test
        /// on the Truck Measurements page ready for field-level assertions.
        /// </summary>
        private void ReachMeasurementsPage()
        {
            SelectTicketViaTroubleScanning();

            var wf         = _waitFlex.Value!;
            var flex       = DriverFactory.DriverFlex;
            var driverPage = new DriverAppPage(DriverFactory.DriverDriver);

            driverPage.GenerateTicket();
            string qrBase64 = driverPage.GetOnlyQrCodeAsBase64();

            var scanBtn = flex.FindElement(MobileBy.XPath(
                "//android.widget.Button[contains(@text,'Scan the driver')] | //android.widget.TextView[contains(@text,'Scan')]"));
            scanBtn.Click();

            if (flex is IJavaScriptExecutor jsExecutor)
            {
                jsExecutor.ExecuteScript("mobile: injectEmulatorCameraImage",
                    new Dictionary<string, object> { { "payload", qrBase64 } });
            }

            // Tap Next to proceed to measurements
            var nextBtn = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.Button[@text='Next'] | //android.widget.TextView[@text='Next']"));
                return (el != null && el.Displayed) ? el : null;
            });
            nextBtn.Click();
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LD-001 — Forms tab displays Load-Disposal option
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("Navigation")]
        public void TC_LD_001_FormsTab_DisplaysLoadDisposalOption()
        {
            var wf = _waitFlex.Value!;

            var formsTab = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Forms'] | //*[@content-desc='Forms']"));
                return (el != null && el.Displayed) ? el : null;
            });
            formsTab.Click();

            var loadDisposalOption = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Load - Disposal']"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(loadDisposalOption.Displayed, Is.True,
                "Load - Disposal option should be visible after tapping the Forms tab.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LD-002 — Load-Disposal list page shows the correct header
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("Navigation")]
        public void TC_LD_002_LoadDisposalListPage_ShowsCorrectHeader()
        {
            NavigateToLoadDisposalList();

            var wf = _waitFlex.Value!;

            var header = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[contains(@text,'Load - Disposal')]"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(header.Text, Contains.Substring("Load - Disposal"),
                "The list page header should reference 'Load - Disposal'.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LD-003 — New button opens the Disposal Site page
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("Navigation")]
        public void TC_LD_003_NewButton_OpensDisposalSitePage()
        {
            NavigateToLoadDisposalList();

            var wf = _waitFlex.Value!;

            var btnNew = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.Button[@text='New'] | //android.widget.TextView[@text='New']"));
                return (el != null && el.Displayed && el.Enabled) ? el : null;
            });
            btnNew.Click();

            var disposalSiteField = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Disposal Site *']/following-sibling::android.view.ViewGroup " +
                    "| //android.widget.EditText[contains(@text,'LANDFILL')]"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(disposalSiteField.Displayed, Is.True,
                "Disposal Site field should be visible after tapping New.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LD-004 — Disposal Site is pre-selected with ORANGE COUNTY LANDFILL
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("DisposalSite")]
        public void TC_LD_004_DisposalSite_IsPreselectedWithOrangeCountyLandfill()
        {
            NavigateToNewDisposalForm();

            var wf = _waitFlex.Value!;

            var disposalSiteField = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Disposal Site *']/following-sibling::android.view.ViewGroup " +
                    "| //android.widget.EditText[contains(@text,'LANDFILL')]"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(disposalSiteField.Text, Is.EqualTo("ORANGE COUNTY LANDFILL"),
                "Disposal Site should be pre-selected with 'ORANGE COUNTY LANDFILL'.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LD-005 — Next on Disposal Site page navigates to the Ticket Scan page
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("Navigation")]
        public void TC_LD_005_DisposalSiteNextButton_NavigatesToTicketScanPage()
        {
            NavigateToNewDisposalForm();

            var wf   = _waitFlex.Value!;
            var flex = DriverFactory.DriverFlex;

            // Confirm the disposal site page loaded
            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Disposal Site *']/following-sibling::android.view.ViewGroup " +
                    "| //android.widget.EditText[contains(@text,'LANDFILL')]"));
                return el != null && el.Displayed ? el : null;
            });

            flex.FindElement(MobileBy.XPath(
                "//android.widget.Button[@text='Next'] | //android.widget.TextView[@text='Next']")).Click();

            // The Ticket Scan page is confirmed by the "having trouble scanning" link
            var troubleLink = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='having trouble scanning']"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(troubleLink.Displayed, Is.True,
                "The Ticket Scan page should appear after tapping Next on the Disposal Site page.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LD-006 — "Having trouble scanning" link is visible on the Ticket Scan page
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("TicketFallback")]
        public void TC_LD_006_HavingTroubleScanning_LinkIsVisible()
        {
            NavigateToTicketScanPage();

            var wf = _waitFlex.Value!;

            var troubleLink = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='having trouble scanning']"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(troubleLink.Displayed, Is.True,
                "'having trouble scanning' link should be visible on the Ticket Scan page.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LD-007 — Tapping "Having trouble scanning" reveals the ticket search dropdown
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("TicketFallback")]
        public void TC_LD_007_HavingTroubleScanning_RevealsTicketSearchDropdown()
        {
            NavigateToTicketScanPage();

            var wf = _waitFlex.Value!;

            var troubleLink = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='having trouble scanning']/following-sibling::android.view.ViewGroup " +
                    "| //android.widget.TextView[@text='having trouble scanning']"));
                return el != null && el.Displayed ? el : null;
            });
            troubleLink.Click();

            var ticketDropdown = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Search for Collection Ticket *']/following-sibling::android.view.ViewGroup"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(ticketDropdown.Displayed, Is.True,
                "The 'Search for Collection Ticket' dropdown should appear after tapping 'having trouble scanning'.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LD-008 — Ticket search dropdown shows available tickets
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("TicketFallback")]
        public void TC_LD_008_TicketSearchDropdown_ShowsAvailableTickets()
        {
            NavigateToTicketScanPage();

            var wf = _waitFlex.Value!;

            var troubleLink = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='having trouble scanning']/following-sibling::android.view.ViewGroup " +
                    "| //android.widget.TextView[@text='having trouble scanning']"));
                return el != null && el.Displayed ? el : null;
            });
            troubleLink.Click();

            var ticketDropdown = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Search for Collection Ticket *']/following-sibling::android.view.ViewGroup"));
                return el != null && el.Displayed ? el : null;
            });
            ticketDropdown.Click();

            // The expected ticket must appear in the list
            var targetTicket = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='6L82-LC-100G']"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(targetTicket.Displayed, Is.True,
                "Ticket '6L82-LC-100G' should be visible in the ticket search dropdown list.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LD-009 — Selecting a ticket from the dropdown confirms the selection
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("TicketFallback")]
        public void TC_LD_009_TicketDropdown_ConfirmsSelection()
        {
            NavigateToTicketScanPage();

            var wf = _waitFlex.Value!;

            var troubleLink = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='having trouble scanning']/following-sibling::android.view.ViewGroup " +
                    "| //android.widget.TextView[@text='having trouble scanning']"));
                return el != null && el.Displayed ? el : null;
            });
            troubleLink.Click();

            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Search for Collection Ticket *']/following-sibling::android.view.ViewGroup"));
                return el != null && el.Displayed ? el : null;
            }).Click();

            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='6L82-LC-100G']"));
                return el != null && el.Displayed ? el : null;
            }).Click();

            // After selection the ticket ID should be reflected in the dropdown field
            var selectedTicket = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='6L82-LC-100G'] | //android.widget.EditText[@text='6L82-LC-100G']"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(selectedTicket.Text, Is.EqualTo("6L82-LC-100G"),
                "The selected ticket ID should be reflected in the dropdown after selection.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LD-010 — Driver App renders QR code after GenerateTicket()
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("DriverAppQR")]
        public void TC_LD_010_DriverApp_RendersQrCodeAfterGenerateTicket()
        {
            var driverPage = new DriverAppPage(DriverFactory.DriverDriver);

            driverPage.GenerateTicket();

            Assert.That(driverPage.IsQrCodeRendered(timeoutSeconds: 5), Is.True,
                "Driver App should render the verification QR code canvas after GenerateTicket().");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LD-011 — "Scan the driver's QR" button is visible after ticket selection
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("DriverAppQR")]
        public void TC_LD_011_ScanDriverQrButton_IsVisibleAfterTicketSelection()
        {
            SelectTicketViaTroubleScanning();

            var wf   = _waitFlex.Value!;
            var flex = DriverFactory.DriverFlex;

            var scanBtn = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.Button[contains(@text,'Scan the driver')] " +
                    "| //android.widget.TextView[contains(@text,'Scan')]"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(scanBtn.Displayed, Is.True,
                "'Scan the driver's QR' button should be visible after a ticket is selected.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LD-012 — Injecting Driver QR transitions to the Measurements page
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("DriverAppQR")]
        public void TC_LD_012_DriverQrInjection_TransitionsToMeasurementsPage()
        {
            SelectTicketViaTroubleScanning();

            var wf         = _waitFlex.Value!;
            var flex       = DriverFactory.DriverFlex;
            var driverPage = new DriverAppPage(DriverFactory.DriverDriver);

            driverPage.GenerateTicket();
            string qrBase64 = driverPage.GetOnlyQrCodeAsBase64();

            var scanBtn = flex.FindElement(MobileBy.XPath(
                "//android.widget.Button[contains(@text,'Scan the driver')] | //android.widget.TextView[contains(@text,'Scan')]"));
            scanBtn.Click();

            if (flex is IJavaScriptExecutor jsExecutor)
            {
                jsExecutor.ExecuteScript("mobile: injectEmulatorCameraImage",
                    new Dictionary<string, object> { { "payload", qrBase64 } });
            }

            var nextBtn = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.Button[@text='Next'] | //android.widget.TextView[@text='Next']"));
                return (el != null && el.Displayed) ? el : null;
            });
            nextBtn.Click();

            // Measurements page is confirmed by the Truck Capacity field
            var truckCapacityField = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.EditText[@text='57']"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(truckCapacityField.Displayed, Is.True,
                "Measurements page should be visible after the Driver QR is successfully injected.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LD-013 — Truck Capacity is autofilled correctly from Driver QR data
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("Measurements")]
        public void TC_LD_013_TruckCapacity_IsAutofilled()
        {
            ReachMeasurementsPage();

            var wf = _waitFlex.Value!;

            var truckCapacity = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.EditText[@text='57']"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(truckCapacity.Text, Is.EqualTo("57"),
                "Truck Capacity should be autofilled with '57' from the Driver QR data.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LD-014 — Debris Class is autofilled correctly from Driver QR data
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("Measurements")]
        public void TC_LD_014_DebrisClass_IsAutofilled()
        {
            ReachMeasurementsPage();

            var flex = DriverFactory.DriverFlex;

            var debrisClass = flex.FindElement(MobileBy.XPath(
                "//android.widget.EditText[@text='VEGETATIVE/WOODY']"));

            Assert.That(debrisClass.Text, Is.EqualTo("VEGETATIVE/WOODY"),
                "Debris Class should be autofilled with 'VEGETATIVE/WOODY' from the Driver QR data.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LD-015 — "Is the capacity correct?" Yes radio is selectable
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("Measurements")]
        public void TC_LD_015_IsCapacityCorrect_YesRadio_IsSelectableAndEnabled()
        {
            ReachMeasurementsPage();

            var flex = DriverFactory.DriverFlex;

            var yesRadio = flex.FindElement(MobileBy.XPath(
                "//android.widget.TextView[@text='Yes']"));

            Assert.That(yesRadio.Enabled, Is.True,
                "'Yes' radio for capacity confirmation should be enabled.");

            yesRadio.Click();

            var yesAfterClick = flex.FindElement(MobileBy.XPath(
                "//android.widget.TextView[@text='Yes']"));

            Assert.That(
                yesAfterClick.Selected || yesAfterClick.GetAttribute("checked") == "true",
                Is.True,
                "'Yes' radio should reflect a selected state after tapping.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LD-016 — Load Call % field accepts numeric input
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("Measurements")]
        public void TC_LD_016_LoadCallPercent_AcceptsNumericInput()
        {
            ReachMeasurementsPage();

            var flex = DriverFactory.DriverFlex;

            // Confirm Yes first (may be required to enable Load Call field)
            flex.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Yes']")).Click();

            var loadCallField = flex.FindElement(MobileBy.XPath(
                "//android.widget.EditText[contains(@resource-id,'loadCall')] | //android.widget.EditText[@text='']"));
            loadCallField.Click();
            loadCallField.SendKeys("75");

            flex.HideKeyboardIfShown();

            Assert.That(loadCallField.Text, Is.EqualTo("75"),
                "Load Call % field should hold the value '75' after input.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LD-017 — Submit button is present after required fields are filled
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("Submit")]
        public void TC_LD_017_SubmitButton_IsPresentAfterRequiredFieldsFilled()
        {
            ReachMeasurementsPage();

            var flex = DriverFactory.DriverFlex;

            flex.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Yes']")).Click();

            var loadCallField = flex.FindElement(MobileBy.XPath(
                "//android.widget.EditText[contains(@resource-id,'loadCall')] | //android.widget.EditText[@text='']"));
            loadCallField.Click();
            loadCallField.SendKeys("75");
            flex.HideKeyboardIfShown();

            var submitButton = flex.FindElement(MobileBy.XPath(
                "//android.widget.Button[@text='Submit'] | //android.widget.TextView[@text='Submit']"));

            Assert.That(submitButton.Displayed, Is.True,
                "Submit button should be present and visible after all required fields are filled.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LD-018 — Generate QR Code modal appears after Submit
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("Submit")]
        public void TC_LD_018_GenerateQrCodeModal_AppearsAfterSubmit()
        {
            ReachMeasurementsPage();

            var wf   = _waitFlex.Value!;
            var flex = DriverFactory.DriverFlex;

            flex.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Yes']")).Click();

            var loadCallField = flex.FindElement(MobileBy.XPath(
                "//android.widget.EditText[contains(@resource-id,'loadCall')] | //android.widget.EditText[@text='']"));
            loadCallField.Click();
            loadCallField.SendKeys("75");
            flex.HideKeyboardIfShown();

            flex.FindElement(MobileBy.XPath(
                "//android.widget.Button[@text='Submit'] | //android.widget.TextView[@text='Submit']")).Click();

            var modalTitle = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Generate QR Code']"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(modalTitle.Text, Is.EqualTo("Generate QR Code"),
                "The 'Generate QR Code' modal should appear after tapping Submit.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LD-019 — Generate QR Code modal has Proceed and Cancel buttons
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("Submit")]
        public void TC_LD_019_GenerateQrCodeModal_HasProceedAndCancelButtons()
        {
            ReachMeasurementsPage();

            var wf   = _waitFlex.Value!;
            var flex = DriverFactory.DriverFlex;

            flex.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Yes']")).Click();

            var loadCallField = flex.FindElement(MobileBy.XPath(
                "//android.widget.EditText[contains(@resource-id,'loadCall')] | //android.widget.EditText[@text='']"));
            loadCallField.Click();
            loadCallField.SendKeys("75");
            flex.HideKeyboardIfShown();

            flex.FindElement(MobileBy.XPath(
                "//android.widget.Button[@text='Submit'] | //android.widget.TextView[@text='Submit']")).Click();

            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Generate QR Code']"));
                return el != null && el.Displayed ? el : null;
            });

            var btnProceed = flex.FindElement(MobileBy.XPath(
                "//android.widget.Button[@text='Proceed'] | //android.widget.TextView[@text='Proceed']"));
            var btnCancel  = flex.FindElement(MobileBy.XPath(
                "//android.widget.Button[@text='Cancel'] | //android.widget.TextView[@text='Cancel']"));

            Assert.Multiple(() =>
            {
                Assert.That(btnProceed.Displayed, Is.True, "'Proceed' button should be visible in the modal.");
                Assert.That(btnCancel.Displayed,  Is.True, "'Cancel' button should be visible in the modal.");
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LD-020 — Receipt QR Code header appears after Proceed is tapped
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("ReceiptQR")]
        public void TC_LD_020_ReceiptQrCodeHeader_AppearsAfterProceed()
        {
            ReachMeasurementsPage();

            var wf   = _waitFlex.Value!;
            var flex = DriverFactory.DriverFlex;

            flex.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Yes']")).Click();

            var loadCallField = flex.FindElement(MobileBy.XPath(
                "//android.widget.EditText[contains(@resource-id,'loadCall')] | //android.widget.EditText[@text='']"));
            loadCallField.Click();
            loadCallField.SendKeys("75");
            flex.HideKeyboardIfShown();

            flex.FindElement(MobileBy.XPath(
                "//android.widget.Button[@text='Submit'] | //android.widget.TextView[@text='Submit']")).Click();

            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Generate QR Code']"));
                return el != null && el.Displayed ? el : null;
            });

            flex.FindElement(MobileBy.XPath(
                "//android.widget.Button[@text='Proceed'] | //android.widget.TextView[@text='Proceed']")).Click();

            var receiptHeader = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[contains(@text,'Receipt QR Code')]"));
                return el != null && el.Displayed ? el : null;
            });

            Assert.That(receiptHeader.Text, Contains.Substring("Receipt QR Code"),
                "The Receipt QR Code header should appear on the Flex App after tapping Proceed.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // TC-LD-021 — Ticket transitions from SYNCING to SYNCED after receipt scan
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        [Category("SyncStatus")]
        public void TC_LD_021_Ticket_TransitionsFromSyncingToSynced()
        {
            ReachMeasurementsPage();

            var wf          = _waitFlex.Value!;
            var flex        = DriverFactory.DriverFlex;
            var driverDriver = DriverFactory.DriverDriver;

            flex.FindElement(MobileBy.XPath("//android.widget.TextView[@text='Yes']")).Click();

            var loadCallField = flex.FindElement(MobileBy.XPath(
                "//android.widget.EditText[contains(@resource-id,'loadCall')] | //android.widget.EditText[@text='']"));
            loadCallField.Click();
            loadCallField.SendKeys("75");
            flex.HideKeyboardIfShown();

            flex.FindElement(MobileBy.XPath(
                "//android.widget.Button[@text='Submit'] | //android.widget.TextView[@text='Submit']")).Click();

            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[@text='Generate QR Code']"));
                return el != null && el.Displayed ? el : null;
            });
            flex.FindElement(MobileBy.XPath(
                "//android.widget.Button[@text='Proceed'] | //android.widget.TextView[@text='Proceed']")).Click();

            // Capture receipt QR and bridge to Driver App
            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.TextView[contains(@text,'Receipt QR Code')]"));
                return el != null && el.Displayed ? el : null;
            });

            var qrElement      = flex.FindElement(MobileBy.XPath(
                "//android.widget.ImageView | //*[contains(@resource-id,'img_receipt_qr')]"));
            string receiptQr64 = ((ITakesScreenshot)qrElement).GetScreenshot().AsBase64EncodedString;

            var btnScanReceipt = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath(
                    "//android.widget.Button[contains(@text,'Scan Receipt')] | //android.widget.TextView[@text='Scan Receipt']"));
                return (el != null && el.Displayed) ? el : null;
            });
            btnScanReceipt.Click();
            if (driverDriver is IJavaScriptExecutor jsExecutor)
            {
                jsExecutor.ExecuteScript("mobile: injectEmulatorCameraImage",
                    new Dictionary<string, object> { { "payload", receiptQr64 } });
            }

            // Dismiss receipt modal on Flex
            flex.FindElement(MobileBy.XPath(
                "//android.widget.TextView[@text='✕'] | //android.widget.Button[contains(@text,'Close')] " +
                "| //android.widget.ImageView[contains(@content-desc,'close')]")).Click();

            // Assert SYNCING appears first
            wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='#5BQ3-LD-100A']"));
                return el != null && el.Displayed ? el : null;
            });

            var syncingLabel = flex.FindElement(MobileBy.XPath(
                "//android.widget.TextView[@text='SYNCING']"));
            Assert.That(syncingLabel.Text, Is.EqualTo("SYNCING"),
                "Ticket should enter SYNCING state immediately after submission.");

            // Then wait for SYNCED
            var syncedLabel = wf.Until(d =>
            {
                var el = d.FindElement(MobileBy.XPath("//android.widget.TextView[@text='SYNCED']"));
                return (el != null && el.Displayed) ? el : null;
            });
            Assert.That(syncedLabel.Text, Is.EqualTo("SYNCED"),
                "Ticket should transition to SYNCED after background sync completes.");
        }
    }
}
