using Microsoft.Extensions.Configuration;
using MobileAutomation.Tests.Utilities;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.iOS;

namespace MobileAutomation.Tests.Drivers;

public static class DriverFactory
{
    public static AppiumDriver Driver
    {
        get;
        private set;
    } = null!;
    
    public static string CurrentEnvironment { get; private set; } = string.Empty;

    public static string CurrentPlatformVersion { get; private set; } = string.Empty;

    public static string CurrentDeviceName { get; private set; } = string.Empty;

    public static string CurrentUdid { get; private set; } = string.Empty;
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

        CurrentEnvironment = environment;
        CurrentPlatformVersion = envSection["PlatformVersion"] ?? string.Empty;
        CurrentDeviceName = envSection["DeviceName"] ?? string.Empty;
        CurrentUdid = envSection["Udid"] ?? string.Empty;

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

            Driver = new AndroidDriver(
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

            Driver = new IOSDriver(
                new Uri(appiumSection["ServerUrl"]!),
                options);
        }
    }
    public static void Quit()
    {
        Driver?.Quit();
    }
}