using Microsoft.Extensions.Configuration;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.iOS;

namespace MobileAutomation.Tests.Drivers;

public static class DriverFactory
{
    private static readonly ThreadLocal<Tuple<string, string>?> _leasedNode = new();
    private static ThreadLocal<AppiumDriver> _driver = new();
    public static AppiumDriver Driver => _driver.Value!;
    public static AndroidDriver AndroidDriver => (AndroidDriver)Driver;

    // --- NUEVOS CONTENEDORES EXCLUSIVOS (Para escenarios simultáneos Flex + Driver) ---
    private static ThreadLocal<AndroidDriver> _driverFlex = new();
    private static ThreadLocal<AndroidDriver> _driverDriver = new();

    public static AndroidDriver DriverFlex => _driverFlex.Value!;
    public static AndroidDriver DriverDriver => _driverDriver.Value!;

    // --- Shared environment variables ---
    private static readonly ThreadLocal<string> _environment = new();
    private static readonly ThreadLocal<string> _platformVersion = new();
    private static readonly ThreadLocal<string> _deviceName = new();
    private static readonly ThreadLocal<string> _udid = new();

    public static string CurrentEnvironment => _environment.Value ?? string.Empty;
    public static string CurrentPlatformVersion => _platformVersion.Value ?? string.Empty;
    public static string CurrentDeviceName => _deviceName.Value ?? string.Empty;
    public static string CurrentUdid => _udid.Value ?? string.Empty;

   // Update the pool declaration in your DriverFactory to use the new isolated nodes
    private static readonly System.Collections.Concurrent.ConcurrentQueue<Tuple<string, string>> _availableNodesPool = new(new[]
    {
        Tuple.Create("Grid_Node1_Flex", "Grid_Node1_Driver"), // Leased by the first concurrent thread
        Tuple.Create("Grid_Node2_Flex", "Grid_Node2_Driver"), // Leased by the second concurrent thread
        Tuple.Create("Grid_Node3_Flex", "Grid_Node3_Driver")  // Leased by the third concurrent thread
    });

