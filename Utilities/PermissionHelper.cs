using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;

namespace MobileAutomation.Tests.Utilities;

public static class PermissionHelper
{
    
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