using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace MobileAutomation.Tests.Utilities;

public static class WaitHelper
{
    public static IWebElement WaitForElement(
        IWebDriver driver,
        By locator,
        int seconds = 20)
    {
        var wait = new WebDriverWait(
            driver,
            TimeSpan.FromSeconds(seconds));

        return wait.Until(
            d => d.FindElement(locator));
    }
}