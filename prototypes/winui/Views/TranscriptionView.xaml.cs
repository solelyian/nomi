using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nomi.Prototype.WinUI.Services;

namespace Nomi.Prototype.WinUI.Views;

public sealed partial class TranscriptionView : UserControl
{
    private bool _running;

    public TranscriptionView() => this.InitializeComponent();

    private async void OnStart(object sender, RoutedEventArgs e)
    {
        if (_running) return;
        _running = true;
        StartButton.IsEnabled = false;
        Transcript.Children.Clear();
        LiveNotes.Children.Clear();

        await foreach (var chunk in PrototypeServices.Speech.StreamTranscriptAsync())
        {
            Timer.Text = $"{chunk.EndSec / 60:D2}:{chunk.EndSec % 60:D2}";
            Transcript.Children.Add(MakeRow(chunk));
            await foreach (var note in PrototypeServices.Inference.StreamLiveNotesAsync(chunk))
            {
                LiveNotes.Children.Add(new Border
                {
                    Style = (Style)Application.Current.Resources["GlassCard"], Padding = new Thickness(10),
                    Child = new TextBlock { Text = note, Style = (Style)Application.Current.Resources["BodyText"], FontSize = 12.5 },
                });
            }
        }
        StartButton.Content = "Transcription terminée";
    }

    private Border MakeRow(TranscriptChunk c)
    {
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var speakerBrush = (Brush)Application.Current.Resources[
            c.Speaker == "Prof" ? "AccentSoftBrush" : "InfoSoftBrush"];
        var speakerFg = (Brush)Application.Current.Resources[
            c.Speaker == "Prof" ? "AccentInkBrush" : "InfoBrush"];
        head.Children.Add(new Border
        {
            Style = (Style)Application.Current.Resources["PillBorder"], Background = speakerBrush,
            Child = new TextBlock { Text = c.Speaker, Style = (Style)Application.Current.Resources["PillText"], Foreground = speakerFg },
        });
        head.Children.Add(new TextBlock
        {
            Text = $"{c.StartSec / 60:D2}:{c.StartSec % 60:D2}",
            Style = (Style)Application.Current.Resources["CodeText"],
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (c.Uncertain)
            head.Children.Add(new Border
            {
                Style = (Style)Application.Current.Resources["PillBorder"],
                Background = (Brush)Application.Current.Resources["WarnSoftBrush"],
                Child = new TextBlock { Text = "incertain", Style = (Style)Application.Current.Resources["PillText"], Foreground = (Brush)Application.Current.Resources["WarnBrush"] },
            });

        var body = new TextBlock { Text = c.Text, Style = (Style)Application.Current.Resources["BodyText"], Margin = new Thickness(0, 4, 0, 0) };
        return new Border
        {
            Padding = new Thickness(10, 8, 10, 8),
            BorderBrush = (Brush)Application.Current.Resources["StrokeBrush"],
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new StackPanel { Children = { head, body } },
        };
    }
}
