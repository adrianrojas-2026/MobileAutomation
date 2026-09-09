using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;

namespace MobileAutomation.Tests.Pages;

public class HomePage : BasePage
{
    public HomePage(AndroidDriver driver)
        : base(driver)
    {
    }

    private By BurgerMenu =>
        By.XPath("//button[@aria-label='Menu']");

    public void OpenMenu()
    {
        Click(BurgerMenu);
    }
}