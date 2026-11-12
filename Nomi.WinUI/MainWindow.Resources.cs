using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Nomi;

// The reasoning model and the model that reads images are offered as one download, in the welcome and in the Nomi page.
public sealed partial class MainWindow
{
    private sealed class ResourcesView(string prefix)
    {
        public string Prefix { get; } = prefix;
        public Border? Host;
        public ProgressBar? Bar;
        public TextBlock? Status;
        public Button? First;
        public string Key = "";
    }

    private readonly ResourcesView welcomeResources = new("Onboarding");
    private readonly ResourcesView nomiResources = new("Nomi");
    private Task? resourcesRun;
    private bool resourcesActive;
    private bool resourcesStopped;
    private bool resourcesLater;
    private bool nomiNoModel;

    private ModelDefinition? reasoningDefinition;

    // The reasoning model offered for download is always the one pinned in local-model.json (Qwen3 8B).
    private ModelDefinition? ReasoningDefinition()
    {
        if (reasoningDefinition is not null) return reasoningDefinition;
        try { return reasoningDefinition = ModelDefinition.Load(); }
        catch (Exception error) when (error is IOException or System.Text.Json.JsonException or UnauthorizedAccessException) { return null; }
    }

    // A model that is already on the computer and only loads in memory is not shown as a download in progress.
    private LocalResource[] ResourceItems() =>
    [
        new("reasoning", ReasoningDefinition()?.Name ?? inference.Model, ReasoningDefinition()?.Bytes ?? inference.DownloadBytes, inference.HasModel,
            modelPreparation is not null && (resourcesActive || !inference.HasModel), modelProgress.Stage, modelProgress.Fraction),
        new("vision", vision.Model, vision.Bytes, vision.HasModel, visionPreparation is not null, visionProgress.Stage, visionProgress.Fraction),
        new("speech", speech.Model, speech.Bytes, speech.HasModel, speechPreparation is not null, speechProgress.Stage, speechProgress.Fraction)
    ];

    private string ResourcesState(LocalResource[] items) => resourcesActive ? LocalResources.Busy : LocalResources.State(items);

    private string ResourcesKey(LocalResource[] items) =>
        $"{ResourcesState(items)}|{string.Join('|', items.Select(item => $"{item.Present}:{item.Busy}"))}|{inference.Ready}";

    private string Gigabytes(long bytes) =>
        (bytes / 1_000_000_000d).ToString("0.0", CultureInfo.GetCultureInfo(language == "fr" ? "fr-FR" : "en-US"));

    private string ResourcesStatus(LocalResource[] items)
    {
        var state = ResourcesState(items);
        if (state == LocalResources.Busy)
        {
            var active = LocalResources.Active(items);
            if (active is null) return T("engineBusy");
            return active.Stage == LocalResources.Downloading
                ? string.Format(T("resDownloading"), active.Name, Readable.Fraction(LocalResources.Fraction(items), language))
                : Label(active.Stage);
        }
        if (state == LocalResources.Ready) return T(inference.Ready ? "resReady" : "resPresent");
        if (state == LocalResources.Paused) return T("model-paused");
        if (state == LocalResources.Missing) return string.Format(T("resMissing"), Gigabytes(LocalResources.MissingBytes(items)));
        return Label(state);
    }

    private Task PrepareResources()
    {
        if (resourcesRun is { IsCompleted: false }) return resourcesRun;
        return resourcesRun = RunResources();
    }

    private async Task RunResources()
    {
        resourcesActive = true;
        resourcesStopped = false;
        resourcesLater = false;
        ResourcesChanged();
        try
        {
            if (!inference.Ready) await PrepareModel();
            if (!resourcesStopped && inference.HasModel && !vision.HasModel && visionPreparation is null)
                await PrepareVision(true, load: false);
            if (!resourcesStopped && inference.HasModel && vision.HasModel && !speech.HasModel && speechPreparation is null)
                await PrepareSpeech(true, load: false);
        }
        finally
        {
            resourcesActive = false;
            ResourcesChanged();
        }
    }