    // Update the method signature to accept an optional app override ("flex" or "driver")
    public static void Initialize(string environment, string appTarget = "flex")
    {
        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("Config/appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var appiumSection = config.GetSection("Appium");
        var envSection = config.GetSection($"MobileConfigurations:{environment}");

        if (!envSection.Exists())
        {
            throw new ArgumentException($"The configuration profile key '{environment}' was not found in appsettings.json.");
        }

        Console.WriteLine("=================================");
        Console.WriteLine($"Environment = {environment}");
        Console.WriteLine($"PlatformName = {envSection["PlatformName"]}");
        Console.WriteLine($"PlatformVersion = {envSection["PlatformVersion"]}");
        Console.WriteLine($"DeviceName = {envSection["DeviceName"]}");
        Console.WriteLine($"Udid = {envSection["Udid"]}");
        Console.WriteLine("=================================");

        _environment.Value = environment;
        _platformVersion.Value = envSection["PlatformVersion"] ?? string.Empty;
        _deviceName.Value = envSection["DeviceName"] ?? string.Empty;
        _udid.Value = envSection["Udid"] ?? string.Empty;

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

        options.AddAdditionalAppiumOption("appium:uiautomator2ServerLaunchTimeout", 120000);

        // SAFE NULL FALLBACK PARSING FOR PORTS
        var systemPortStr = envSection["SystemPort"];
        if (!string.IsNullOrWhiteSpace(systemPortStr))
        {
            options.AddAdditionalAppiumOption("appium:systemPort", int.Parse(systemPortStr));
        }

        var mjpegPortStr = envSection["mjpegServerPort"];
        if (!string.IsNullOrWhiteSpace(mjpegPortStr))
        {
            options.AddAdditionalAppiumOption("appium:mjpegServerPort", int.Parse(mjpegPortStr));
        }

        options.AddAdditionalAppiumOption("appium:newCommandTimeout", int.Parse(appiumSection["NewCommandTimeout"]!));
        options.AddAdditionalAppiumOption("appium:autoGrantPermissions", bool.Parse(appiumSection["AutoGrantPermissions"]!));
        options.AddAdditionalAppiumOption("appium:chromedriverAutodownload", bool.Parse(appiumSection["ChromedriverAutodownload"]!));

        var noResetStr = appiumSection["noReset"];
        if (!string.IsNullOrWhiteSpace(noResetStr))
        {
            options.AddAdditionalAppiumOption("appium:noReset", bool.Parse(noResetStr));
        }

        var dontStopAppStr = appiumSection["dontStopAppOnReset"];
        if (!string.IsNullOrWhiteSpace(dontStopAppStr))
        {
            options.AddAdditionalAppiumOption("appium:dontStopAppOnReset", bool.Parse(dontStopAppStr));
        }

        if (string.Equals(options.PlatformName, "Android", StringComparison.OrdinalIgnoreCase))
        {
            // 💡 DYNAMIC APP TARGETING LOGIC
            string package;
            string activity;

            if (string.Equals(appTarget, "driver", StringComparison.OrdinalIgnoreCase))
            {
                package = "com.tetratech.recoverytracdriver";
                activity = "com.tetratech.recoverytracdriver.MainActivity";
            }
            else // Defaults to Flex app
            {
                package = "com.tetratech.recoverytracflex";
                activity = "com.tetratech.recoverytracflex.MainActivity";
            }

            // FALLBACK TO GLOBAL APPIUM APP PARAMETERS IF PROFILE LACKS UNIQUE EXPLICIT PATHS
            options.AddAdditionalAppiumOption("appium:appPackage", package);
            options.AddAdditionalAppiumOption("appium:appActivity", activity);

            _driver.Value = new AndroidDriver(new Uri(appiumSection["ServerUrl"]!), options);
            
            var androidDriver = (AndroidDriver)Driver;
            Console.WriteLine($"Connected to Android {androidDriver.Capabilities.GetCapability("platformVersion")}");
            Console.WriteLine($"Connected to device {androidDriver.Capabilities.GetCapability("udid")}");
        }
        else
        {
            options.AddAdditionalAppiumOption("appium:automationName", "XCUITest");
            _driver.Value = new IOSDriver(new Uri(appiumSection["ServerUrl"]!), options);
        }
    }
    
    public static void InitializeDualApps()
    {
        // Lease an available node pair from the pool
        if (!_availableNodesPool.TryDequeue(out var node))
            throw new InvalidOperationException(
                "No emulator node pairs available. All grid nodes are in use.");

        _leasedNode.Value = node;
        string environmentFlex = node.Item1;
        string environmentDriver = node.Item2;

        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("Config/appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var appiumSection = config.GetSection("Appium");
        string serverUrl = appiumSection["ServerUrl"]!;

        if (!config.GetSection($"MobileConfigurations:{environmentFlex}").Exists()) throw new ArgumentException("Invalid environment specified for Flex app.");
        if (!config.GetSection($"MobileConfigurations:{environmentDriver}").Exists()) throw new ArgumentException("Invalid environment specified for Driver app.");
        // 1. INICIALIZAR RECOVERY TRAC FLEX
        var envFlexSection = config.GetSection($"MobileConfigurations:{environmentFlex}");
        var optionsFlex = ConfigureOptions(appiumSection, envFlexSection, "flex");
        _driverFlex.Value = new AndroidDriver(new Uri(serverUrl), optionsFlex);

        // 2. INICIALIZAR RECOVERY TRAC DRIVER
        var envDriverSection = config.GetSection($"MobileConfigurations:{environmentDriver}");
        var optionsDriver = ConfigureOptions(appiumSection, envDriverSection, "driver");
        _driverDriver.Value = new AndroidDriver(new Uri(serverUrl), optionsDriver);

        _environment.Value = $"{environmentFlex} + {environmentDriver}";
    }
    private static AppiumOptions ConfigureOptions(IConfigurationSection appiumSection, IConfigurationSection envSection, string appTarget)
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

        var systemPortStr = envSection["SystemPort"];
        if (!string.IsNullOrWhiteSpace(systemPortStr))
        {
            options.AddAdditionalAppiumOption("appium:systemPort", int.Parse(systemPortStr));
        }

        var mjpegPortStr = envSection["mjpegServerPort"];
        if (!string.IsNullOrWhiteSpace(mjpegPortStr))
        {
            options.AddAdditionalAppiumOption("appium:mjpegServerPort", int.Parse(mjpegPortStr));
        }

        options.AddAdditionalAppiumOption("appium:uiautomator2ServerLaunchTimeout", 120000);
        options.AddAdditionalAppiumOption("appium:newCommandTimeout", int.Parse(appiumSection["NewCommandTimeout"]!));
        options.AddAdditionalAppiumOption("appium:autoGrantPermissions", bool.Parse(appiumSection["AutoGrantPermissions"]!));
        
        // 💡 DYNAMIC TARGET OVERRIDE INSIDE DUAL METHOD TOO
        string package;
        string activity;

        if (string.Equals(appTarget, "driver", StringComparison.OrdinalIgnoreCase))
        {
            package = "com.tetratech.recoverytracdriver";
            activity = "com.tetratech.recoverytracdriver.MainActivity";
        }
        else
        {
            package = "com.tetratech.recoverytracflex";
            activity = "com.tetratech.recoverytracflex.MainActivity";
        }

        options.AddAdditionalAppiumOption("appium:appPackage", package);
        options.AddAdditionalAppiumOption("appium:appActivity", activity);

        return options;
    }

    public static void Quit()
    {
        // Return leased node back to the pool so next test class can use it
        if (_leasedNode.Value != null)
        {
            _availableNodesPool.Enqueue(_leasedNode.Value);
            _leasedNode.Value = null;
        }

        // 1. Clean Base Driver
        try
        {
            _driver.Value?.Quit();
        }
        catch { /* Suppress runtime loop faults */ }
        _driver.Value = null!;
        
        // 2. Clean DriverFlex
        try 
        { 
            _driverFlex.Value?.Quit(); 
        } 
        catch { /* Suppress runtime loop faults */ }
        _driverFlex.Value = null!;

        // 3. Clean DriverDriver
        try 
        { 
            _driverDriver.Value?.Quit(); 
        } 
        catch { /* Suppress runtime loop faults */ }
        _driverDriver.Value = null!;

    }
}
