using OpenQA.Selenium;

namespace MobileAutomation.Tests.Pages;

// "Not Clocked In" alert raised by Forms > New, and the Clock In modal it opens.
// Project, Task, Labor Category and Work State come pre-filled from the current assignment.
public class ClockInPage : BasePage
{
    public ClockInPage(IWebDriver driver)
        : base(driver)
    {
    }

    private By NotClockedInAlert => By.XPath("//ion-alert[.//*[contains(normalize-space(.),'Not Clocked In')]]");
    private By AlertClockInButton => By.XPath(
        "//ion-alert[.//*[contains(normalize-space(.),'Not Clocked In')]]//button[contains(translate(normalize-space(.),'clockin','CLOCKIN'),'CLOCK IN')]");
    private const string ClockInModal = "//ion-modal[.//ion-title[normalize-space()='Clock In']]";
    private By MileageOption(string option) => By.XPath(
        $"{ClockInModal}//*[contains(normalize-space(.),'track your mileage')]/following::*" +
        $"[self::ion-segment-button or self::ion-button or self::ion-item or self::button][normalize-space()='{option}'][1]");
    private By WorkingOnTextArea => By.XPath($"{ClockInModal}//textarea");
    private By SaveButton => By.XPath(
        $"{ClockInModal}//ion-button[translate(normalize-space(.),'save','SAVE')='SAVE']");

    // Returns true when the user was not clocked in and the Clock In flow was completed
    public bool ClockInIfPrompted(string workingOn, bool trackMileage = false, int promptTimeoutSeconds = 5)
    {
        if (FindVisible(NotClockedInAlert, promptTimeoutSeconds) == null)
        {
            Console.WriteLine("⏩ User is already clocked in. Skipping Clock In flow.");
            return false;
        }

        Console.WriteLine("⏱️ 'Not Clocked In' prompt detected. Clocking in...");
        ScrollAndClick(AlertClockInButton);

        ScrollAndClick(MileageOption(trackMileage ? "Yes" : "No"));
        EnterText(WorkingOnTextArea, workingOn);
        ScrollAndClick(SaveButton);

        // Wait for the modal to close before handing control back to the form flow
        var wait = new OpenQA.Selenium.Support.UI.WebDriverWait(Driver, TimeSpan.FromSeconds(30));
        // The modal can be removed mid-check; poll again instead of failing on the stale element
        wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
        wait.Until(d => !d.FindElements(By.XPath(ClockInModal)).Any(e => e.Displayed));
        return true;
    }
}
