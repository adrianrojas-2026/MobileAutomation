// Pages/FlexApp/FlexMainPage.cs
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace MobileAutomation.Tests.Pages.FlexApp;
    public class FlexAppPage
    {
        private readonly AndroidDriver _driverFlex;
        private readonly WebDriverWait _wait;

        // Localizadores (Usa los IDs reales que encuentres con Appium Inspector)
        private readonly MobileBy _txtDestino = (MobileBy)MobileBy.Id("flex.package:id/input_destino");
        private readonly MobileBy _btnEnviar = (MobileBy)MobileBy.Id("flex.package:id/btn_enviar");

        public FlexAppPage(AndroidDriver driver)
        {
            _driverFlex = driver;
            _wait = new WebDriverWait(_driverFlex, TimeSpan.FromSeconds(15));
        }

        public void CrearNuevoServicio(string direccion)
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
    }

