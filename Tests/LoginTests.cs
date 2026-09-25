using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Pages;
using MobileAutomation.Tests.Utilities;
using OpenQA.Selenium.Support.UI;
using MobileAutomation.Tests.Reporting;

namespace MobileAutomation.Tests.Tests;


[TestFixture]
[Parallelizable(ParallelScope.All)]
public class LoginTests : BaseTest
{

    [Test]
    [Parallelizable(ParallelScope.Self)]
    public void LoginTraxAndroid8()
    {
        ExecuteLogin("Android8");
    }

    [Test]
    [Parallelizable(ParallelScope.Self)]
    public void LoginTraxAndroid12()
    {
        ExecuteLogin("Android12");
    }

    [Test]
   [Parallelizable(ParallelScope.Self)]
    public void LoginTraxAndroid14()
    {
        ExecuteLogin("Android14");
    }

    private void ExecuteLogin(string environment)
    {
        var start = DateTime.Now;
        Console.WriteLine(
            $"START {environment} - {start:HH:mm:ss.fff}");
        try
        {
            DriverFactory.Initialize(environment);
            
            Assert.That(
                DriverFactory.AndroidDriver.CurrentPackage,
                Is.EqualTo("com.tetratech.recoverytracflex"));
            
            Thread.Sleep(5000);
            foreach (var context in DriverFactory.Driver.Contexts)
            {
                Console.WriteLine(context);
            }

            PermissionHelper.HandleNotifications(
                DriverFactory.AndroidDriver);

            var loginPage = new LoginPage(DriverFactory.AndroidDriver);
            
            loginPage.LoginTrac(
                "test@gmail.com",
                "Trac1234");

            var screenshotsFolder =
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Reports",
                    "Screenshots");

            Directory.CreateDirectory(
                screenshotsFolder);

            var screenshotFile = Path.Combine(
                screenshotsFolder,
                $"{environment}.png");

            DriverFactory.AndroidDriver
                .GetScreenshot()
                .SaveAsFile(screenshotFile);

            var end = DateTime.Now;

            Console.WriteLine(
                $"END {environment} - {end:HH:mm:ss.fff}");

            HtmlReportGenerator.AddResult(
                new ReportResult
                {
                    Environment = environment,
                    PlatformVersion = DriverFactory.CurrentPlatformVersion,
                    DeviceName = DriverFactory.CurrentDeviceName,
                    Result = "PASS",
                    DurationSeconds = (end - start).TotalSeconds,
                    ScreenshotPath = $"Screenshots/{environment}.png",
                    StartTime = start.ToString("HH:mm:ss.fff"),
                    EndTime = end.ToString("HH:mm:ss.fff")
                }
            );

            Console.WriteLine("Login successful");
            Console.WriteLine(
                $"END {environment} - {DateTime.Now:HH:mm:ss.fff}");
        }
            catch (Exception ex)
            {
                var end = DateTime.Now;
                var screenshotsFolder =
                    Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "Reports",
                        "Screenshots");

                Directory.CreateDirectory(
                    screenshotsFolder);

                var screenshotFile = Path.Combine(
                    screenshotsFolder,
                    $"{environment}_FAIL.png");

                try
                {
                    DriverFactory.AndroidDriver
                        .GetScreenshot()
                        .SaveAsFile(screenshotFile);
                }
                catch
                {
                    // Ignore screenshot failures
                }

                HtmlReportGenerator.AddResult(
                    new ReportResult
                    {
                        Environment = environment,
                        PlatformVersion =
                            DriverFactory.CurrentPlatformVersion,
                        DeviceName =
                            DriverFactory.CurrentDeviceName,
                        Result = "FAIL",
                        ErrorMessage = ex.Message,
                        DurationSeconds =
                            (end - start).TotalSeconds,
                        ScreenshotPath =
                            $"Screenshots/{environment}_FAIL.png",
                        StartTime = start.ToString("HH:mm:ss.fff"),
                        EndTime = end.ToString("HH:mm:ss.fff")
                    });

                throw;
            }
        finally
        {
            DriverFactory.Quit();
        }
    }
}
