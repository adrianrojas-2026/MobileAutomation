using Microsoft.Extensions.Configuration;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.iOS;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using MobileAutomation.Tests.Utilities;

namespace MobileAutomation.Tests.Drivers;

public static class DriverFactory
{
    private static readonly ThreadLocal<Tuple<string, string>?> _leasedNode = new();
    private static ThreadLocal<AppiumDriver> _driver = new();
    public static AppiumDriver Driver => _driver.Value!;
    public static AndroidDriver AndroidDriver => (AndroidDriver)Driver;

    // --- NUEVOS CONTENEDORES EXCLUSIVOS (Para escenarios simultáneos Flex + Driver) ---
    private static ThreadLocal<IWebDriver> _driverFlex = new();
    private static ThreadLocal<IWebDriver> _driverDriver = new();

    public static IWebDriver DriverFlex => _driverFlex.Value!;
    public static IWebDriver DriverDriver => _driverDriver.Value!;

    // The Driver app session is opened on demand (StartDriverApp) once the Flex side has something for it to act on,
    // so it doesn't sit idle past newCommandTimeout during the long Flex flow
    private static ThreadLocal<Func<IWebDriver>?> _driverDriverLauncher = new();

    // Files each browser's fake camera plays (Web mode only; null on emulators)
    private static readonly ThreadLocal<string?> _flexCameraFeedPath = new();
    private static readonly ThreadLocal<string?> _driverCameraFeedPath = new();
    public static string? FlexCameraFeedPath => _flexCameraFeedPath.Value;
    public static string? DriverCameraFeedPath => _driverCameraFeedPath.Value;

    // --- Shared environment variables ---
    private static readonly ThreadLocal<string> _environment = new();
    private static readonly ThreadLocal<string> _platformVersion = new();
    private static readonly ThreadLocal<string> _deviceName = new();
    private static readonly ThreadLocal<string> _udid = new();

    public static string CurrentEnvironment => _environment.Value ?? string.Empty;
    public static string CurrentPlatformVersion => _platformVersion.Value ?? string.Empty;
    public static string CurrentDeviceName => _deviceName.Value ?? string.Empty;
    public static string CurrentUdid => _udid.Value ?? string.Empty;
    private static readonly ThreadLocal<string> _loginEmail = new();
    private static readonly ThreadLocal<string> _loginPassword = new();

    public static string LoginEmail => _loginEmail.Value ?? string.Empty;
    public static string LoginPassword => _loginPassword.Value ?? string.Empty;

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
            .AddUserSecrets(typeof(DriverFactory).Assembly, optional: true)
            .Build();

        var appiumSection = config.GetSection("Appium");
        var envSection = config.GetSection($"MobileConfigurations:{environment}");

