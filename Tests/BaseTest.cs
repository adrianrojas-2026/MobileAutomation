using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Reporting;

namespace MobileAutomation.Tests.Tests;

public class BaseTest
{

   [TearDown]
    public void Cleanup()
    {
        // TBD if we want to kill and start app for each test or if we wish to keep it running in the background for the entire test suite. 
        // For now, we will gracefully terminate the app after each test to ensure a clean state for the next test.
        Console.WriteLine("[Teardown] Gracefully force-closing application packages from device memory...");

        try
        {
            // 🟢 1. Handle Single-App Test Closures
            string activePackage = DriverFactory.AndroidDriver.CurrentPackage;
            if (!string.IsNullOrWhiteSpace(activePackage))
            {
                DriverFactory.AndroidDriver.TerminateApp(activePackage);
            }
        }
        catch { /* Protect against null/already disposed state faults */ }

        try
        {
            // 🟢 2. Handle Dual-App Parallel Scenario Test Closures (Flex + Driver)
            DriverFactory.DriverFlex.TerminateApp("com.tetratech.recoverytracflex");
            DriverFactory.DriverDriver.TerminateApp("com.tetratech.recoverytracdriver");
        }
        catch { /* Protect against null/already disposed state faults */ }

        // 🟢 3. Clear the ThreadLocal handles out of the memory engine
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