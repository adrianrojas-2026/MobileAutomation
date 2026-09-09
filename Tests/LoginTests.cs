using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Pages;
using MobileAutomation.Tests.Utilities;
using OpenQA.Selenium.Support.UI;
using MobileAutomation.Tests.Reporting;

namespace MobileAutomation.Tests.Tests;


[TestFixture]
public class LoginTests : BaseTest
{

    [Test]
    [Ignore("Temporarily disabled while debugging Appium")]
    public void VerifySessionStarts()
    {
        Console.WriteLine("Session started");
        //Console.WriteLine(DriverFactory.Driver.CurrentPackage);
        Assert.Pass();
    }

    [Test]
    [Ignore("Temporarily disabled while debugging Appium")]
    public void DumpCurrentScreen()
    {
        
        PermissionHelper.HandleNotifications(DriverFactory.AndroidDriver);
        Thread.Sleep(3000);
        
        File.WriteAllText(
            @"C:\temp\RecoveryTrac.xml",
            DriverFactory.Driver.PageSource);

        var screenshot =
            DriverFactory.Driver.GetScreenshot();

        screenshot.SaveAsFile(@"C:\temp\RecoveryTrac.png");

        Console.WriteLine("Contexts:");

        var contexts =
            DriverFactory.Driver.Contexts;

        foreach (var context in contexts)
        {
            Console.WriteLine($"- {context}");
        }
        Assert.Pass();
    }

    [Test]
    //[Ignore("Temporarily disabled while debugging Appium")]
    [TestCase("Android8")]
    [TestCase("Android12")]
    [TestCase("Android14")]
    //[TestCase("Android16")]
    public void LoginTrax(string environment)
    {
        var start = DateTime.Now;

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

            HtmlReportGenerator.AddResult(
                new ReportResult
                {
                    Environment = environment,
                    PlatformVersion = DriverFactory.CurrentPlatformVersion,
                    DeviceName = DriverFactory.CurrentDeviceName,
                    Result = "PASS",
                    DurationSeconds = (DateTime.Now - start).TotalSeconds,
                    ScreenshotPath = $"Screenshots/{environment}.png"
                }
            );

            Console.WriteLine("Login successful");
        }
            catch (Exception ex)
            {
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
                            (DateTime.Now - start).TotalSeconds,
                        ScreenshotPath =
                            $"Screenshots/{environment}_FAIL.png"
                    });

                throw;
            }
        finally
        {
            DriverFactory.Driver.Quit();
        }
    }

    [Test]
    [Ignore("Temporarily disabled while debugging Appium")]
    public void VerifyWebViewAccess()
    {
        Thread.Sleep(5000);
        Console.WriteLine("1");
        ContextHelper.SwitchToWebView(
            DriverFactory.AndroidDriver);
        Console.WriteLine("2");
        // Console.WriteLine(
        //     DriverFactory.Driver.PageSource);
        
        Assert.Pass();
    }

    [Test]
    [Ignore("Temporarily disabled while debugging Appium")]
    public void UserCanLogin()
    {
        Thread.Sleep(5000);
        // Switch to WebView if your app is hybrid
        //ContextHelper.SwitchToWebView(DriverFactory.Driver);

        Console.WriteLine("Current Contexts:");
        foreach (var context in DriverFactory.Driver.Contexts)
        {
            Console.WriteLine(context);
        }

        DriverFactory.Driver.ExecuteScript(
            "mobile: clickGesture",
            new Dictionary<string, object>
            {
                ["x"] = 500,
                ["y"] = 985
            });

        Thread.Sleep(15000);
        Console.WriteLine("Current Contexts:");
        foreach (var context in DriverFactory.Driver.Contexts)
        {
            Console.WriteLine(context);
        }
        
        DriverFactory.Driver.Context = "WEBVIEW_chrome";

        var wait = new WebDriverWait(
            DriverFactory.Driver,
            TimeSpan.FromSeconds(30));
        
        var loginPage = new LoginPage(DriverFactory.AndroidDriver);
        
        loginPage.Login(
            "hsa103",
            "Becu1234");

        // var inputs = DriverFactory.Driver.FindElements(
        //     OpenQA.Selenium.By.TagName("input"));

        // Console.WriteLine($"Inputs found: {inputs.Count}");

        // foreach (var input in inputs)
        // {
        //     try
        //     {
        //         Console.WriteLine(
        //             $"id={input.GetAttribute("id")} " +
        //             $"name={input.GetAttribute("name")} " +
        //             $"type={input.GetAttribute("type")}");
        //     }
        //     catch
        //     {
        //     }
        // }

        Thread.Sleep(20000);

        DriverFactory.Driver.ExecuteScript(
            "mobile: clickGesture",
            new Dictionary<string, object>
            {
                ["x"] = 592,
                ["y"] = 2180
            });
        Thread.Sleep(5000);
        Console.WriteLine("Current Contexts: Logged");
        DriverFactory.Driver.Context = "NATIVE_APP";

        Assert.Pass("Login test executed successfully.");
    }


}
