using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Reporting;

namespace MobileAutomation.Tests.Tests;

public class BaseTest
{

   [TearDown]
    public void Cleanup()
    {
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