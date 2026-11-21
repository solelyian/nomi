using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nomi.Prototype.WinUI.Services;

namespace Nomi.Prototype.WinUI.Views;

public sealed partial class FicheView : UserControl
{
    private static readonly (string Text, string Stamp)[] SourceExcerpts =
    {
        ("« poids inférieur au 10ᵉ percentile de l'âge gestationnel »", "12:04"),
        ("« tabac : premier facteur évitable »", "18:37"),
        ("« insuffisance placentaire — mécanisme le plus fréquent »", "22:10"),
        ("« hauteur utérine à chaque consultation »", "27:45"),
        ("« doppler ombilical si suspicion »", "31:02"),
    };

    public FicheView()
    {
        this.InitializeComponent();
        foreach (var (text, stamp) in SourceExcerpts)
        {
            var row = new StackPanel { Spacing = 2 };
            row.Children.Add(new TextBlock { Text = text, Style = (Style)Application.Current.Resources["CaptionText"], FontStyle = Windows.UI.Text.FontStyle.Italic });
            row.Children.Add(new TextBlock { Text = "cours — " + stamp, Style = (Style)Application.Current.Resources["OverlineText"], FontSize = 10 });
            Sources.Children.Add(row);
        }
    }

    private async void OnGenerate(object sender, RoutedEventArgs e)
    {
        GenButton.IsEnabled = false;
        Spinner.IsActive = true; Spinner.Visibility = Visibility.Visible;
        Sections.Children.Clear();

        var sheet = await PrototypeServices.Inference.BuildRevisionSheetAsync("transcription IPP");

        Spinner.IsActive = false; Spinner.Visibility = Visibility.Collapsed;
        foreach (var s in sheet.Sections)
        {
            var (bg, fg, label) = s.Provenance switch
            {
                "nomi" => ("InfoSoftBrush", "InfoBrush", "Explication Nomi"),
                "hypothese" => ("WarnSoftBrush", "WarnBrush", "Hypothèse"),
                _ => ("AccentSoftBrush", "AccentInkBrush", "Cours"),
            };
            var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            head.Children.Add(new TextBlock { Text = s.Title, Style = (Style)Application.Current.Resources["CardTitle"], VerticalAlignment = VerticalAlignment.Center });
            head.Children.Add(new Border
            {
                Style = (Style)Application.Current.Resources["PillBorder"],
                Background = (Brush)Application.Current.Resources[bg],
                Child = new TextBlock { Text = label, Style = (Style)Application.Current.Resources["PillText"], Foreground = (Brush)Application.Current.Resources[fg] },
            });
            Sections.Children.Add(new Border
            {
                Style = (Style)Application.Current.Resources["GlassCard"], Padding = new Thickness(14),
                Child = new StackPanel { Spacing = 6, Children = { head, new TextBlock { Text = s.Body, Style = (Style)Application.Current.Resources["BodyText"] } } },
            });
        }
        GenButton.Content = "Régénérer";
        GenButton.IsEnabled = true;
    }
}
