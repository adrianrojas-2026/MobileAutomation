using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;

namespace RecoveryTracTests
{
    [TestFixture]
    public class EscenariosCruzadosTests
    {
        // 1. Declarar los ThreadLocal de forma simple sin inicializar internamente sus valores aquí
        private static ThreadLocal<AndroidDriver> _driverFlex = new ThreadLocal<AndroidDriver>();
        private static ThreadLocal<AndroidDriver> _driverDriver = new ThreadLocal<AndroidDriver>();

        // 2. Propiedades públicas limpias para exponer los drivers a los tests
        public static AndroidDriver DriverFlex => _driverFlex.Value!;
        public static AndroidDriver DriverDriver => _driverDriver.Value!;

        [SetUp]
        public void InicializarEmuladores()
        {
            // El servidor de Appium 2.x consolidado
            Uri appiumServerUri = new Uri("http://127.0.0.1:4723/");

            // --- CONFIGURACIÓN PARA RECOVERY TRAC FLEX ---
            var optionsFlex = new AppiumOptions();
            optionsFlex.PlatformName = "Android";
            optionsFlex.AutomationName = "UiAutomator2";
            
            optionsFlex.AddAdditionalAppiumOption("appium:deviceName", "Emulador_Flex_5554");
            optionsFlex.AddAdditionalAppiumOption("appium:udid", "emulator-5554"); // ID from 'adb devices'
            optionsFlex.AddAdditionalAppiumOption("appium:app", @"C:\apks\recovery_trac_flex.apk");
            optionsFlex.AddAdditionalAppiumOption("appium:systemPort", 8201);

            // ASIGNACIÓN CORRECTA: Aquí es donde se le da el valor real al ThreadLocal
            _driverFlex.Value = new AndroidDriver(appiumServerUri, optionsFlex);

            // --- CONFIGURACIÓN PARA RECOVERY TRAC DRIVER ---
            var optionsDriver = new AppiumOptions();
            optionsDriver.PlatformName = "Android";
            optionsDriver.AutomationName = "UiAutomator2";

            optionsFlex.AddAdditionalAppiumOption("appium:deviceName", "Emulador_Flex_5556");
            optionsFlex.AddAdditionalAppiumOption("appium:udid", "emulator-5556"); // ID from 'adb devices'
            optionsFlex.AddAdditionalAppiumOption("appium:app", @"C:\apks\recovery_trac_driver.apk");
            optionsFlex.AddAdditionalAppiumOption("appium:systemPort", 8202); 
            
            // ASIGNACIÓN CORRECTA: Aquí se le da el valor real al segundo ThreadLocal
            _driverDriver.Value = new AndroidDriver(appiumServerUri, optionsDriver);
        }

        [TearDown]
        public void FinalizarSesiones()
        {
            // Libera de forma segura la memoria de cada emulador al terminar
            if (_driverFlex.IsValueCreated && _driverFlex.Value != null)
            {
                _driverFlex.Value.Quit();
                _driverFlex.Value.Dispose();
            }

            if (_driverDriver.IsValueCreated && _driverDriver.Value != null)
            {
                _driverDriver.Value.Quit();
                _driverDriver.Value.Dispose();
            }
        }

        [OneTimeTearDown]
        public static void LiberarContenedoresEstaticos()
        {
            // 2. SOLUCIÓN AL NUnit1032: Esto destruye los ThreadLocal estáticos 
            // al finalizar TODA la suite de pruebas de esta clase.
            _driverFlex?.Dispose();
            _driverDriver?.Dispose();
        }
    }
}
