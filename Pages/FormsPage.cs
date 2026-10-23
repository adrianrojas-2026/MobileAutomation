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

    // ALL FORMS footer button on a form's summary page: back to the form definition list
    public void OpenAllForms()
    {
        ScrollAndClick(By.XPath(
            "//app-form-summary//ion-button[contains(translate(normalize-space(.),'allforms','ALLFORMS'),'ALL FORMS')]"),
            jsClickWhenIntercepted: true);
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

    // QR icon in the top-right corner of a submitted card (next to Print on Load - Disposal); opens the QR modal
    public void OpenSubmittedCardQr(string ticketId)
    {
        ScrollAndClick(By.XPath(
            $"//app-form-summary//ion-card[.//ion-card-subtitle[normalize-space()='{ticketId}']]" +
            "//*[self::ion-button or self::ion-icon][contains(@name,'qr') or .//ion-icon[contains(@name,'qr')]]"),
            jsClickWhenIntercepted: true);
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
