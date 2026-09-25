// Pages/DriverApp/DriverMapPage.cs
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace MobileAutomation.Tests.Pages.Driver;
    public class DriverAppPage
    {
        private readonly AndroidDriver _driver;
        private readonly WebDriverWait _wait;

        private readonly By _btnAceptarViaje = By.Id("driver.package:id/btn_aceptar_viaje");

        public DriverAppPage(AndroidDriver driver)
        {
            _driver = driver;
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(15));
        }

        // Creas un método dedicado a la acción
        public void GenerarTicket()
        {
            var btn = _driver.FindElement(By.Id("com.tetratech.recoverytracdriver:id/btn_generate_ticket"));
            btn.Click();
        }
        public bool ValidateAlertVisibleTrip()
        {
            try
            {
                // 4. En lugar de buscarlo directamente, esperamos a que el elemento sea visible en pantalla
                var elemento = _wait.Until(drv => drv.FindElement(_btnAceptarViaje).Displayed);
                return elemento;
            }
            catch (WebDriverTimeoutException)
            {
                // Si pasan los 15 segundos y no aparece, retorna false sin romper el hilo abruptamente
                return false;
            }
        }

        public void AceptarViaje()
        {
            var boton = _wait.Until(drv => {
                var elem = drv.FindElement(_btnAceptarViaje);
                return (elem.Displayed && elem.Enabled) ? elem : null;
            });

        boton?.Click();
        }
    }
