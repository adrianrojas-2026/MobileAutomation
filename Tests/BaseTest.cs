using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Reporting;
using OpenQA.Selenium.Appium.Android;

namespace MobileAutomation.Tests.Tests;

public class BaseTest
{

   [TearDown]
    public void Cleanup()
        {
        Console.WriteLine("[Teardown] Gracefully closing driver sessions and application packages...");

        // 🟢 1. Handle Single-App Test Closures (Only if running on an actual Appium Android session)
        try
        {
            if (DriverFactory.Driver is AndroidDriver androidDriver)
            {
                string activePackage = androidDriver.CurrentPackage;
                if (!string.IsNullOrWhiteSpace(activePackage))
                {
                    androidDriver.TerminateApp(activePackage);
                }
            }
        }
        catch { /* Protect against null/already disposed state faults */ }

        // 🟢 2. Handle Dual-App Parallel Scenario Test Closures (Safely type-check for AndroidDriver wrapper)
        try
        {
            if (DriverFactory.DriverFlex is AndroidDriver flexAndroid)
            {
                flexAndroid.TerminateApp("com.tetratech.recoverytracflex");
            }
        }
        catch { /* Protect against null/already disposed state faults */ }

        try
        {
            if (DriverFactory.DriverDriver is AndroidDriver driverAndroid)
            {
                driverAndroid.TerminateApp("com.tetratech.recoverytracdriver");
            }
        }
        catch { /* Protect against null/already disposed state faults */ }

        // 🟢 3. Clear the ThreadLocal handles out of the memory engine and recycle leased grid nodes
        DriverFactory.Quit();
    }

    [OneTimeSetUp]
    public void StartSuite()
    {
        HtmlReportGenerator.SuiteStartTime = DateTime.Now;
    }

    [OneTimeTearDown]
    public void GenerateReport()
    {
        HtmlReportGenerator.SuiteEndTime = DateTime.Now;
        Console.WriteLine("Generating HTML Report...");
        HtmlReportGenerator.Generate();
    }
}