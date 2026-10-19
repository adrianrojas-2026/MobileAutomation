using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Pages;
using MobileAutomation.Tests.Utilities;
using MobileAutomation.Tests.Reporting;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Microsoft.Extensions.Configuration;

namespace MobileAutomation.Tests.Tests;

[TestFixture]
[Parallelizable(ParallelScope.Children)]
public class LoginTests : BaseTest
{

    //[Test]
    // public async Task LoginTraxAndroid8()
    // {
    //     await ExecuteLogin("Grid_Node2_Flex");
    // }
    
    // [Test]
    // public async Task LoginTraxAndroid11()
    // {
    //     await ExecuteLogin("Grid_Node2_Driver");
    // }

    // [Test]
    // public async Task LoginTraxAndroid12()
    // {
    //     await ExecuteLogin("Grid_Node1_Flex");
    // }

    [Test]
    public async Task LoginTraxAndroid14()
    {
        await ExecuteLogin("Grid_Node1_Flex", "flex");
    }

    private async Task ExecuteLogin(string environment, string appType = "flex")
    {
        var start = DateTime.Now;
        Console.WriteLine(
            $"START {environment} - {start:HH:mm:ss.fff}");
        try
        {
            DriverFactory.Initialize(environment, appType);
            
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
                .AddUserSecrets(typeof(LoginTests).Assembly, optional: true)
                .AddInMemoryCollection(environmentVariables)
                .Build();

            string email = config["TestSettings:LoginEmail"] ?? throw new NullReferenceException("LoginEmail setting is missing. Set it with 'dotnet user-secrets set TestSettings:LoginEmail <value>'.");
            string password = config["TestSettings:LoginPassword"] ?? throw new NullReferenceException("LoginPassword setting is missing. Set it with 'dotnet user-secrets set TestSettings:LoginPassword <value>'.");

            // string email = await KeyVaultAccessClient.GetKeyVaultSecretAsync(
            //     config["TestSettings:flexLoginEmail"] ?? throw new NullReferenceException("LoginEmail setting is missing."), 
            //     config["TestSettings:KeyVaultName"] ?? throw new NullReferenceException("KeyVaultName setting is missing."));

            // string password = await KeyVaultAccessClient.GetKeyVaultSecretAsync(
            //     config["TestSettings:flexLoginPassword"] ?? throw new NullReferenceException("LoginPassword setting is missing."), 
            //     config["TestSettings:KeyVaultName"] ?? throw new NullReferenceException("KeyVaultName setting is missing."));

            int timeout = int.Parse(config["TestSettings:ExplicitWaitTimeoutSeconds"] ?? "15");

            string expectedPackage = string.Equals(appType, "driver", StringComparison.OrdinalIgnoreCase)
                ? "com.tetratech.recoverytracdriver"
                : "com.tetratech.recoverytracflex";

            // Parameterized assertion handles both application lifecycles flawlessly
            Assert.That(
                DriverFactory.AndroidDriver.CurrentPackage,
                Is.EqualTo(expectedPackage), 
                $"Expected application package '{expectedPackage}' to be active, but found '{DriverFactory.AndroidDriver.CurrentPackage}' instead.");

            
            var wait = new WebDriverWait(DriverFactory.AndroidDriver, TimeSpan.FromSeconds(timeout));
    
            foreach (var context in DriverFactory.Driver.Contexts)
            {
                Console.WriteLine(context);
            }

            PermissionHelper.HandleNotifications(
                DriverFactory.AndroidDriver);
            
            if (string.Equals(appType, "flex", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[Flow Automation] Executing target Login validation sequence for Flex app container...");
                var loginPage = new LoginPage(DriverFactory.AndroidDriver);
                loginPage.LoginTrac(email, password);
            }
            else
            {
               Console.WriteLine($"[Flow Automation] Driver App target detected. Handling onboarding welcome canvas layer...");
                
                // 1. Wait until the 'GET STARTED' button is fully rendered and interactable on the screen viewport
                var btnGetStarted = wait.Until(d => {
                    var el = d.FindElement(By.XPath("//android.widget.Button[@text='GET STARTED'] | //android.widget.TextView[@text='GET STARTED'] | //*[contains(@content-desc, 'STARTED')]"));
                    return (el != null && el.Displayed && el.Enabled) ? el : null;
                });
                
                // Assert that the welcome page loaded successfully before performing the interactive tap
                Assert.That(btnGetStarted.Displayed, Is.True, "The RecoveryTrac Driver App welcome splash landing page failed to display.");
                
                // 2. Click the button to dismiss the onboarding splash screen view
                btnGetStarted.Click();
                Console.WriteLine("[Flow Automation] Successfully tapped 'GET STARTED'. Onboarding wizard cleared cleanly.");
                
            }

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
    }
}
