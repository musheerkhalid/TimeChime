using System.Windows;

namespace TimeChime.App.Services;

public static class ThemeManager
{
    public static void ApplyTheme(string theme)
    {
        if (Application.Current == null) return;

        bool isDark = true; // default dark for utility look
        if (string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase))
        {
            isDark = false;
        }
        else if (string.Equals(theme, "system", StringComparison.OrdinalIgnoreCase))
        {
            // Detect Windows system app theme from registry
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int val)
                {
                    isDark = val == 0;
                }
            }
            catch
            {
                isDark = true;
            }
        }

        string themeUri = isDark ? "Resources/DarkTheme.xaml" : "Resources/LightTheme.xaml";

        var appMerged = Application.Current.Resources.MergedDictionaries;
        var existing = appMerged.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Theme.xaml"));

        var newDict = new ResourceDictionary { Source = new Uri(themeUri, UriKind.Relative) };

        if (existing != null)
        {
            int index = appMerged.IndexOf(existing);
            appMerged[index] = newDict;
        }
        else
        {
            appMerged.Add(newDict);
        }
    }
}

