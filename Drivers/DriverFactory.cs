using Microsoft.Extensions.Configuration;
using MobileAutomation.Tests.Utilities;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.iOS;

namespace MobileAutomation.Tests.Drivers;

public static class DriverFactory
{
    private static ThreadLocal<AppiumDriver> _driver =
    new();

    public static AppiumDriver Driver =>
        _driver.Value!;
    
    private static readonly ThreadLocal<string> _environment = new();
    private static readonly ThreadLocal<string> _platformVersion = new();
    private static readonly ThreadLocal<string> _deviceName = new();
    private static readonly ThreadLocal<string> _udid = new();

    public static string CurrentEnvironment =>
        _environment.Value ?? string.Empty;
    public static string CurrentPlatformVersion =>
        _platformVersion.Value ?? string.Empty;

    public static string CurrentDeviceName =>
        _deviceName.Value ?? string.Empty;

    public static string CurrentUdid =>
        _udid.Value ?? string.Empty;

    public static AndroidDriver AndroidDriver =>
        (AndroidDriver)Driver;

    public static void Initialize(string environment)
    {
        IConfiguration config =
            new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(
                    "Config/appsettings.json",
                    optional: false,
                    reloadOnChange: false)
                .Build();

        var appiumSection = config.GetSection("Appium");
        var envSection =
            config.GetSection($"MobileConfigurations:{environment}");

        Console.WriteLine("=================================");
        Console.WriteLine($"Environment = {environment}");
        Console.WriteLine($"PlatformName = {envSection["PlatformName"]}");
        Console.WriteLine($"PlatformVersion = {envSection["PlatformVersion"]}");
        Console.WriteLine($"DeviceName = {envSection["DeviceName"]}");
        Console.WriteLine($"Udid = {envSection["Udid"]}");
        Console.WriteLine("=================================");

        _environment.Value = environment;
        _platformVersion.Value =
            envSection["PlatformVersion"] ?? string.Empty;;

        _deviceName.Value =
            envSection["DeviceName"] ?? string.Empty;;

        _udid.Value =
            envSection["Udid"] ?? string.Empty;;

        var options = new AppiumOptions();

        options.PlatformName = envSection["PlatformName"];

        options.PlatformVersion = envSection["PlatformVersion"];

       options.DeviceName = envSection["DeviceName"];

        options.AutomationName = appiumSection["AutomationName"];

        var udid = envSection["Udid"];

        if (!string.IsNullOrWhiteSpace(udid))
        {
            options.AddAdditionalAppiumOption(
                "appium:udid",
                udid);
       }

       options.AddAdditionalAppiumOption(
            "appium:uiautomator2ServerLaunchTimeout",
            120000);

       options.AddAdditionalAppiumOption(
            "appium:systemPort",
            int.Parse(envSection["SystemPort"]!));

        options.AddAdditionalAppiumOption(
            "appium:newCommandTimeout",
            int.Parse(appiumSection["NewCommandTimeout"]!));

        options.AddAdditionalAppiumOption(
            "appium:autoGrantPermissions",
            bool.Parse(appiumSection["AutoGrantPermissions"]!));

        options.AddAdditionalAppiumOption(
            "appium:chromedriverAutodownload",
            bool.Parse(appiumSection["ChromedriverAutodownload"]!));

        if (string.Equals(options.PlatformName,
            "Android",
            StringComparison.OrdinalIgnoreCase))
        {
            options.AddAdditionalAppiumOption(
                "appium:appPackage",
                appiumSection["AppPackage"]);

            options.AddAdditionalAppiumOption(
                "appium:appActivity",
                appiumSection["AppActivity"]);

            _driver.Value = 
                new AndroidDriver(
                    new Uri(appiumSection["ServerUrl"]!),
                    options);
            
            var androidDriver = (AndroidDriver)Driver;
            Console.WriteLine(
                $"Connected to Android {androidDriver.Capabilities.GetCapability("platformVersion")}");
            Console.WriteLine(
                $"Connected to device {androidDriver.Capabilities.GetCapability("udid")}");
        }
        else
        {
            options.AddAdditionalAppiumOption(
                "appium:automationName",
                "XCUITest");

            _driver.Value = new IOSDriver(
                new Uri(appiumSection["ServerUrl"]!),
                options);
        }
    }
    public static void Quit()
    {
        try
        {
            _driver.Value?.Quit();
        }
        catch
        {
        }

        _driver.Value = null!;
    }
}