    private void PauseResources()
    {
        resourcesStopped = true;
        modelPreparation?.Cancel();
        visionPreparation?.Cancel();
        speechPreparation?.Cancel();
    }

    private void ResourcesChanged()
    {
        UpdateOnboardingModel();
        if (view != "nomi") return;
        if (nomiNoModel != (!inference.HasModel && !inference.Ready))
        {
            BuildNomi();
            return;
        }
        if (nomiResources.Host is null) return;
        var items = ResourceItems();
        if (ResourcesKey(items) == nomiResources.Key)
        {
            RefreshResources(nomiResources, items);
            return;
        }
        var focused = FocusManager.GetFocusedElement(Root.XamlRoot) is FrameworkElement element
            && AutomationProperties.GetAutomationId(element) is "NomiDownload" or "NomiModelPause" or "NomiModelLater";
        nomiResources.Host.Child = nomiNoModel ? Glass(ResourcesCard(nomiResources, null), 22, true) : ResourcesBanner();
        nomiResources.Host.Visibility = nomiResources.Host.Child is null ? Visibility.Collapsed : Visibility.Visible;
        if (focused) nomiResources.First?.Focus(FocusState.Programmatic);
    }

    private void RefreshResources(ResourcesView target, LocalResource[] items)
    {
        if (target.Bar is not null)
        {
            target.Bar.IsIndeterminate = LocalResources.Active(items)?.Stage != LocalResources.Downloading;
            target.Bar.Value = LocalResources.Fraction(items) * 100;
        }
        var status = ResourcesStatus(items);
        if (target.Status is not null && target.Status.Text != status) target.Status.Text = status;
    }

    private TextBlock ResourcesStatusLine(ResourcesView target, LocalResource[] items, double size)
    {
        var status = Text(ResourcesStatus(items), size, "NomiInk2");
        AutomationProperties.SetAutomationId(status, $"{target.Prefix}ModelStatus");
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        target.Status = status;
        return status;
    }

    private ProgressBar ResourcesBar(ResourcesView target, LocalResource[] items)
    {
        var bar = new ProgressBar { Minimum = 0, Maximum = 100, Height = 6, CornerRadius = new CornerRadius(3) };
        AutomationProperties.SetName(bar, T("oobe-modelTitle"));
        target.Bar = bar;
        RefreshResources(target, items);
        return bar;
    }

    private Button ResourcesPause(ResourcesView target)
    {
        var pause = Chip(T("oobeModelPause"), "NomiChip", "\uE769", $"{target.Prefix}ModelPause");
        pause.Click += (_, _) => PauseResources();
        target.First = pause;
        return pause;
    }

    private string ResourcesAction(LocalResource[] items)
    {
        if (items.All(item => item.Present)) return T("loadLocalModel");
        var resume = items.Any(item => !item.Present && item.Stage is LocalResources.Paused or "model-download-error");
        return resume ? T("oobeModelResume") : T("oobeDownloadNow");
    }

