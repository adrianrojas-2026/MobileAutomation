using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Pages;
using MobileAutomation.Tests.Utilities;
using MobileAutomation.Tests.Reporting;
using OpenQA.Selenium.Support.UI;
using Microsoft.Extensions.Configuration;

namespace MobileAutomation.Tests.Tests;


[TestFixture]
[Parallelizable(ParallelScope.All)]
public class LoginTests : BaseTest
{

    [Test]
    [Parallelizable(ParallelScope.Self)]
    public void LoginTraxAndroid8()
    {
        ExecuteLogin("Grid_Node2_Flex");
    }

    [Test]
    [Parallelizable(ParallelScope.Self)]
    public void LoginTraxAndroid12()
    {
        ExecuteLogin("Grid_Node1_Flex");
    }

    [Test]
   [Parallelizable(ParallelScope.Self)]
    public void LoginTraxAndroid14()
    {
        ExecuteLogin("Grid_Node1_Driver");
    }

    private void ExecuteLogin(string environment)
    {
        var start = DateTime.Now;
        Console.WriteLine(
            $"START {environment} - {start:HH:mm:ss.fff}");
        try
        {
            DriverFactory.Initialize(environment);
            
            var environmentVariables = new Dictionary<string, string?>();
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                var key = entry.Key?.ToString();
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                // Azure DevOps and other CI systems commonly export variables with __ separators.
                // This converts them to the ':' format expected by the configuration binding.
                var normalizedKey = key.Replace("__", ":");
                environmentVariables[normalizedKey] = entry.Value?.ToString();
            }

            IConfiguration config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("Config/appsettings.json", optional: false, reloadOnChange: false)
                .AddInMemoryCollection(environmentVariables)
                .Build();

            string email = config["TestSettings:LoginEmail"] ?? throw new NullReferenceException("LoginEmail setting is missing.");
            string password = config["TestSettings:LoginPassword"] ?? throw new NullReferenceException("LoginPassword setting is missing.");
            int timeout = int.Parse(config["TestSettings:ExplicitWaitTimeoutSeconds"] ?? "15");

            Assert.That(
                DriverFactory.AndroidDriver.CurrentPackage,
                Is.EqualTo("com.tetratech.recoverytracflex"));
            
            var wait = new WebDriverWait(DriverFactory.AndroidDriver, TimeSpan.FromSeconds(timeout));
    
            foreach (var context in DriverFactory.Driver.Contexts)
            {
                Console.WriteLine(context);
            }

            PermissionHelper.HandleNotifications(
                DriverFactory.AndroidDriver);

            var loginPage = new LoginPage(DriverFactory.AndroidDriver);
            
            loginPage.LoginTrac(
                email,
                password);

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
