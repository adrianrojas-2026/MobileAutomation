using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Reporting;
using MobileAutomation.Tests.Utilities;
using OpenQA.Selenium.Support.UI;

namespace MobileAutomation.Tests.Tests;

// Flex + Driver app on one node pair (Grid_<node>_Flex + Grid_<node>_Driver), with failure evidence from both apps.
// Shared by the QR handoff tests and the QR baselines the BLE handoff tests are compared against.
public abstract class DualAppTestBase : BaseTest
{
    // ThreadLocal keeps explicit wait instances fully isolated on a per-thread basis
    protected readonly ThreadLocal<WebDriverWait> _wait = new();

    // By default each test case names its node as its first argument; fixtures pinned to one node override this
    protected virtual string NodeForCurrentTest => (string)TestContext.CurrentContext.Test.Arguments[0]!;

    // null logs in with the node's own Flex account; fixtures that pick the account by name override this
    protected virtual string? AccountForCurrentTest => null;

    [SetUp]
    public void SetupDualApps()
    {
        QrBaselineReport.StartTest();
        DriverFactory.InitializeDualApps(NodeForCurrentTest, AccountForCurrentTest);
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
        if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            SaveEvidence(DriverFactory.DriverFlex, "Flex", timestamp);
            // Only once the test has started it
            if (DriverFactory.DriverDriver != null) SaveEvidence(DriverFactory.DriverDriver, "DriverApp", timestamp);
        }

        // After the evidence, so the final report can show it
        QrBaselineReport.RecordTest(NodeForCurrentTest);
    }

    private static void SaveEvidence(OpenQA.Selenium.IWebDriver driver, string app, string timestamp)
    {
        // A screenshot of a frozen emulator would hang the teardown as long as the test itself
        if (driver is OpenQA.Selenium.Appium.Android.AndroidDriver android
            && AdbHelper.GetUdid(android) is { } udid && !AdbHelper.IsResponsive(udid))
        {
            Console.WriteLine($"⚠️ {app} device {udid} is not responding to adb; no failure evidence captured. Restart that emulator before the next run.");
            return;
        }

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
}
