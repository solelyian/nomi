using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using Windows.System;

namespace Nomi;

public sealed partial class MainWindow
{
    private Border AgendaCard()
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(Eyebrow(T("agendaTitle")));
        stack.Children.Add(Text(T("agendaHelp"), 12.5, "NomiInk2"));
        var now = Now;
        if (profile.AgendaSource is { } source)
        {
            var connected = Text(string.Format(CultureInfo.CurrentCulture, T("agendaConnected"), AgendaSource.Describe(source)), 13);
            connected.FontWeight = FontWeights.SemiBold;
            stack.Children.Add(connected);
            var meetings = Meetings(now);
            var end = Agenda.FirstMeetingEnd(meetings, profile.Day);
            var meeting = meetings.FirstOrDefault(item => item.End == end);
            stack.Children.Add(Text(meeting is null ? T("agendaNoMeeting")
                : string.Format(CultureInfo.CurrentCulture, T("agendaFirst"), meeting.Title, At(meeting.End)), 12.5, "NomiInk2"));
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            var refresh = Chip(T("agendaRefresh"), "NomiChip", "\uE72C", "AgendaRefresh");
            refresh.IsEnabled = !agendaBusy;
            refresh.Click += async (_, _) => await RefreshAgenda(true);
            var remove = Chip(T("agendaRemove"), "NomiGhost", "\uE74D", "AgendaRemove");
            remove.Click += (_, _) =>
            {
                profile.AgendaSource = null;
                SecureFile.Delete(agendaCache);
                meetingsDate = default;
                todayMeetings = [];
                agendaStatus = "";
                SaveProfile();
                Refresh();
            };
            buttons.Children.Add(refresh);
            buttons.Children.Add(remove);
            stack.Children.Add(buttons);
        }
        else
        {
            var box = new TextBox { PlaceholderText = T("agendaUrlHint") };
            AutomationProperties.SetName(box, T("agendaUrl"));
            AutomationProperties.SetAutomationId(box, "AgendaUrl");
            var save = Primary(T("agendaSave"), "AgendaSave");
            save.Click += async (_, _) => await SetAgenda(box.Text);
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(box);
            Grid.SetColumn(save, 1);
            row.Children.Add(save);
            stack.Children.Add(row);
            var file = Chip(T("agendaFile"), "NomiChip", "\uE8E5", "AgendaFile");
            file.Click += async (_, _) => await PickAgendaFile();
            stack.Children.Add(file);
        }
        if (agendaBusy) stack.Children.Add(Text(T("agendaLoading"), 12.5, "NomiInk3"));
        if (agendaStatus.Length > 0) stack.Children.Add(Text(agendaStatus, 12.5, "NomiAccent"));
        stack.Children.Add(Text(T("agendaPrivacy"), 11.5, "NomiInk3"));
        var card = Glass(stack, 18);
        Mark(card, "AgendaCard", T("agendaTitle"));
        return card;
    }

    private async Task PickAgendaFile()
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".ics");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            if (await picker.PickSingleFileAsync() is { } file) await SetAgenda(file.Path);
        }
        catch (COMException)
        {
            agendaStatus = T("agendaUnreadable");
            Refresh();
        }
    }

    private async Task SetAgenda(string source)
    {
        source = source.Trim();
        if (!AgendaSource.Valid(source))
        {
            agendaStatus = T("agendaInvalid");
            Refresh();
            return;
        }
        profile.AgendaSource = source;
        agendaStatus = "";
        SecureFile.Delete(agendaCache);
        meetingsDate = default;
        todayMeetings = [];
        SaveProfile();
        await RefreshAgenda(true);
    }

    private async Task RefreshAgenda(bool manual)
    {
        if (profile.AgendaSource is not { } source || agendaBusy) return;
        if (!manual && agendaFetched is { } at && Now - at < TimeSpan.FromMinutes(30)) return;
        agendaBusy = true;
        agendaFetched = Now;
        if (manual && view == "routines") Refresh();
        try
        {
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var text = await AgendaSource.ReadAsync(source, nomiWeb, cancel.Token);
            if (text is null) agendaStatus = T("agendaUnreadable");
            else
            {
                SecureFile.Write(agendaCache, text);
                meetingsDate = default;
                agendaStatus = "";
            }
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            agendaStatus = T("agendaUnreachable");
        }
        finally
        {
            agendaBusy = false;
        }
        if (view == "routines" && !FocusInside(RoutinesBody)) Refresh();
    }

    private Version CurrentVersion => typeof(App).Assembly.GetName().Version ?? new Version(0, 0, 0);

    private async Task CheckForUpdates(bool manual)
    {
        if (!manual && (!profile.CheckUpdates || !Updates.Due(profile.UpdateChecked, Now))) return;
        updateStatus = T("updateChecking");
        if (view == "settings") Refresh();
        try
        {
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            latest = await Updates.FetchAsync(nomiWeb, cancel.Token);
            profile.UpdateChecked = Now;
            profile.LatestVersion = latest?.Version.ToString(3);
            SaveProfile();
            if (latest is { } release && Updates.Newer(release, CurrentVersion))
            {
                updateStatus = string.Format(CultureInfo.CurrentCulture, T("updateAvailable"), release.Version.ToString(3));
                if (!Busy) ShowToast(updateStatus, T("updateOpen"), () => _ = Launcher.LaunchUriAsync(release.Page));
            }
            else updateStatus = latest is null ? T("updateFailed") : T("updateCurrent");
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or IOException or InvalidDataException)
        {
            updateStatus = T("updateFailed");
        }
        if (view == "settings" && !FocusInside(SettingsBody)) Refresh();
    }

    private void BuildUpdates()
    {
        AddSetting(T("updatesSetting"), T("updatesHelp"), [T("on"), T("off")], profile.CheckUpdates ? 0 : 1,
            value => ChangeProfile(() => profile.CheckUpdates = value == 0));
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var check = Chip(T("updateCheck"), "NomiChip", "\uE895", "UpdatesCheck");
        check.Click += async (_, _) => await CheckForUpdates(true);
        var newer = latest is { } release && Updates.Newer(release, CurrentVersion);
        var open = Chip(newer ? string.Format(CultureInfo.CurrentCulture, T("updateOpenVersion"), latest!.Version.ToString(3)) : T("updates"),
            newer ? "NomiChip" : "NomiGhost", "\uE8A7", "Updates");
        open.Click += async (_, _) => await Launcher.LaunchUriAsync(newer ? latest!.Page : new Uri(ReleasesUrl));
        row.Children.Add(check);
        row.Children.Add(open);
        SettingsBody.Children.Add(row);
        var status = updateStatus.Length > 0 ? updateStatus
            : profile.UpdateChecked is { } at ? string.Format(CultureInfo.CurrentCulture, T("updateLast"), at.ToString("d MMMM, HH:mm", CultureInfo.GetCultureInfo(Current.Locale)))
            : "";
        if (status.Length > 0)
        {
            var text = Text(status, 12, newer ? "NomiAccent" : "NomiInk3");
            AutomationProperties.SetAutomationId(text, "UpdatesStatus");
            AutomationProperties.SetLiveSetting(text, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
            SettingsBody.Children.Add(text);
        }
    }

    private string RescueDetail(RescueLine line, double ratio, DateTimeOffset next) => line.Choice switch
    {
        RescueChoices.Keep => string.Format(CultureInfo.CurrentCulture, T("rescueKeepLine"), Minutes(line.Today * ratio)),
        RescueChoices.Lighten => string.Format(CultureInfo.CurrentCulture, T("rescueLightenLine"), Minutes(line.Light * ratio), At(next)),
        _ => string.Format(CultureInfo.CurrentCulture, T("rescueDeferLine"), At(next))
    };

    private Border RescueCard(DateTimeOffset now, double ratio)
    {
        var lines = rescue ?? [];
        var next = profile.Day.NextStart(now);
        var available = profile.Day.Available(now);
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(Eyebrow(T("rescueTitle")));
        stack.Children.Add(Text(T("rescueIntro"), 13, "NomiInk2"));
        var total = Text("", 12.5, "NomiInk2");
        AutomationProperties.SetAutomationId(total, "RescueSummary");
        AutomationProperties.SetLiveSetting(total, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        void Summarize()
        {
            var today = lines.Sum(line => line.Choice switch
            {
                RescueChoices.Keep => line.Today,
                RescueChoices.Lighten => line.Light,
                _ => 0
            }) * ratio;
            total.Text = string.Format(CultureInfo.CurrentCulture, T("rescueSummary"), Minutes(today), Minutes(available));
            total.Foreground = Palette(today > available ? "NomiAccent" : "NomiInk2");
        }
        for (var index = 0; index < lines.Count; index++) stack.Children.Add(RescueRow(lines[index], index, ratio, next, Summarize));
        Summarize();
        stack.Children.Add(total);
        stack.Children.Add(RescueButtons(lines));
        stack.Children.Add(Text(T("rescueNote"), 11.5, "NomiInk3"));
        var card = Glass(stack, 18, true);
        Mark(card, "RescuePlan", T("rescueTitle"));
        return card;
    }

    private Border RescueRow(RescueLine line, int index, double ratio, DateTimeOffset next, Action changed)
    {
        var choices = line.Light < line.Today ? RescueChoices.All : [RescueChoices.Keep, RescueChoices.Defer];
        var row = new Grid { ColumnSpacing = 12, Padding = new Thickness(12, 8, 12, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var texts = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var title = Text(line.Task.Title, 13);
        title.FontWeight = FontWeights.SemiBold;
        texts.Children.Add(title);
        var detail = Text(RescueDetail(line, ratio, next), 12, "NomiInk3");
        texts.Children.Add(detail);
        row.Children.Add(texts);
        var select = new ComboBox
        {
            ItemsSource = choices.Select(choice => T(choice switch { RescueChoices.Keep => "rescueKeep", RescueChoices.Lighten => "rescueLighten", _ => "rescueDefer" })).ToArray(),
            SelectedIndex = Math.Max(0, Array.IndexOf(choices, line.Choice)),
            MinWidth = 150,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(select, line.Task.Title);
        AutomationProperties.SetAutomationId(select, $"Rescue_{index}");
        select.SelectionChanged += (_, _) =>
        {
            if (select.SelectedIndex < 0) return;
            line.Choice = choices[select.SelectedIndex];
            detail.Text = RescueDetail(line, ratio, next);
            changed();
        };
        Grid.SetColumn(select, 1);
        row.Children.Add(select);
        return new Border { Background = Palette("NomiGlassSoft"), CornerRadius = new CornerRadius(12), Child = row };
    }

    private StackPanel RescueButtons(IReadOnlyList<RescueLine> lines)
    {
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var apply = Primary(T("rescueApply"), "RescueApply");
        apply.Click += (_, _) =>
        {
            var undo = DayRescue.Apply(board, lines.Where(line => board.Tasks.Contains(line.Task)), Now, profile.Day);
            rescue = null;
            Refresh();
            ShowToast(T("rescueApplied"), T("undo"), () =>
            {
                DayRescue.Undo(board, undo);
                Refresh();
                ShowToast(T("rescueUndone"), null, null);
            });
        };
        var cancel = Chip(T("rescueCancel"), "NomiGhost", null, "RescueCancel");
        cancel.Click += (_, _) => { rescue = null; Refresh(); };
        buttons.Children.Add(apply);
        buttons.Children.Add(cancel);
        return buttons;
    }
}
