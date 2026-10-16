using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;

namespace MobileAutomation.Tests.Pages;

public class HomePage : BasePage
{
    public HomePage(AndroidDriver driver)
        : base(driver)
    {
    }

    // Core elements to assert successful login and policy acceptance
    public By SelectProjectSection => By.XPath("//*[contains(text(), 'SELECT A PROJECT')]");
    
    public By ActivityLogReminder => By.XPath("//*[contains(text(), 'You must sign and submit your Activity Log')]");
    
    public By ClockInButton => By.XPath("//button[contains(., 'Clock In')] | //ion-button[contains(., 'Clock In')]");

    private By BurgerMenu => By.XPath("//button[@aria-label='Menu']");

    public void OpenMenu()
    {
        Click(BurgerMenu);
    }
}