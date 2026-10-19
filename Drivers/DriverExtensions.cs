using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;

namespace MobileAutomation.Tests.Drivers;

public static class DriverExtensions
{
    // Hides the soft keyboard on Android sessions; no-op for web drivers.
    public static void HideKeyboardIfShown(this IWebDriver driver)
    {
        if (driver is AndroidDriver androidDriver && androidDriver.IsKeyboardShown())
        {
            androidDriver.HideKeyboard();
        }
    }
}
