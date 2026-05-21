using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AIExposureScanner.Scanner;
using AIExposureScanner.Scanner.Filesystem;
using AIExposureScanner.Scanner.Models;
using AIExposureScanner.Scanner.Reporting;
using Microsoft.Win32;

namespace AIExposureScanner.App;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var app = new Application
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose
        };
        app.Run(new MainWindow());
    }
}

public sealed class MainWindow : Window
{
    private readonly ScanOrchestrator _orchestrator = new();
    private readonly ReportBuilder _reportBuilder = new();
    private readonly ObservableCollection<FindingListItem> _visibleFindings = [];

    private ScanResult? _result;
    private Finding? _selectedFinding;

    private readonly TextBlock _statusValue = new();
    private readonly TextBlock _toolsValue = new();
    private readonly TextBlock _mcpValue = new();
    private readonly TextBlock _criticalValue = new();
    private readonly TextBlock _highValue = new();
    private readonly TextBlock _mediumValue = new();
    private readonly TextBlock _lowValue = new();
    private readonly ComboBox _severityFilter = new();
    private readonly ComboBox _appFilter = new();
    private readonly ListBox _findingList = new();
    private readonly TextBlock _detailTitle = new();
    private readonly TextBlock _detailMeta = new();
    private readonly TextBlock _detailBody = new();
    private readonly Button _openConfigButton = new();
    private readonly Button _exportMarkdownButton = new();
    private readonly Button _exportHtmlButton = new();
    private readonly Button _exportPdfButton = new();

    public MainWindow()
    {
        Title = "AI Exposure Scanner";
        Width = 1120;
        Height = 760;
        MinWidth = 980;
        MinHeight = 680;

        Content = BuildLayout();
        Loaded += (_, _) => Scan();
    }

    private UIElement BuildLayout()
    {
        var root = new DockPanel();

        var toolbar = BuildToolbar();
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);

        var summary = BuildSummary();
        DockPanel.SetDock(summary, Dock.Top);
        root.Children.Add(summary);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(390) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var sidebar = BuildSidebar();
        Grid.SetColumn(sidebar, 0);
        grid.Children.Add(sidebar);

        var detail = BuildDetailPane();
        Grid.SetColumn(detail, 1);
        grid.Children.Add(detail);

