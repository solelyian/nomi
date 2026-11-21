using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nomi.Prototype.WinUI.Services;

namespace Nomi.Prototype.WinUI.Views;

public sealed partial class CodeView : UserControl
{
    public CodeView() => this.InitializeComponent();

    private async void OnAnalyze(object sender, RoutedEventArgs e)
    {
        AnalyzeButton.IsEnabled = false;
        Spinner.IsActive = true; Spinner.Visibility = Visibility.Visible;
        DiagList.Children.Clear();
        DiffCard.Visibility = Visibility.Collapsed;

        var review = await PrototypeServices.Inference.ReviewCodeAsync(DemoData.InvoiceSource);

        Spinner.IsActive = false; Spinner.Visibility = Visibility.Collapsed;
        foreach (var d in review.Diagnostics)
        {
            var (chipBg, chipFg) = d.Severity switch
            {
                "erreur" => ("ErrSoftBrush", "ErrBrush"),
                "avertissement" => ("WarnSoftBrush", "WarnBrush"),
                _ => ("InfoSoftBrush", "InfoBrush"),
            };
            var card = new Border { Style = (Style)Application.Current.Resources["GlassCard"], Padding = new Thickness(12) };
            var stack = new StackPanel { Spacing = 4 };
            var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            head.Children.Add(new Border
            {
                Style = (Style)Application.Current.Resources["PillBorder"],
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[chipBg],
                Child = new TextBlock { Text = d.Severity.ToUpperInvariant(), Style = (Style)Application.Current.Resources["PillText"], Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[chipFg] },
            });
            head.Children.Add(new TextBlock { Text = $"ligne {d.Line}", Style = (Style)Application.Current.Resources["CodeText"], VerticalAlignment = VerticalAlignment.Center });
            stack.Children.Add(head);
            stack.Children.Add(new TextBlock { Text = d.Message, Style = (Style)Application.Current.Resources["BodyText"] });
            stack.Children.Add(new TextBlock { Text = "→ " + d.Suggestion, Style = (Style)Application.Current.Resources["CaptionText"] });
            card.Child = stack;
            DiagList.Children.Add(card);
        }
        DiffBefore.Text = review.DiffBefore;
        DiffAfter.Text = review.DiffAfter;
        Explanation.Text = review.Explanation;
        DiffCard.Visibility = Visibility.Visible;
        AnalyzeButton.IsEnabled = true;
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        Editor.Text = Editor.Text
            .Replace("ht += l.prix * l.qte;", "ht += Math.round(l.prix * l.qte * 100) / 100;")
            .Replace("const tva = ht * 0.2;", "const tva = ht * TAUX_TVA;")
            .Replace("export function", "const TAUX_TVA = 0.2;\nexport function")
            .Replace("// l.5 : pas d'arrondi par ligne", "")
            .Replace("// l.9 : taux codé en dur", "");
        DiffCard.Visibility = Visibility.Collapsed;
    }

    private void OnDismiss(object sender, RoutedEventArgs e) => DiffCard.Visibility = Visibility.Collapsed;
}
