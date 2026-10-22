using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace MobileAutomation.Tests.Pages;

// Forms menu, form definition tiles and the In Progress / Submitted summary list.
// Flex is an Ionic hybrid app, so these DOM locators serve both Web mode and the mobile WEBVIEW context.
public class FormsPage : BasePage
{
    public FormsPage(IWebDriver driver)
        : base(driver)
    {
    }

    private By FormsMenuItem => By.XPath("//ion-menu//ion-item[.//ion-label[normalize-space()='Forms']]");
    private By MenuToggleButton => By.CssSelector("ion-menu-button");
    private By FormTile(string formName) => By.XPath(
        $"//app-forms-list//ion-item[.//span[contains(@class,'form-label') and normalize-space()='{formName}']]");
    private By NewButton => By.XPath(
        "//app-form-summary//ion-button[contains(translate(normalize-space(.),'new','NEW'),'NEW')]");
    private By SubmittedTab => By.XPath("//app-form-summary//ion-segment-button[@value='Submitted']");
    private By SubmittedHeader => By.XPath("//app-form-summary//h4[contains(normalize-space(.),'Submitted')]");
    private By TicketIdLabels => By.XPath("//app-form-summary//ion-card//ion-card-subtitle");
    private By SubmittedCard(string ticketId) => By.XPath(
        $"//app-form-summary//ion-card[.//ion-card-subtitle[normalize-space()='{ticketId}']]");

    public void OpenFormsMenu()
    {
        // On narrow (mobile) layouts the side menu is collapsed behind the hamburger button
        if (FindVisible(FormsMenuItem, 2) == null)
        {
            ScrollAndClick(MenuToggleButton);

            // Clicks during the slide-in animation fall through to the page behind (e.g. Home's time card opens
            // Time Card), so wait until Ionic reports the menu fully open; isOpen() resolves after the animation
            new WebDriverWait(Driver, TimeSpan.FromSeconds(10)).Until(_ =>
                ((IJavaScriptExecutor)Driver).ExecuteScript(
                    "const m = document.querySelector('ion-menu'); return m ? m.isOpen() : false;") is true);
        }
        ScrollAndClick(FormsMenuItem);
        Find(By.CssSelector("app-forms-list"));
    }

    public void OpenForm(string formName)
    {
        Console.WriteLine($"Opening form definition '{formName}'...");
        ScrollAndClick(FormTile(formName), 30);
        Find(NewButton);
    }

    public void ClickNew()
    {
        ScrollAndClick(NewButton);
    }

    public void OpenSubmittedTab()
    {
        ScrollAndClick(SubmittedTab);
        Find(SubmittedHeader);
    }

    public IReadOnlyList<string> GetSubmittedTicketIds()
    {
        OpenSubmittedTab();
        // Brief settle so a just-submitted card has time to render before the list is read
        Thread.Sleep(1000);
        return Driver.FindElements(TicketIdLabels).Select(e => e.Text.Trim()).Where(t => t.Length > 0).ToList();
    }

    // Reads the "Label: value" rows (Truck Capacity, Driver Name, ...) from a submitted ticket card
    public Dictionary<string, string> GetSubmittedCardValues(string ticketId)
    {
        var card = Find(SubmittedCard(ticketId));
        var values = new Dictionary<string, string>();
        foreach (var row in card.FindElements(By.CssSelector("div.item-data")))
        {
            var spans = row.FindElements(By.TagName("span"));
            if (spans.Count >= 2)
            {
                values[spans[0].Text.Trim().TrimEnd(':')] = spans[1].Text.Trim();
            }
        }
        return values;
    }

    // Sync runs in the background after submit; wait until the card shows the "Synced" status
    public bool WaitUntilSynced(string ticketId, int timeoutSeconds = 90)
    {
        var syncedStatus = By.XPath(
            $"//app-form-summary//ion-card[.//ion-card-subtitle[normalize-space()='{ticketId}']]" +
            "//ion-button[contains(@class,'sync-success') or translate(normalize-space(.),'synced','SYNCED')='SYNCED']");
        return FindVisible(syncedStatus, timeoutSeconds) != null;
    }
}
