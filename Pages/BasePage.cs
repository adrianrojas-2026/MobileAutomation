using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace MobileAutomation.Tests.Pages;

public abstract class BasePage
{
    protected readonly AndroidDriver Driver;

    protected BasePage(AndroidDriver driver)
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

        element.Clear();
        element.SendKeys(text);
    }
}