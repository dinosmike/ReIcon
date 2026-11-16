using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using ReIcon.Services;

namespace ReIcon;

public partial class About
{
    public About()
    {
        InitializeComponent();
    }

    private void About_OnLoaded(object sender, RoutedEventArgs e)
    {
        AutoUpdateCheck.IsChecked = SettingsStorage.Load().AutoUpdate;
        UpdateWhatsNewDot();
        ReUpdate.WhatsNewState.Changed += UpdateWhatsNewDot;
        Closed += (_, _) => ReUpdate.WhatsNewState.Changed -= UpdateWhatsNewDot;

        try
        {
            var uri = new Uri("pack://application:,,,/app.ico");
            var decoder = BitmapDecoder.Create(
                uri,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            BitmapFrame? best = null;
            foreach (BitmapFrame frame in decoder.Frames)
            {
                if (best == null || frame.PixelWidth > best.PixelWidth)
                    best = frame;
            }

            if (best == null)
                return;

            if (best.CanFreeze)
                best.Freeze();

            TitleBarIcon.Source = best;
            RenderOptions.SetBitmapScalingMode(TitleBarIcon, BitmapScalingMode.Fant);
        }
        catch
        {
            // оставляем пустую иконку при ошибке декодирования
        }
    }

    private void UpdateWhatsNewDot() =>
        WhatsNewDotAbout.Visibility = ReUpdate.WhatsNewState.HasUnseen ? Visibility.Visible : Visibility.Collapsed;

    private void WhatsNew_OnClick(object sender, RoutedEventArgs e) => ReUpdate.WhatsNewWindow.ShowFor(this);

    private void AutoUpdateCheck_OnChanged(object sender, RoutedEventArgs e)
    {
        var settings = SettingsStorage.Load();
        settings.AutoUpdate = AutoUpdateCheck.IsChecked == true;
        SettingsStorage.Save(settings);
    }

    private void SocialLink_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string url })
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void DonateLink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    public static void Show(Window owner)
    {
        var w = new About { Owner = owner };
        w.ShowDialog();
    }

    private void Ok_OnClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>Скрыто: правый клик по «ОК» — сколько раз скачали и обновили программу (счётчики GitHub).</summary>
    private async void Ok_OnRightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        DownloadStatsText.Text = "…";
        DownloadStatsText.Visibility = Visibility.Visible;
        DownloadStatsText.Text = await ReUpdate.DownloadStats.LoadTextAsync();
    }

    private void TitleBar_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void TitleBarClose_OnClick(object sender, RoutedEventArgs e) => Close();
}
