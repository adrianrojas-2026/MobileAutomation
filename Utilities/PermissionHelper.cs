using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace MobileAutomation.Tests.Utilities;

public static class PermissionHelper
{
    // Native Android System UI locators for permission buttons
    private static readonly By NativeAllowButton = By.Id("com.android.permissioncontroller:id/permission_allow_button");
    private static readonly By NativeAllowForegroundButton = By.Id("com.android.permissioncontroller:id/permission_allow_foreground_only_button");

    /// <summary>
    /// Automatically detects and clicks "Allow" on any native Android OS system permission popups 
    /// (Notifications, Camera, Location, etc.) within a short window.
    /// </summary>
    /// <param name="driver">The specific AndroidDriver instance experiencing the popup.</param>
    /// <param name="timeoutSeconds">Max time to wait for the popup to show up (default is small for speed).</param>
    public static void HandleSystemPermissions(AndroidDriver driver, int timeoutSeconds = 4)
    {
        var shortWait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            // Wait until either the standard Allow button OR the "While using the app" button is visible
            IWebElement? buttonToClick = shortWait.Until(drv =>
            {
                try
                {
                    var allowBtn = drv.FindElement(NativeAllowButton);
                    if (allowBtn.Displayed) return allowBtn;
                }
                catch (NoSuchElementException) { /* Continue checking next locator */ }

                try
                {
                    var foregroundBtn = drv.FindElement(NativeAllowForegroundButton);
                    if (foregroundBtn.Displayed) return foregroundBtn;
                }
                catch (NoSuchElementException) { /* Both are missing in this poll cycle */ }

                return null;
            });

            if (buttonToClick != null)
            {
                buttonToClick.Click();
                Console.WriteLine($"[PermissionHelper] Native Android permission dialog bypassed successfully.");
                
                // Recursive call to check if another system permission dialog appears immediately after (e.g., Notification followed by Camera)
                HandleSystemPermissions(driver, timeoutSeconds: 2);
            }
        }
        catch (WebDriverTimeoutException)
        {
            // No permission popup appeared within the timeframe; safely proceed with the test execution flow
            Console.WriteLine("[PermissionHelper] No native permission dialogs detected. Moving forward.");
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