// Pages/DriverApp/DriverMapPage.cs
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace MobileAutomation.Tests.Pages.Driver;
    public class DriverAppPage
    {
        private readonly AndroidDriver _driver;
        private readonly WebDriverWait _wait;

        private readonly By _btnAceptarViaje = By.Id("driver.package:id/btn_aceptar_viaje");
        // Replace this ID with your actual RecoveryTrac Driver QR element locator ID
        private readonly By _qrCodeImageLocator = By.Id("com.tetratech.recoverytracdriver:id/img_ticket_qr_code");


        public DriverAppPage(AndroidDriver driver)
        {
            _driver = driver;
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(15));
        }

        // Create method for generating a ticket// DriverAppPage.GetOnlyQrCodeAsBase64()
        public void GenerateTicket()
        {
            var btn = _driver.FindElement(By.Id("com.tetratech.recoverytracdriver:id/btn_generate_ticket"));
            btn.Click();
        }
        public bool ValidateAlertVisibleTrip()
        {
            try
            {
                // 4. Instead of searching for it directly, we wait for the element to be visible on screen
                var elemento = _wait.Until(drv => drv.FindElement(_btnAceptarViaje).Displayed);
                return elemento;
            }
            catch (WebDriverTimeoutException)
            {
                // if 15 seconds pass and the element does not appear, return false without abruptly breaking the thread
                return false;
            }
        }

        public void AcceptTrip()
        {
            var boton = _wait.Until(drv => {
                var elem = drv.FindElement(_btnAceptarViaje);
                return (elem.Displayed && elem.Enabled) ? elem : null;
            });

        boton?.Click();
        }

         /// Dynamically waits for the QR code element to render completely on the driver's screen.
        /// </summary>
        /// <param name="timeoutSeconds">Maximum time to wait before throwing an exception.</param>
        /// <returns>True if the QR code is fully visible and ready for screen capture.</returns>
        public bool IsQrCodeRendered(int timeoutSeconds = 5)
        {
            try
            {
                var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSeconds));
                // Wait until the element is physically present and displayed
                var qrElement = wait.Until(d => d.FindElement(_qrCodeImageLocator));
                return qrElement.Displayed;
            }
            catch (WebDriverTimeoutException)
            {
                return false;
            }
        }

        // Get the QR code as a Base64 string directly from the element, ensuring we capture only the QR code and not the entire screen.
        public string GetQrCodeBase64(IWebElement qrElement)
        {
            // Appium permite tomar un screenshot directamente de un elemento específico del DOM/UI
            var elementScreenshot = ((ITakesScreenshot)qrElement).GetScreenshot();
            return elementScreenshot.AsBase64EncodedString;
        }

        public string GetOnlyQrCodeAsBase64()
        {
            // Finding the element using the locator you already defined above
            IWebElement qrElement = _wait.Until(d => d.FindElement(_qrCodeImageLocator));
            return GetQrCodeBase64(qrElement);
        }
    }
