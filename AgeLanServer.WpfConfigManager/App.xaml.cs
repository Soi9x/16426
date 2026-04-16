using System.Windows;
using WpfThemeMode = AgeLanServer.WpfConfigManager.Models.ThemeMode;

namespace AgeLanServer.WpfConfigManager;

public partial class App : Application
{
    private const int ThemeDictionaryIndex = 1;

    public void ApplyTheme(WpfThemeMode theme)
    {
        var dictionaryPath = theme == WpfThemeMode.Dark
            ? "Themes/DarkTheme.xaml"
            : "Themes/LightTheme.xaml";

        var themeDictionary = new ResourceDictionary
        {
            Source = new Uri(dictionaryPath, UriKind.Relative)
        };

        if (Resources.MergedDictionaries.Count > ThemeDictionaryIndex)
        {
            Resources.MergedDictionaries[ThemeDictionaryIndex] = themeDictionary;
            return;
        }

        Resources.MergedDictionaries.Add(themeDictionary);
    }
}
