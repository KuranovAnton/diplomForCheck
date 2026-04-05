using Microsoft.Win32;
using System;
using System.Windows;

namespace diplomnarabotki
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Устанавливаем версию IE для WebBrowser
            SetBrowserEmulation();
        }

        private void SetBrowserEmulation()
        {
            string appName = System.Diagnostics.Process.GetCurrentProcess().ProcessName + ".exe";

            try
            {
                // Создаем ключи, если их нет
                string featureControlPath = @"Software\Microsoft\Internet Explorer\Main\FeatureControl";

                // FEATURE_BROWSER_EMULATION - для версии IE
                using (var key = Registry.CurrentUser.OpenSubKey(featureControlPath + @"\FEATURE_BROWSER_EMULATION", true))
                {
                    if (key == null)
                    {
                        Registry.CurrentUser.CreateSubKey(featureControlPath + @"\FEATURE_BROWSER_EMULATION");
                    }
                }

                // FEATURE_LOCALSTORAGE - для поддержки localStorage
                using (var key = Registry.CurrentUser.OpenSubKey(featureControlPath + @"\FEATURE_LOCALSTORAGE", true))
                {
                    if (key == null)
                    {
                        Registry.CurrentUser.CreateSubKey(featureControlPath + @"\FEATURE_LOCALSTORAGE");
                    }
                }

                // FEATURE_WEBSOCKET - для поддержки WebSocket (опционально)
                using (var key = Registry.CurrentUser.OpenSubKey(featureControlPath + @"\FEATURE_WEBSOCKET", true))
                {
                    if (key == null)
                    {
                        Registry.CurrentUser.CreateSubKey(featureControlPath + @"\FEATURE_WEBSOCKET");
                    }
                }

                // Устанавливаем версию IE11 Edge режим (11001)
                // 11000 - IE11
                // 11001 - IE11 Edge режим (рекомендуется)
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION",
                    appName, 11001, RegistryValueKind.DWord);

                // Включаем localStorage (1 = включен)
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_LOCALSTORAGE",
                    appName, 1, RegistryValueKind.DWord);

                // Включаем WebSocket (1 = включен)
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_WEBSOCKET",
                    appName, 1, RegistryValueKind.DWord);

                // Включаем поддержку JSON (1 = включен)
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_JSON",
                    appName, 1, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                // Игнорируем ошибки доступа к реестру, но логируем
                System.Diagnostics.Debug.WriteLine($"Ошибка настройки реестра: {ex.Message}");
            }
        }
    }
}