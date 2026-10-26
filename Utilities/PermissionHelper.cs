using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace MobileAutomation.Tests.Utilities;

public static class PermissionHelper
{
        // 💡 Expanded to match standard permission buttons ("Allow", "ALLOW") case-insensitively
    private static readonly By NativeAllowButton = By.XPath(
        "//android.widget.Button[contains(translate(@text, 'ALLOW', 'allow'), 'allow')]");

    // 💡 Expanded to catch location-specific permissions ("While using the app", "Only this time")
    private static readonly By NativeAllowForegroundButton = By.XPath(
        "//android.widget.Button[contains(@text, 'using the app')] " +
        "| //android.widget.Button[contains(@text, 'this time')] " +
        "| //android.widget.Button[contains(translate(@text, 'WHILE USING THE APP', 'while using the app'), 'using the app')]");

    // Android 16+ images with 16 KB pages (e.g. Pixel_10_Pro, ps16k) warn on launch that Flex's native libraries
    // aren't 16 KB aligned. The app still runs, but the dialog covers it. Only matched when its text says "16 KB",
    // so an unrelated "OK" is never tapped. "Don't Show Again" first; the apostrophe varies, so match "Show Again".
    private static readonly By PageSizeCompatDialog = By.XPath("//*[contains(@text, '16 KB')]");
    private static readonly By PageSizeCompatDismissButton = By.XPath(
        "//android.widget.Button[contains(translate(@text, 'SHOW AGAIN', 'show again'), 'show again')]" +
        " | //android.widget.Button[translate(@text, 'ok', 'OK')='OK']");

    public static void HandleSystemPermissions(AndroidDriver driver)
    {
        DismissPageSizeCompatDialog(driver);
        GrantRuntimePermissions(driver);
        try
        {
            // 💡 Comprehensive XPath matching standard Android OS permission dialog choices
            // Targets: "Allow" (Notifications/Files), "While using the app" / "Only this time" (GPS Location)
            var permissionButtonXpath = By.XPath(
                "//android.widget.Button[contains(translate(@text, 'ALLOW', 'allow'), 'allow')] " +
                "| //android.widget.Button[contains(@text, 'using the app')] " +
                "| //android.widget.Button[contains(@text, 'this time')]");

            // Check up to 2 times in case multiple different system popups appear back-to-back
            for (int i = 0; i < 2; i++)
            {
                var buttons = driver.FindElements(permissionButtonXpath);
                if (buttons.Count > 0 && buttons.First().Displayed)
                {
                    Console.WriteLine($"📱 PermissionHelper: Native system dialog button detected. Dismissing layer...");
                    buttons.First().Click();
                    
                    // Brief pause to allow the OS UI layout calculation threads to update
                    System.Threading.Thread.Sleep(1000); 
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PermissionHelper non-blocking notice: {ex.Message}");
        }
    }
    
    // Without location, Clock In opens Flex's "Location Permissions Required" screen instead of saving.
    // Background location is left out: the app works without it (it isn't granted on the older AVDs either).
    private static readonly string[] RuntimePermissions =
    {
        "android.permission.POST_NOTIFICATIONS",
        "android.permission.ACCESS_FINE_LOCATION",
        "android.permission.ACCESS_COARSE_LOCATION"
    };

    // autoGrantPermissions only applies when Appium installs the app, and these sessions attach by appPackage with
    // noReset, so a new AVD (e.g. Pixel_10_Pro) starts with nothing granted. Tapping "Allow" isn't reliable either:
    // on the Android 16 image uiautomator reports the notification dialog's bounds ~400px above where it's drawn,
    // so the tap lands on the title. Granting through pm also closes the dialog if it's already showing.
    public static void GrantRuntimePermissions(AndroidDriver driver)
    {
        if (driver.Capabilities.GetCapability("appPackage") is not string appPackage) return;

        // One at a time: Android 12 and older have no POST_NOTIFICATIONS, and that failure mustn't skip the rest
        foreach (var permission in RuntimePermissions)
        {
            try
            {
                driver.ExecuteScript("mobile: changePermissions", new Dictionary<string, object>
                {
                    ["permissions"] = permission,
                    ["appPackage"] = appPackage,
                    ["action"] = "grant"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PermissionHelper non-blocking notice ({permission}): {ex.Message}");
            }
        }
        System.Threading.Thread.Sleep(1000);
    }

    public static void DismissPageSizeCompatDialog(AndroidDriver driver)
    {
        try
        {
            if (!driver.FindElements(PageSizeCompatDialog).Any(e => e.Displayed)) return;

            // Prefer "Don't Show Again" so the dialog stays away for the next runs; fall back to OK
            var buttons = driver.FindElements(PageSizeCompatDismissButton).Where(b => b.Displayed).ToList();
            var button = buttons.FirstOrDefault(b => b.Text.Contains("Again", StringComparison.OrdinalIgnoreCase)) ?? buttons.FirstOrDefault();
            if (button == null) return;

            Console.WriteLine($"📱 PermissionHelper: 16 KB page-size compatibility dialog detected. Tapping '{button.Text}'...");
            button.Click();
            System.Threading.Thread.Sleep(1000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PermissionHelper non-blocking notice: {ex.Message}");
        }
    }

    public static void HandleNotifications(AndroidDriver driver)
    {
        try
        {
            var allowButton =
                driver.FindElement(
                    By.XPath("//*[@text='Allow']"));

            allowButton.Click();

            Console.WriteLine(
                "Notification permission accepted.");
        }
        catch
        {
            Console.WriteLine(
                "Notification permission not displayed.");
        }
    }
}