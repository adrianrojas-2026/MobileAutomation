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

    public static void HandleSystemPermissions(AndroidDriver driver)
    {
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