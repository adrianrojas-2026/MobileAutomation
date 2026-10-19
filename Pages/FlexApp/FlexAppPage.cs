// Pages/FlexApp/FlexMainPage.cs
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;

namespace MobileAutomation.Tests.Pages.FlexApp;
    public class FlexAppPage
    {
        private readonly IWebDriver _driverFlex;
        private readonly WebDriverWait _wait;

        // Locators (Use real IDs using Appium Inspector)
        private readonly By _txtDestino = By.Id("flex.package:id/input_destination");
        private readonly By _btnEnviar = By.Id("flex.package:id/btn_enviar");
        private readonly By _errorDialogLocator = By.Id("com.tetratech.recoverytracflex:id/txt_distance_error");
        private readonly By _lblTicketStatus = By.Id("com.tetratech.recoverytracflex:id/txt_ticket_status");
        private readonly By _btnScanQR = By.Id("com.tetratech.recoverytracflex:id/btn_scan_qr");
        private readonly By _cameraOverlay = By.Id("com.tetratech.recoverytracflex:id/camera_preview_view"); // Adjust to your camera view ID
        private readonly By _qrCodeImageLocator = By.Id("com.tetratech.recoverytracflex:id/img_ticket_qr_code"); //TBD
      

        public FlexAppPage(IWebDriver driver)
        {
            _driverFlex = driver;
            _wait = new WebDriverWait(_driverFlex, TimeSpan.FromSeconds(15));
        }

        public void StartScan()
        {
            _driverFlex.FindElement(_btnScanQR).Click();
            // DYNAMIC WAIT: Wait up to 5 seconds for the camera overlay layout to be fully visible
            var wait = new WebDriverWait(_driverFlex, TimeSpan.FromSeconds(5));
            wait.Until(d => d.FindElement(_cameraOverlay).Displayed);
        }

        public bool IsDistanceErrorDisplayed()
        {
            try
            {
                var wait = new WebDriverWait(_driverFlex, TimeSpan.FromSeconds(5));
                return wait.Until(d => d.FindElement(_errorDialogLocator).Displayed);
            }
            catch (WebDriverTimeoutException)
            {
                return false;
            }
        }
        public string GetDistanceErrorMessage()
        {
            var wait = new WebDriverWait(_driverFlex, TimeSpan.FromSeconds(5));
            return wait.Until(d => d.FindElement(_errorDialogLocator)).Text;
        }

        public string GetTicketStatus()
        {
            var wait = new WebDriverWait(_driverFlex, TimeSpan.FromSeconds(5));
            return wait.Until(d => d.FindElement(_lblTicketStatus)).Text;
        }

        public void CreateNewService(string direccion)
        {
            var inputDestino = _wait.Until(drv => {
                var elem = drv.FindElement(_txtDestino);
                return elem.Displayed ? elem : null;
            });
            inputDestino!.SendKeys(direccion);
            
            var botonEnviar = _wait.Until(drv => {
            var elem = drv.FindElement(_btnEnviar);
                return (elem.Displayed && elem.Enabled) ? elem : null;
            });
            botonEnviar!.Click();
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
            var wait = new WebDriverWait(_driverFlex, TimeSpan.FromSeconds(10));
            IWebElement qrElement = wait.Until(d => d.FindElement(_qrCodeImageLocator));
            return GetQrCodeBase64(qrElement);
        }
    }

