using System.Windows;
using ReIcon.Models;

namespace ReIcon.Services;

public static class ThemeService
{
    private static readonly Uri DarkUri = new("pack://application:,,,/Themes/Dark.xaml", UriKind.Absolute);

    public static void Apply(UiTheme theme)
    {
        _ = theme;
        var rd = new ResourceDictionary { Source = DarkUri };
        var merged = Application.Current.Resources.MergedDictionaries;
        if (merged.Count > 0)
            merged[0] = rd;
        else
            merged.Add(rd);

        if (Application.Current is global::ReIcon.App app)
            app.RefreshIconsForTheme(UiTheme.Dark);
    }
}
