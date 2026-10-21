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

    // Centers the element in its scroll container so Ionic footers/toolbars don't intercept the click
    protected void ScrollIntoView(IWebElement element)
    {
        ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].scrollIntoView({block: 'center'});", element);
    }

    // Waits for a visible element, scrolls it into view and clicks it, retrying while overlays/animations intercept the click
    protected void ScrollAndClick(By locator, int timeoutSeconds = 15)
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds));
        wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException),
            typeof(ElementClickInterceptedException), typeof(ElementNotInteractableException));

        wait.Until(d =>
        {
            // Scroll first: Ionic reports content outside the visible scroll area as not displayed
            var element = d.FindElement(locator);
            ScrollIntoView(element);
            if (!element.Displayed) return false;
            element.Click();
            return true;
        });
    }

    // Returns the first visible match, or null once the timeout elapses
    protected IWebElement? FindVisible(By locator, int timeoutSeconds)
    {
        try
        {
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds));
            wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
            return wait.Until(d => d.FindElements(locator).FirstOrDefault(e => e.Displayed));
        }
        catch (WebDriverTimeoutException)
        {
            return null;
        }
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