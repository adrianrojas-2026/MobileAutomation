using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace MobileAutomation.Tests.Pages;

public abstract class BasePage
{
     protected readonly IWebDriver Driver;

    protected BasePage(IWebDriver driver)
    {
        Driver = driver;
    }

    protected IWebElement Find(By locator)
    {
        var wait = new WebDriverWait(
            Driver,
            TimeSpan.FromSeconds(30));

        return wait.Until(d =>
            d.FindElement(locator));
    }

    protected void Click(By locator)
    {
        Find(locator).Click();
    }

    protected void EnterText(By locator, string text)
    {
        var element = Find(locator);

       // 💡 Check if we are running in Web mode (not an Android Appium Session) before attempting to read browser tag properties
        if (Driver is not OpenQA.Selenium.Appium.Android.AndroidDriver && 
            element.TagName.StartsWith("ion-", StringComparison.OrdinalIgnoreCase))
        {
            // Pierce into the native element child context to perform safe interactions inside Web mode
            var nativeInput = element.FindElement(By.CssSelector("input, textarea"));
            nativeInput.Clear();
            nativeInput.SendKeys(text);
        }
        else
        {
            // Standard action loop fallback execution path for pure HTML fields and mobile emulators
            element.Clear();
            element.SendKeys(text);
        }
    }
}