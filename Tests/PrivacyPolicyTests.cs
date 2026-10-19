using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Tests;
using MobileAutomation.Tests.Pages;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using MobileAutomation.Tests.Utilities;

namespace RecoveryTrac.AutomatedTests
{
    [TestFixture]
    public class PrivacyPolicyTests : BaseTest
    {
        // ThreadLocal keeps explicit wait instances fully isolated on a per-thread basis
        private readonly ThreadLocal<WebDriverWait> _wait = new();

        [SetUp]
        public void SetupPrivacyScenario()
        {
            // Initializes the dual-app matrix pools and configures the thread tokens
            DriverFactory.InitializeDualApps();
            int timeoutSeconds = int.Parse(TestConfiguration.Configuration["TestSettings:ExplicitWaitTimeoutSeconds"] ?? "15");
            _wait.Value = new WebDriverWait(DriverFactory.DriverFlex, TimeSpan.FromSeconds(timeoutSeconds));

            // 💡 2. DISMISS POPUP: Handle native system notification permissions programmatically
            if (DriverFactory.DriverFlex is OpenQA.Selenium.Appium.Android.AndroidDriver androidFlex)
            {
                Console.WriteLine("📱 Mobile Mode: Detecting and dismissing native Android permission popups...");
                
                // Using your project's custom utility helper to safely bypass the OS overlay
                PermissionHelper.HandleSystemPermissions(androidFlex);
            }

            // 💡 NEW: Automatically pierce into the active WebView engine layer for the mobile emulators
            DriverFactory.SwitchToWebViewContext(DriverFactory.DriverFlex);
            DriverFactory.SwitchToWebViewContext(DriverFactory.DriverDriver);
        }

        // Clears thread-static allocations immediately after this class finishes executing
        [OneTimeTearDown]
        public void FinalizeClassDisposal()
        {
            Console.WriteLine("Disposing class-level ThreadLocal explicit wait resources to prevent engine memory leaks...");
            
            if (_wait != null)
            {   
                _wait.Dispose();
            }
        }

        [Test]
        public void Test_AcceptPrivacyPolicy_And_VerifyDashboard_E2E()
        {
            var driver = DriverFactory.DriverFlex;
            var wf = _wait.Value!;

            // 1. Initialize Page Objects passing DriverFlex context
            var loginPage = new LoginPage(driver);
            var privacyPolicyPage = new PrivacyPolicyPage(driver);
            var homePage = new HomePage(driver);

            // 💡 2. Encapsulated Flow: Conditionally performs login and automatically handles initial sync spinner
            loginPage.LoginIfNeeded(DriverFactory.LoginEmail, DriverFactory.LoginPassword);

             // 💡 NEW: Catch late-popping OS location prompts right after the login/session load pipeline completes
            if (driver is OpenQA.Selenium.Appium.Android.AndroidDriver androidDriver)
            {
                Console.WriteLine("📱 Mobile Mode: Checking for post-login system permission overrides...");
                PermissionHelper.HandleSystemPermissions(androidDriver);
            }

            // 💡 3. Encapsulated Flow: Automatically handles any sequential onboarding documents (Privacy, EULA, etc.)
            privacyPolicyPage.ClearAllPendingAgreements();

            // -------------------------------------------------------------------------
            // STEP 3: Assert Home Page Layout and Element Visual Assertions
            // -------------------------------------------------------------------------
            Console.WriteLine("Verifying that the Dashboard home page loaded successfully...");
            
            homePage.CompleteProjectAssignment(wf);

            // Safely wait for the banner element presence using FindElements inside the Until loop
            var demoBannerElement = wf.Until(d => 
            {
                var elements = d.FindElements(homePage.DemoTestingBanner);
                // Return the element only if it exists in the list, otherwise return null to keep trying
                return elements.Count > 0 ? elements[0] : null;
            });

            // Retrieve element listing snapshots using safe collections extraction rules
            var profileElements = driver.FindElements(homePage.ProjectManagerProfileText);
            var clockElements = driver.FindElements(homePage.ClockActionButton);

            Assert.Multiple(() =>
            {
                // 1. Validate Main Dashboard Container Visibility
                Assert.That(demoBannerElement, Is.Not.Null, 
                    "The Dashboard 'DEMO-TESTING ONLY' main card block container was not found or visible.");
                
                // 2. Validate User Profile / Session Status String Attributes safely
                Assert.That(profileElements.Count, Is.GreaterThan(0), 
                    "The user profile session status container text tracking 'You are clocked in' was missing from the application tree UI structure.");
                
                if (profileElements.Count > 0)
                {
                    string profileText = profileElements.First().Text;
                    // Accept either valid clock session state
                    Assert.That(profileText.ToLower(),
                        Does.Contain("clocked out")
                        .Or.Contains("clocked in"),
                        $"The user profile text context value '{profileText}' does not contain expected validation keywords.");
                }

                // 3. Validate Interactive Buttons Visibility
                Assert.That(clockElements.Count, Is.GreaterThan(0), "The main dashboard tracking control button interface layer was missing.");
                
                if (clockElements.Count > 0)
                {
                    Assert.That(clockElements.First().Displayed, Is.True, 
                        "The active clock action button is present but blocked from user visibility or interaction pools.");
                }
            });
        }
    }
}
