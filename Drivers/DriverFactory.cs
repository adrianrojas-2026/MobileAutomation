using Microsoft.Extensions.Configuration;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.iOS;

namespace MobileAutomation.Tests.Drivers;

public static class DriverFactory
{
    private static ThreadLocal<AppiumDriver> _driver = new();
    public static AppiumDriver Driver => _driver.Value!;
    public static AndroidDriver AndroidDriver => (AndroidDriver)Driver;

        // --- NUEVOS CONTENEDORES EXCLUSIVOS (Para escenarios simultáneos Flex + Driver) ---
    private static ThreadLocal<AndroidDriver> _driverFlex = new();
    private static ThreadLocal<AndroidDriver> _driverDriver = new();

    public static AndroidDriver DriverFlex => _driverFlex.Value!;
    public static AndroidDriver DriverDriver => _driverDriver.Value!;


    // --- Shared enviroment variables ---
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
    
    public static void InitializeDualApps(string environmentFlex, string environmentDriver)
    {
        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("Config/appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var appiumSection = config.GetSection("Appium");
        string serverUrl = appiumSection["ServerUrl"]!;

        // 1. INICIALIZAR RECOVERY TRAC FLEX
        var envFlexSection = config.GetSection($"MobileConfigurations:{environmentFlex}");
        var optionsFlex = ConfigurarOpciones(appiumSection, envFlexSection);
        _driverFlex.Value = new AndroidDriver(new Uri(serverUrl), optionsFlex);

        // 2. INICIALIZAR RECOVERY TRAC DRIVER
        var envDriverSection = config.GetSection($"MobileConfigurations:{environmentDriver}");
        var optionsDriver = ConfigurarOpciones(appiumSection, envDriverSection);
        _driverDriver.Value = new AndroidDriver(new Uri(serverUrl), optionsDriver);

        _environment.Value = $"{environmentFlex} + {environmentDriver}";
    }

    // Método utilitario interno para reutilizar la lógica de lectura del appsettings.json
    private static AppiumOptions ConfigurarOpciones(IConfigurationSection appiumSection, IConfigurationSection envSection)
    {
        var options = new AppiumOptions();
        options.PlatformName = envSection["PlatformName"];
        options.PlatformVersion = envSection["PlatformVersion"];
        options.DeviceName = envSection["DeviceName"];
        options.AutomationName = appiumSection["AutomationName"];

        var udid = envSection["Udid"];
        if (!string.IsNullOrWhiteSpace(udid))
        {
            options.AddAdditionalAppiumOption("appium:udid", udid);
        }

        options.AddAdditionalAppiumOption("appium:systemPort", int.Parse(envSection["SystemPort"]!));
        options.AddAdditionalAppiumOption("appium:uiautomator2ServerLaunchTimeout", 120000);
        options.AddAdditionalAppiumOption("appium:newCommandTimeout", int.Parse(appiumSection["NewCommandTimeout"]!));
        options.AddAdditionalAppiumOption("appium:autoGrantPermissions", bool.Parse(appiumSection["AutoGrantPermissions"]!));
        options.AddAdditionalAppiumOption("appium:appPackage", envSection["AppPackage"] ?? appiumSection["AppPackage"]);
        options.AddAdditionalAppiumOption("appium:appActivity", envSection["AppActivity"] ?? appiumSection["AppActivity"]);

        return options;
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
        
        // Clean DriverFlex
        try 
        { 
            _driverFlex.Value?.Quit(); 
        } 
        catch
        {
        }
        _driverFlex.Value = null!;

        // clean DriverDriver
        try 
        { 
            _driverDriver.Value?.Quit(); 
        } 
        catch
        {
        }
        _driverDriver.Value = null!;

    }
}