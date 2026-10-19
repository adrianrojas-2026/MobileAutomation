using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using MobileAutomation.Tests.Drivers;

namespace MobileAutomation.Tests.Pages;

public class PrivacyPolicyPage : BasePage
{
    public PrivacyPolicyPage(IWebDriver driver) 
        : base(driver) 
    { 
    }
    public By AgreeButton
    {
        get
        {
            if (Driver is not OpenQA.Selenium.Appium.Android.AndroidDriver)
            {
                return By.CssSelector("ion-button[slot='end'], ion-button.right-button");
            }
            
            // 💡 FIXED: Stripped the generic '//*' to ensure Appium targets the clickable Button wrapper widget, not an inner text node
            return By.XPath(
                "//android.widget.Button[contains(@text, 'AGREE')] " +
                "| //android.widget.Button[@text='I AGREE']");
        }
    }
    public void ClickAgree()
    {
        // 🌐 WEB ONLY WORKFLOW
        if (Driver is not OpenQA.Selenium.Appium.Android.AndroidDriver && Driver is IJavaScriptExecutor js)
        {
            var scriptQuery = "return document.querySelector('ion-buttons[slot=\"end\"] ion-button') || document.querySelector('ion-button.right-button') || document.querySelector('ion-button');";
            var element = js.ExecuteScript(scriptQuery) as IWebElement;
            if (element != null)
            {
                element.Click();
                return;
            }
        }

        // 📱 MOBILE WORKFLOW
        Console.WriteLine("📱 Mobile Mode: Triggering explicit click sequence on the native button widget...");
        var buttonElement = Driver.FindElement(AgreeButton);
        
        // A standard click works perfectly once locked onto the true android.widget.Button node
        buttonElement.Click();
    }
    public void ClearAllPendingAgreements()
    {
        Console.WriteLine("Scanning for active agreement screens (Privacy Policy / Terms of Service)...");
        bool agreementsPending = true;
        int agreementCounter = 0;

        while (agreementsPending && agreementCounter < 3)
        {
            try
            {
                var agreementWait = new WebDriverWait(Driver, TimeSpan.FromSeconds(3));
                
                var agreeBtn = agreementWait.Until(d => 
                {
                    // 💡 FIX: Use FindElements to safely return an empty collection instead of crashing the runner thread
                    var elements = d.FindElements(AgreeButton);
                    if (elements.Count > 0)
                    {
                        var el = elements.First();
                        return (el != null && el.Displayed && el.Enabled) ? el : null;
                    }
                    return null;
                });

                if (agreeBtn != null)
                {
                    Console.WriteLine($"[Agreement #{agreementCounter + 1}] Found active screen. Clicking 'I AGREE'...");
                    ClickAgree();
                    agreementCounter++;
                    
                    // Brief pause to allow the network layout rendering to settle
                    System.Threading.Thread.Sleep(1000); 
                }
                else
                {
                    agreementsPending = false;
                }
            }
            catch (WebDriverTimeoutException)
            {
                Console.WriteLine("No active agreement screens detected. Moving forward to Dashboard verification.");
                agreementsPending = false;
            }
        }
    }
}
