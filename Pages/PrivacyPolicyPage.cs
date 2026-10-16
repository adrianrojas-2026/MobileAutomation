using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;

namespace MobileAutomation.Tests.Pages;

public class PrivacyPolicyPage : BasePage
{
    public PrivacyPolicyPage(AndroidDriver driver)
        : base(driver)
    {
    }

    // Locates the "I Agree" button inside the Ionic container via web-compatible XPath elements
    private By AgreeButton => By.XPath("//ion-button[contains(., 'I AGREE')] | //button[contains(., 'I AGREE')]");

    public void ClickAgree()
    {
        Click(AgreeButton);
    }
}
