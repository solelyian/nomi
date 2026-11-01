using System.Runtime.InteropServices;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace Nomi;

public sealed partial class MainWindow
{
    private HardwareReport? hardware;
    private bool hardwareProbing;
    private StackPanel? onboardingHardware;

    private Border HardwareCard()
    {
        onboardingHardware = new StackPanel { Spacing = 10 };
        AutomationProperties.SetAutomationId(onboardingHardware, "OnboardingHardware");
        AutomationProperties.SetName(onboardingHardware, T("oobeHwTitle"));
        FillHardware();
        if (hardware is null) ProbeHardware();
        return Glass(onboardingHardware, 22);
    }

    private async void ProbeHardware()
    {
        if (hardwareProbing) return;
        hardwareProbing = true;
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nyne", "Nomi");
        try
        {
            hardware = await Task.Run(() => HardwareProbe.Detect(directory));
        }
        catch (Exception error) when (error is ExternalException or DllNotFoundException or EntryPointNotFoundException)
        {
            hardware = new("", Environment.ProcessorCount, "", 0, 0, [], false);
        }
        finally
        {
            hardwareProbing = false;
        }
        FillHardware();
    }

    private long HardwareModelBytes()
    {
        if (inference.HasModel) return 0;
        if (inference.DownloadBytes > 0) return inference.DownloadBytes;
        try { return ModelDefinition.Load().Bytes; }
        catch (Exception error) when (error is IOException or System.Text.Json.JsonException or UnauthorizedAccessException) { return 0; }
    }

    private void FillHardware()
    {
        if (onboardingHardware is not { } panel) return;
        panel.Children.Clear();
        var title = Text(T("oobeHwTitle"), 15);
        title.FontWeight = FontWeights.SemiBold;
        panel.Children.Add(title);
        if (hardware is not { } report)
        {
            var waiting = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            waiting.Children.Add(new ProgressRing { IsActive = true, Width = 16, Height = 16 });
            var checking = Text(T("oobeHwChecking"), 13.5, "NomiInk2");
            AutomationProperties.SetAutomationId(checking, "OnboardingHardwareChecking");
            waiting.Children.Add(checking);
            panel.Children.Add(waiting);
            return;
        }

        var unknown = T("oobeHwUnknown");
        var processor = report.Processor.Length > 0 ? report.Processor : unknown;
        if (report.Cores > 0) processor = $"{processor} · {string.Format(T("oobeHwCores"), report.Cores)}";
        var grid = new Grid { ColumnSpacing = 18, RowSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var rows = new (string Id, string Label, string Value)[]
        {
            ("processor", T("oobeHwProcessor"), processor),
            ("memory", T("oobeHwMemory"), report.Memory > 0 ? string.Format(T("oobeHwGb"), Hardware.Gigabytes(report.Memory)) : unknown),
            ("disk", T("oobeHwFree"), report.FreeDisk > 0 ? string.Format(T("oobeHwGb"), Hardware.Gigabytes(report.FreeDisk)) : unknown),
            ("npu", T("oobeHwNpuLabel"), report.Npus.Count > 0 ? string.Join(", ", report.Npus) : T("oobeHwNone"))
        };
        for (var i = 0; i < rows.Length; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = Text(rows[i].Label, 13, "NomiInk3");
            var value = Text(rows[i].Value, 13.5);
            value.TextWrapping = TextWrapping.Wrap;
            AutomationProperties.SetAutomationId(value, $"OnboardingHardware_{rows[i].Id}");
            AutomationProperties.SetName(value, $"{rows[i].Label} : {rows[i].Value}");
            Grid.SetRow(label, i);
            Grid.SetRow(value, i);
            Grid.SetColumn(value, 1);
            grid.Children.Add(label);
            grid.Children.Add(value);
        }
        panel.Children.Add(grid);

        var bytes = HardwareModelBytes();
        var verdict = Hardware.Verdict(report, bytes);
        var message = verdict switch
        {
            "disk" => string.Format(T("oobeHwVerdictDisk"), Hardware.Gigabytes(Hardware.DiskShortfall(report, bytes))),
            "npu" => string.Format(T("oobeHwVerdictNpu"), report.Npus[0]),
            "npu-windows" => T("oobeHwVerdictNpuWindows"),
            "cpu-memory" => T("oobeHwVerdictMemory"),
            _ => T("oobeHwVerdictCpu")
        };
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        line.Children.Add(new FontIcon
        {
            Glyph = verdict is "npu" or "cpu" ? "\uE73E" : "\uE7BA",
            FontSize = 14,
            Foreground = Palette(verdict is "npu" or "cpu" ? "NomiOk" : "NomiAccent")
        });
        var text = Text(message, 13.5);
        text.TextWrapping = TextWrapping.Wrap;
        text.MaxWidth = 460;
        AutomationProperties.SetAutomationId(text, "OnboardingHardwareVerdict");
        AutomationProperties.SetLiveSetting(text, AutomationLiveSetting.Polite);
        line.Children.Add(text);
        panel.Children.Add(line);
    }
}
