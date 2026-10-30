using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace MobileAutomation.Tests.Pages;

// Flex "Send via Bluetooth" picker (app-ble-truck-picker), opened from a submitted card's Bluetooth icon. It searches
// for advertising Driver apps for 15 s, lists each by its truck code (RTD-XXXX), sends the ticket to the one tapped and
// shows the outcome in its status line ("Sent to RTD-XXXX" or a failure message).
public class BleTruckPickerPage : BasePage
{
    public BleTruckPickerPage(IWebDriver driver)
        : base(driver)
    {
    }

    private const string Picker = "//app-ble-truck-picker";

    private By Title => By.XPath($"{Picker}//ion-title[normalize-space()='Send via Bluetooth']");
    private By Searching => By.XPath($"{Picker}//*[contains(@class,'picker-searching')]");
    private By ScanMessage => By.XPath($"{Picker}//*[contains(@class,'picker-message')]");
    // Only the success / danger tones render an ion-text; in-flight stages are plain text
    private By FinalStatus => By.XPath($"{Picker}//*[contains(@class,'picker-status')]//ion-text");
    private By TruckRow(string truckCode) => By.XPath($"{Picker}//ion-item[.//ion-label[normalize-space()='{truckCode}']]");
    private By TruckLabels => By.XPath($"{Picker}//ion-item//ion-label[contains(@class,'truck-name')]");
    private By RescanButton => By.XPath($"{Picker}//ion-footer//ion-button[contains(@class,'left-button')]");
    private By CloseButton => By.XPath($"{Picker}//ion-footer//ion-button[contains(@class,'right-button')]");

    public bool IsOpen(int timeoutSeconds = 15) => FindVisible(Title, timeoutSeconds) != null;

    // Truck codes currently listed
    public IReadOnlyList<string> ListedTrucks() =>
        Driver.FindElements(TruckLabels).Where(e => e.Displayed).Select(e => e.Text.Trim()).Where(t => t.Length > 0).ToList();

    // Waits for the truck to be listed, searching again (Rescan) whenever a search ends without it: the first search
    // can end before the Driver app advertises, or fail while the Nearby devices permission was still being asked
    public void WaitForTruck(string truckCode, int searches = 3)
    {
        for (int search = 1; search <= searches; search++)
        {
            var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(25));
            wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
            string outcome = wait.Until(d =>
            {
                if (d.FindElements(TruckRow(truckCode)).Any(e => e.Displayed)) return "found";
                bool searching = d.FindElements(Searching).Any(e => e.Displayed);
                return searching ? null : "ended";
            });
            if (outcome == "found") return;

            string message = Driver.FindElements(ScanMessage).FirstOrDefault(e => e.Displayed)?.Text.Trim() ?? string.Empty;
            Console.WriteLine($"📡 Search {search} ended without {truckCode} (listed: [{string.Join(", ", ListedTrucks())}]" +
                              $"{(message.Length > 0 ? $", message: '{message}'" : string.Empty)}).");
            if (search < searches) ScrollAndClick(RescanButton, jsClickWhenIntercepted: true);
        }

        throw new WebDriverTimeoutException(
            $"Flex did not find the Driver app '{truckCode}' after {searches} Bluetooth searches (listed: [{string.Join(", ", ListedTrucks())}]).");
    }

    public void SelectTruck(string truckCode) => ScrollAndClick(TruckRow(truckCode), jsClickWhenIntercepted: true);

    // Waits for the send to finish (Preparing / Connecting / Sending / Waiting for confirmation are in-flight) and
    // returns the final status line: "Sent to <truck>" or the failure message
    public string WaitForSendResult(int timeoutSeconds = 60)
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds));
        wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
        return wait.Until(d =>
        {
            // Read the text from the final status itself, not the status line: that may still hold the in-flight
            // text when the final status replaces it
            var final = d.FindElements(FinalStatus).FirstOrDefault(e => e.Displayed);
            string text = final?.Text.Trim() ?? string.Empty;
            return text.Length > 0 ? text : null;
        });
    }

    public void Close()
    {
        ScrollAndClick(CloseButton, jsClickWhenIntercepted: true);
        var closedWait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
        closedWait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
        closedWait.Until(d => !d.FindElements(Title).Any(e => e.Displayed));
    }
}