        root.Children.Add(grid);
        return root;
    }

    private UIElement BuildToolbar()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(12),
            VerticalAlignment = VerticalAlignment.Center
        };

        panel.Children.Add(Button("Scan", (_, _) => Scan()));
        _exportMarkdownButton.Content = "Markdown";
        _exportMarkdownButton.Margin = new Thickness(8, 0, 0, 0);
        _exportMarkdownButton.Click += (_, _) => Export("AIExposureScanner-Report.md", "Markdown report (*.md)|*.md", r => _reportBuilder.Markdown(r));
        panel.Children.Add(_exportMarkdownButton);

        _exportHtmlButton.Content = "HTML";
        _exportHtmlButton.Margin = new Thickness(8, 0, 0, 0);
        _exportHtmlButton.Click += (_, _) => Export("AIExposureScanner-Report.html", "HTML report (*.html)|*.html", r => _reportBuilder.Html(r));
        panel.Children.Add(_exportHtmlButton);

        _exportPdfButton.Content = "PDF";
        _exportPdfButton.Margin = new Thickness(8, 0, 0, 0);
        _exportPdfButton.Click += (_, _) => ExportBytes("AIExposureScanner-Report.pdf", "PDF report (*.pdf)|*.pdf", r => _reportBuilder.Pdf(r));
        panel.Children.Add(_exportPdfButton);

        UpdateExportButtons();
        return panel;
    }

    private UIElement BuildSummary()
    {
        var border = new Border
        {
            BorderBrush = Brushes.Gainsboro,
            BorderThickness = new Thickness(0, 1, 0, 1),
            Padding = new Thickness(12)
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(Metric("Status", _statusValue));
        panel.Children.Add(Metric("Tools", _toolsValue));
        panel.Children.Add(Metric("MCP", _mcpValue));
        panel.Children.Add(Metric("Critical", _criticalValue));
        panel.Children.Add(Metric("High", _highValue));
        panel.Children.Add(Metric("Medium", _mediumValue));
        panel.Children.Add(Metric("Low", _lowValue));

        border.Child = panel;
        return border;
    }

    private UIElement BuildSidebar()
    {
        var panel = new DockPanel { Margin = new Thickness(12) };

        var filters = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 8)
        };

        _severityFilter.ItemsSource = new[] { "All", "Critical", "High", "Medium", "Low" };
        _severityFilter.SelectedIndex = 0;
        _severityFilter.MinWidth = 130;
        _severityFilter.SelectionChanged += (_, _) => RefreshVisibleFindings();
        filters.Children.Add(_severityFilter);

        _appFilter.MinWidth = 180;
        _appFilter.Margin = new Thickness(8, 0, 0, 0);
        _appFilter.SelectionChanged += (_, _) => RefreshVisibleFindings();
        filters.Children.Add(_appFilter);

        DockPanel.SetDock(filters, Dock.Top);
        panel.Children.Add(filters);

        _findingList.ItemsSource = _visibleFindings;
        _findingList.SelectionChanged += (_, _) =>
        {
            _selectedFinding = (_findingList.SelectedItem as FindingListItem)?.Finding;
            RefreshDetail();
        };
        panel.Children.Add(_findingList);

        return panel;
    }

    private UIElement BuildDetailPane()
    {
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(28)
        };

        var panel = new StackPanel();

        _detailTitle.FontSize = 22;
        _detailTitle.FontWeight = FontWeights.SemiBold;
        _detailTitle.TextWrapping = TextWrapping.Wrap;
        panel.Children.Add(_detailTitle);

        _detailMeta.Margin = new Thickness(0, 10, 0, 18);
        _detailMeta.Foreground = Brushes.DimGray;
        _detailMeta.TextWrapping = TextWrapping.Wrap;
        panel.Children.Add(_detailMeta);

        _detailBody.TextWrapping = TextWrapping.Wrap;
        panel.Children.Add(_detailBody);

        _openConfigButton.Content = "Open config file";
        _openConfigButton.Margin = new Thickness(0, 18, 0, 0);
        _openConfigButton.Click += (_, _) => OpenSelectedConfig();
        panel.Children.Add(_openConfigButton);

        scroll.Content = panel;
        return scroll;
    }

    private async void Scan()
    {
        try
        {
            _result = await _orchestrator.ScanAsync(new LocalFilesystem());
            RefreshSummary();
            RefreshAppFilter();
            RefreshVisibleFindings();
            UpdateExportButtons();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Scan failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshSummary()
    {
        if (_result is null)
        {
            return;
        }

        var summary = ReportSummary.FromScanResult(_result);
        _statusValue.Text = summary.Status.ToString();
        _toolsValue.Text = summary.ToolsFound.ToString();
        _mcpValue.Text = summary.McpServersFound.ToString();
        _criticalValue.Text = summary.Critical.ToString();
        _highValue.Text = summary.High.ToString();
        _mediumValue.Text = summary.Medium.ToString();
        _lowValue.Text = summary.Low.ToString();
    }

    private void RefreshAppFilter()
    {
        var selected = _appFilter.SelectedItem as string ?? "All";
        var apps = new[] { "All" }
            .Concat((_result?.Findings ?? []).Select(finding => finding.App).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            .ToArray();

        _appFilter.ItemsSource = apps;
        _appFilter.SelectedItem = apps.Contains(selected, StringComparer.Ordinal) ? selected : "All";
    }

    private void RefreshVisibleFindings()
    {
        if (_result is null)
        {
            return;
        }

        var severity = _severityFilter.SelectedItem as string ?? "All";
        var app = _appFilter.SelectedItem as string ?? "All";

        var findings = _result.Findings
            .Where(finding => severity == "All" || finding.Severity.ToString() == severity)
            .Where(finding => app == "All" || finding.App == app)
            .OrderBy(finding => finding.Severity.SortRank())
            .ThenBy(finding => finding.RuleId, StringComparer.Ordinal)
            .ThenBy(finding => finding.App, StringComparer.Ordinal)
            .Select(finding => new FindingListItem(finding))
            .ToArray();

        _visibleFindings.Clear();
        foreach (var finding in findings)
        {
            _visibleFindings.Add(finding);
        }

        _findingList.SelectedIndex = _visibleFindings.Count > 0 ? 0 : -1;
        _selectedFinding = (_findingList.SelectedItem as FindingListItem)?.Finding;
        RefreshDetail();
    }

    private void RefreshDetail()
    {
        if (_selectedFinding is null)
        {
            _detailTitle.Text = "No findings";
            _detailMeta.Text = string.Empty;
            _detailBody.Text = "Run a scan or adjust the filters.";
            _openConfigButton.Visibility = Visibility.Collapsed;
            return;
        }

        var finding = _selectedFinding;
        _detailTitle.Text = finding.Title;
        _detailMeta.Text = string.Join(
            Environment.NewLine,
            new[]
            {
                $"{finding.Severity} · {finding.RuleId}",
                $"App: {finding.App}",
                finding.ServerName is null ? null : $"Server: {finding.ServerName}",
                finding.ExtensionId is null ? null : $"Extension: {finding.ExtensionId}",
                finding.AffectedPath is null ? null : $"Path: {finding.AffectedPath}",
                finding.MaskedValue is null ? null : $"Masked value: {finding.MaskedValue}"
            }.Where(line => line is not null)
        );
        _detailBody.Text = $"Why this matters{Environment.NewLine}{finding.Explanation}{Environment.NewLine}{Environment.NewLine}Recommended fix{Environment.NewLine}{finding.Recommendation}";
        _openConfigButton.Visibility = finding.AffectedPath is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OpenSelectedConfig()
    {
        if (_selectedFinding?.AffectedPath is null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo(_selectedFinding.AffectedPath)
        {
            UseShellExecute = true
        });
    }

    private void Export(string fileName, string filter, Func<ScanResult, string> render)
    {
        if (_result is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            FileName = fileName,
            Filter = filter,
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, render(_result));
    }

    private void ExportBytes(string fileName, string filter, Func<ScanResult, byte[]> render)
    {
        if (_result is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            FileName = fileName,
            Filter = filter,
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        File.WriteAllBytes(dialog.FileName, render(_result));
    }

    private void UpdateExportButtons()
    {
        var hasResult = _result is not null;
        _exportMarkdownButton.IsEnabled = hasResult;
        _exportHtmlButton.IsEnabled = hasResult;
        _exportPdfButton.IsEnabled = hasResult;
    }

    private static Button Button(string content, RoutedEventHandler onClick)
    {
        var button = new Button
        {
            Content = content,
            Padding = new Thickness(12, 5, 12, 5)
        };
        button.Click += onClick;
        return button;
    }

    private static UIElement Metric(string title, TextBlock value)
    {
        var panel = new StackPanel
        {
            Width = 116,
            Margin = new Thickness(0, 0, 10, 0)
        };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = Brushes.DimGray,
            FontSize = 12
        });
        value.Text = "-";
        value.FontSize = 18;
        value.FontWeight = FontWeights.SemiBold;
        panel.Children.Add(value);
        return panel;
    }
}

internal sealed record FindingListItem(Finding Finding)
{
    public override string ToString() =>
        $"{Finding.Severity}  {Finding.RuleId}{Environment.NewLine}{Finding.Title}{Environment.NewLine}{Finding.App}";
}