        if (!envSection.Exists())
        {
            throw new ArgumentException($"The configuration profile key '{environment}' was not found in appsettings.json.");
        }

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
        }
        else
        {
            options.AddAdditionalAppiumOption("appium:automationName", "XCUITest");
            _driver.Value = new IOSDriver(new Uri(appiumSection["ServerUrl"]!), options);
        }
    }
    
    public static void InitializeDualApps()
    {
        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("Config/appsettings.json", optional: false, reloadOnChange: false)
            .AddUserSecrets(typeof(DriverFactory).Assembly, optional: true)
            .Build();

        string runMode = config["TestSettings:RunMode"] ?? "Mobile";
        string webUrl = config["TestSettings:WebBaseUrl"] ?? "http://localhost:8100";
        string driverWebUrl = config["TestSettings:DriverWebBaseUrl"] ?? "http://localhost:8200";

        _loginEmail.Value = config["TestSettings:LoginEmail"]
            ?? throw new InvalidOperationException("LoginEmail setting is missing. Set it with 'dotnet user-secrets set TestSettings:LoginEmail <value>'.");
        _loginPassword.Value = config["TestSettings:LoginPassword"]
            ?? throw new InvalidOperationException("LoginPassword setting is missing. Set it with 'dotnet user-secrets set TestSettings:LoginPassword <value>'.");

        // =========================================================================
        // SCENARIO 1: DEV MODE ON LOCALHOST (Launches web browsers directly)
        // =========================================================================
        if (string.Equals(runMode, "Web", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("🌐 RunMode=Web. Launching desktop browser wrappers...");
            
            var chromeOptions = new ChromeOptions();
            chromeOptions.AddArgument("--start-maximized");
            // ADD THESE TWO LINES HERE TO SUPPRESS WEB POPUPS:
            chromeOptions.AddArgument("--disable-notifications");
            chromeOptions.AddArgument("--disable-popup-blocking");
            // Both apps scan QR codes with the camera: auto-grant it (no "Use camera?" prompt) and point it at a file
            // the test rewrites with the code to scan (see FakeCameraFeed)
            string cameraFeedDirectory = Path.Combine(AppContext.BaseDirectory, "FakeCamera");
            _flexCameraFeedPath.Value = Path.Combine(cameraFeedDirectory, $"flex_{Guid.NewGuid():N}.y4m");
            _driverCameraFeedPath.Value = Path.Combine(cameraFeedDirectory, $"driver_{Guid.NewGuid():N}.y4m");
            FakeCameraFeed.WriteBlank(_flexCameraFeedPath.Value);
            FakeCameraFeed.WriteBlank(_driverCameraFeedPath.Value);
            string driverCameraFeedPath = _driverCameraFeedPath.Value;

            chromeOptions.AddArgument("--use-fake-ui-for-media-stream");
            chromeOptions.AddArgument("--use-fake-device-for-media-stream");
            chromeOptions.AddArgument($"--use-file-for-fake-video-capture={_flexCameraFeedPath.Value}");

            _driverFlex.Value = new ChromeDriver(chromeOptions);
            _driverFlex.Value.Navigate().GoToUrl(webUrl);

            _driverDriverLauncher.Value = () =>
            {
                var edgeOptions = new EdgeOptions();
                edgeOptions.AddArgument("--start-maximized");
                edgeOptions.AddArgument("--disable-notifications");
                edgeOptions.AddArgument("--use-fake-ui-for-media-stream");
                edgeOptions.AddArgument("--use-fake-device-for-media-stream");
                edgeOptions.AddArgument($"--use-file-for-fake-video-capture={driverCameraFeedPath}");
                var edge = new EdgeDriver(edgeOptions);
                edge.Navigate().GoToUrl(driverWebUrl);
                return edge;
            };

            _environment.Value = "Localhost_Web_Development";
            return;
        }

        // =========================================================================
        // SCENARIO 2: PARALLEL EMULATOR SUITE (Launches Android Appium server nodes)
        // =========================================================================
        if (!_availableNodesPool.TryDequeue(out var node))
            throw new InvalidOperationException(
                "No emulator node pairs available. All grid nodes are in use.");

        _leasedNode.Value = node;
        string environmentFlex = node.Item1;
        string environmentDriver = node.Item2;

        var appiumSection = config.GetSection("Appium");
        string serverUrl = appiumSection["ServerUrl"]!;

        if (!config.GetSection($"MobileConfigurations:{environmentFlex}").Exists()) throw new ArgumentException("Invalid environment specified for Flex app.");
        if (!config.GetSection($"MobileConfigurations:{environmentDriver}").Exists()) throw new ArgumentException("Invalid environment specified for Driver app.");
                
        // 1. INITIALIZE RECOVERY TRAC FLEX

        var envFlexSection = config.GetSection($"MobileConfigurations:{environmentFlex}");
        var optionsFlex = ConfigureOptions(appiumSection, envFlexSection, "flex");
        _driverFlex.Value = new AndroidDriver(new Uri(serverUrl), optionsFlex);

        // Debug APKs call the local Aspire services on the emulator's own "localhost"; the reverse forwards don't
        // survive across sessions, so (re)apply them once the session is up
        var aspirePorts = config.GetSection("TestSettings:AspireServicePorts").GetChildren()
            .Select(port => int.Parse(port.Value!)).ToArray();
        AdbHelper.ReversePorts(envFlexSection["Udid"], aspirePorts);

        // 2. INITIALIZE RECOVERY TRAC DRIVER
        var envDriverSection = config.GetSection($"MobileConfigurations:{environmentDriver}");
        var optionsDriver = ConfigureOptions(appiumSection, envDriverSection, "driver");
        _driverDriverLauncher.Value = () =>
        {
            var driverApp = new AndroidDriver(new Uri(serverUrl), optionsDriver);
            AdbHelper.ReversePorts(envDriverSection["Udid"], aspirePorts);
            return driverApp;
        };

        _environment.Value = $"{environmentFlex} + {environmentDriver}";
    }
    // Opens the RecoveryTrac Driver app: Edge on localhost in Web mode, the paired emulator in Mobile mode
    public static IWebDriver StartDriverApp()
    {
        if (_driverDriver.Value != null) return _driverDriver.Value;

        var launcher = _driverDriverLauncher.Value
            ?? throw new InvalidOperationException("InitializeDualApps() must run before StartDriverApp().");
        Console.WriteLine("🚚 Starting the RecoveryTrac Driver app instance...");
        _driverDriver.Value = launcher();
        return _driverDriver.Value;
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

        options.AddAdditionalAppiumOption("appium:noReset", true);
        options.AddAdditionalAppiumOption("appium:shouldTerminateApp", true);
        options.AddAdditionalAppiumOption("appium:clearDeviceLogsOnStart", true);

        options.AddAdditionalAppiumOption("appium:uiautomator2ServerLaunchTimeout", 240000);
        options.AddAdditionalAppiumOption("appium:uiautomator2ServerInstallTimeout", 240000);
        options.AddAdditionalAppiumOption("appium:newCommandTimeout", int.Parse(appiumSection["NewCommandTimeout"]!));
        options.AddAdditionalAppiumOption("appium:autoGrantPermissions", bool.Parse(appiumSection["AutoGrantPermissions"]!));
        // Add this line to prevent older Android emulators from crashing on boot settings check
        options.AddAdditionalAppiumOption("appium:ignoreHiddenApiPolicyError", true);

        // ADD THIS LINE AS A BACKUP FOR ANDROID 13/14 ALERTS:
        options.AddAdditionalAppiumOption("appium:autoAcceptAlerts", true);

        // Lets Appium fetch a chromedriver matching each emulator's WebView version (needs the server started with
        // --allow-insecure=uiautomator2:chromedriver_autodownload), so tests can switch into the WEBVIEW context
        options.AddAdditionalAppiumOption("appium:chromedriverAutodownload", bool.Parse(appiumSection["ChromedriverAutodownload"] ?? "false"));

        // 💡 DYNAMIC TARGET OVERRIDE INSIDE DUAL METHOD TOO
        
        string package = string.Equals(appTarget, "driver", StringComparison.OrdinalIgnoreCase) 
            ? "com.tetratech.recoverytracdriver" 
            : "com.tetratech.recoverytracflex";

        string activity = string.Equals(appTarget, "driver", StringComparison.OrdinalIgnoreCase)
            ? "com.tetratech.recoverytracdriver.MainActivity"
            : "com.tetratech.recoverytracflex.MainActivity";

        options.AddAdditionalAppiumOption("appium:appPackage", package);
        options.AddAdditionalAppiumOption("appium:appActivity", activity);

        return options;
    }

    public static void SwitchToWebViewContext(IWebDriver driver)
    {
        // 💡 Only execute context switching if we are running an actual mobile Appium session
        if (driver is AndroidDriver androidDriver)
        {
            Console.WriteLine("📱 Mobile Mode: Scanning for active Hybrid WebViews...");
            
            // Give Appium up to 5 seconds to load and recognize the underlying WebView layer
            var contextWait = new OpenQA.Selenium.Support.UI.WebDriverWait(androidDriver, TimeSpan.FromSeconds(5));
            try
            {
                contextWait.Until(d => androidDriver.Contexts.Count > 1);
                
                // Select the target context node that contains the word 'WEBVIEW'
                string webviewContext = androidDriver.Contexts.FirstOrDefault(c => c.Contains("WEBVIEW")) 
                    ?? androidDriver.Contexts.Last(); // Fallback to last initialized container
                
                Console.WriteLine($"📱 Mobile Mode: Switching context from NATIVE_APP to => {webviewContext}");
                androidDriver.Context = webviewContext;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Context switch to WEBVIEW failed: {ex.Message}. Remaining in native context.");
            }
        }
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
        _driverDriverLauncher.Value = null;

        // 4. Remove the fake camera feed files (browsers are closed, so they're no longer locked)
        foreach (var feed in new[] { _flexCameraFeedPath, _driverCameraFeedPath })
        {
            try
            {
                if (feed.Value != null) File.Delete(feed.Value);
            }
            catch { /* Best effort cleanup */ }
            feed.Value = null;
        }

    }
}