    // Both models with their size and state, one status line and one button for the whole download.
    private StackPanel ResourcesCard(ResourcesView target, Action? later)
    {
        var items = ResourceItems();
        var state = ResourcesState(items);
        target.Key = ResourcesKey(items);
        target.Bar = null;
        target.First = null;
        var card = new StackPanel { Spacing = 14 };
        var rows = new StackPanel { Spacing = 12 };
        foreach (var item in items)
        {
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new FontIcon
            {
                Glyph = item.Present ? "\uE73E" : "\uE896",
                FontSize = 14,
                Foreground = Palette(item.Present ? "NomiOk" : "NomiAccent"),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 4, 0, 0)
            });
            var names = new StackPanel { Spacing = 2 };
            var name = Text(item.Name, 14.5);
            name.FontWeight = FontWeights.SemiBold;
            names.Children.Add(name);
            names.Children.Add(Text(T(item.Id switch { "vision" => "resVisionRole", "speech" => "resSpeechRole", _ => "resReasoningRole" }), 12.5, "NomiInk3"));
            Grid.SetColumn(names, 1);
            row.Children.Add(names);
            var where = item.Present ? T("resOnComputer")
                : item.Busy ? T("resGetting")
                : string.Format(T("resToGet"), Gigabytes(item.Bytes));
            var side = Text(where, 12.5, "NomiInk2");
            side.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(side, 2);
            row.Children.Add(side);
            AutomationProperties.SetAutomationId(side, $"{target.Prefix}Resource_{item.Id}");
            AutomationProperties.SetName(side, $"{item.Name} : {where}");
            rows.Children.Add(row);
        }
        card.Children.Add(rows);
        card.Children.Add(ResourcesStatusLine(target, items, 13.5));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        if (state == LocalResources.Busy)
        {
            card.Children.Add(ResourcesBar(target, items));
            actions.Children.Add(ResourcesPause(target));
            card.Children.Add(actions);
            card.Children.Add(Text(T(later is null ? "nomiDownloading" : "oobeModelBackground"), 12.5, "NomiInk3"));
            return card;
        }
        if (state == LocalResources.Ready && (inference.Ready || modelPreparation is not null)) return card;
        var download = Primary(ResourcesAction(items), $"{target.Prefix}Download");
        download.Padding = new Thickness(22, 12, 22, 12);
        download.FontSize = 15;
        download.Click += (_, _) => _ = PrepareResources();
        target.First = download;
        actions.Children.Add(download);
        if (later is not null)
        {
            var skip = Chip(T("routineLater"), "NomiGhost", null, $"{target.Prefix}ModelLater");
            skip.Click += (_, _) => later();
            actions.Children.Add(skip);
        }
        card.Children.Add(actions);
        return card;
    }

    // In the Nomi page, when the reasoning model is there but the model that reads images is not.
    private UIElement? ResourcesBanner()
    {
        var items = ResourceItems();
        nomiResources.Key = ResourcesKey(items);
        nomiResources.Bar = null;
        nomiResources.Status = null;
        nomiResources.First = null;
        if (resourcesLater || vision.HasModel && !resourcesActive) return null;
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(new FontIcon { Glyph = "\uE896", FontSize = 14, Foreground = Palette("NomiAccent"), VerticalAlignment = VerticalAlignment.Center });
        var text = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        if (resourcesActive)
        {
            text.Children.Add(ResourcesStatusLine(nomiResources, items, 13));
            text.Children.Add(ResourcesBar(nomiResources, items));
            actions.Children.Add(ResourcesPause(nomiResources));
        }
        else
        {
            var missing = items.First(item => item.Id == "vision");
            var line = Text(string.Format(T("resBanner"), missing.Name, Gigabytes(vision.MissingBytes)), 13, "NomiInk2");
            AutomationProperties.SetAutomationId(line, "NomiModelStatus");
            text.Children.Add(line);
            if (LocalResources.Error(items) is { } error) text.Children.Add(Text(Label(error), 12.5, "NomiAccent"));
            var download = Chip(ResourcesAction(items), "NomiPrimary", null, "NomiDownload");
            download.Click += (_, _) => _ = PrepareResources();
            nomiResources.First = download;
            actions.Children.Add(download);
            var skip = Chip(T("routineLater"), "NomiGhost", null, "NomiModelLater");
            skip.Click += (_, _) =>
            {
                resourcesLater = true;
                ResourcesChanged();
            };
            actions.Children.Add(skip);
        }
        Grid.SetColumn(text, 1);
        Grid.SetColumn(actions, 2);
        row.Children.Add(text);
        row.Children.Add(actions);
        var banner = new Border
        {
            Child = row,
            Background = Palette("NomiSurface2"),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(16, 10, 12, 10),
            Margin = new Thickness(14, 4, 14, 6)
        };
        AutomationProperties.SetAutomationId(banner, "NomiResources");
        AutomationProperties.SetName(banner, T("oobe-modelTitle"));
        return banner;
    }
}
