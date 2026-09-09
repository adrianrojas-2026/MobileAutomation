using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Reporting;

namespace MobileAutomation.Tests.Tests;

public class BaseTest
{

   [TearDown]
    public void Cleanup()
    {
        // try
        // {
        //     if (DriverFactory.Driver != null &&
        //         TestContext.CurrentContext.Result.Outcome.Status ==
        //         NUnit.Framework.Interfaces.TestStatus.Failed)
        //     {
        //         var screenshotsFolder =
        //             Path.Combine(
        //                 AppContext.BaseDirectory,
        //                 "Screenshots");

        //         Directory.CreateDirectory(
        //             screenshotsFolder);

        //         var fileName =
        //             Path.Combine(
        //                 screenshotsFolder,
        //                 $"{TestContext.CurrentContext.Test.Name}.png");

        //         DriverFactory.AndroidDriver
        //             .GetScreenshot()
        //             .SaveAsFile(fileName);

        //         Console.WriteLine(
        //             $"Screenshot saved: {fileName}");
        //     }
        // }
        // catch (Exception ex)
        // {
        //     Console.WriteLine(
        //         $"Screenshot capture failed: {ex.Message}");
        // }
        Console.WriteLine("***** GENERATING HTML REPORT *****");
        HtmlReportGenerator.Generate();
        DriverFactory.Quit();
    }

    [OneTimeTearDown]
    public void GenerateReport()
    {
        Console.WriteLine("Generating HTML Report...");
        HtmlReportGenerator.Generate();
    }
}