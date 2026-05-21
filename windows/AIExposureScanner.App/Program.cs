using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AIExposureScanner.Scanner;
using AIExposureScanner.Scanner.Filesystem;
using AIExposureScanner.Scanner.Models;
using AIExposureScanner.Scanner.Reporting;
using AIExposureScanner.Scanner.RulePacks;
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

// MARK: - Rule Pack Store

public sealed class RulePackEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Yaml { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public bool IsValid { get; set; } = true;
    public string? ValidationError { get; set; }
}

public sealed class WinRulePackStore
{
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AIExposureScanner", "rule-packs.json");

    public ObservableCollection<RulePackEntry> Entries { get; } = [];

    public WinRulePackStore() { Load(); }

    public void Add(string yaml)
    {
        var result = RulePackLoader.Load(yaml);
        var entry = result switch
        {
            RulePackLoadResult.Valid v => new RulePackEntry { Name = v.Pack.Name, Yaml = yaml },
            RulePackLoadResult.Invalid e => new RulePackEntry { Name = "Invalid pack", Yaml = yaml, IsEnabled = false, IsValid = false, ValidationError = e.Error },
            _ => throw new UnreachableException()
        };
        Entries.Add(entry);
        Save();
    }

    public void Remove(RulePackEntry entry) { Entries.Remove(entry); Save(); }

    public IReadOnlyList<RulePack> ActivePacks =>
        Entries
            .Where(e => e.IsEnabled && e.IsValid)
            .Select(e => RulePackLoader.Load(e.Yaml))
            .OfType<RulePackLoadResult.Valid>()
            .Select(v => v.Pack)
            .ToList();

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            File.WriteAllText(StorePath, JsonSerializer.Serialize(Entries.ToList()));
        }
        catch { /* best effort */ }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(StorePath)) return;
            var list = JsonSerializer.Deserialize<List<RulePackEntry>>(File.ReadAllText(StorePath));
            if (list is null) return;
            foreach (var e in list) Entries.Add(e);
        }
        catch { /* best effort */ }
    }
}

// MARK: - Rule Packs Window

public sealed class RulePacksWindow : Window
{
    private readonly WinRulePackStore _store;
    private readonly ListBox _listBox = new() { Margin = new Thickness(0, 0, 0, 8) };

    public RulePacksWindow(WinRulePackStore store)
    {
        _store = store;
        Title = "Rule Packs";
        Width = 540;
        Height = 480;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _listBox.ItemsSource = store.Entries;
        _listBox.ItemTemplate = BuildEntryTemplate();

        var addButton = new Button { Content = "+ Add Rule Pack", Padding = new Thickness(12, 5, 12, 5) };
        addButton.Click += (_, _) => ShowAddDialog();

        var closeButton = new Button { Content = "Close", Padding = new Thickness(12, 5, 12, 5) };
        closeButton.Click += (_, _) => Close();

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttonRow.Children.Add(addButton);
        buttonRow.Children.Add(new UIElement[] { }.Length == 0 ? new Separator { Width = 8, Visibility = Visibility.Hidden } : new Separator());
        buttonRow.Children.Add(closeButton);

        var root = new DockPanel { Margin = new Thickness(16) };
        DockPanel.SetDock(buttonRow, Dock.Bottom);
        root.Children.Add(buttonRow);
        root.Children.Add(_listBox);
        Content = root;
    }

    private DataTemplate BuildEntryTemplate()
    {
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetValue(Border.PaddingProperty, new Thickness(8, 6, 8, 6));
        factory.SetValue(Border.MarginProperty, new Thickness(0, 0, 0, 4));
        factory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(247, 247, 247)));
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

        var row = new FrameworkElementFactory(typeof(DockPanel));

        var deleteBtn = new FrameworkElementFactory(typeof(Button));
        deleteBtn.SetValue(Button.ContentProperty, "✕");
        deleteBtn.SetValue(Button.PaddingProperty, new Thickness(6, 2, 6, 2));
        deleteBtn.SetValue(DockPanel.DockProperty, Dock.Right);
        deleteBtn.AddHandler(Button.ClickEvent, new RoutedEventHandler((s, _) =>
        {
            if (s is FrameworkElement fe && fe.DataContext is RulePackEntry entry)
                _store.Remove(entry);
        }));

        var nameBlock = new FrameworkElementFactory(typeof(TextBlock));
        nameBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));
        nameBlock.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

        row.AppendChild(deleteBtn);
        row.AppendChild(nameBlock);
        factory.AppendChild(row);

        return new DataTemplate { VisualTree = factory };
    }

    private void ShowAddDialog()
    {
        var dialog = new Window
        {
            Title = "Add Rule Pack",
            Width = 540,
            Height = 400,
            ResizeMode = ResizeMode.NoResize,
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var editor = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var hint = new TextBlock
        {
            Text = "Paste YAML rule pack content:",
            Margin = new Thickness(0, 0, 0, 4)
        };
        var addBtn = new Button { Content = "Add", Padding = new Thickness(12, 5, 12, 5), IsDefault = true };
        var cancelBtn = new Button { Content = "Cancel", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(8, 0, 0, 0), IsCancel = true };

        addBtn.Click += (_, _) =>
        {
            var yaml = editor.Text.Trim();
            if (string.IsNullOrWhiteSpace(yaml)) return;
            _store.Add(yaml);
            dialog.Close();
        };
        cancelBtn.Click += (_, _) => dialog.Close();

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        btnRow.Children.Add(addBtn);
        btnRow.Children.Add(cancelBtn);

        var stack = new DockPanel { Margin = new Thickness(16) };
        DockPanel.SetDock(btnRow, Dock.Bottom);
        DockPanel.SetDock(hint, Dock.Top);
        stack.Children.Add(btnRow);
        stack.Children.Add(hint);
        stack.Children.Add(editor);
        dialog.Content = stack;
        dialog.ShowDialog();
    }
}

public sealed class MainWindow : Window
{
    private readonly ScanOrchestrator _orchestrator = new();
    private readonly ReportBuilder _reportBuilder = new();
    private readonly WinRulePackStore _rulePackStore = new();
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
    private readonly Button _exportJsonButton = new();

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

        _exportJsonButton.Content = "JSON";
        _exportJsonButton.Margin = new Thickness(8, 0, 0, 0);
        _exportJsonButton.Click += (_, _) => Export("AIExposureScanner-Report.json", "JSON report (*.json)|*.json", r => _reportBuilder.Json(r));
        panel.Children.Add(_exportJsonButton);

        var rulePacksButton = new Button
        {
            Content = "Rule Packs",
            Margin = new Thickness(16, 0, 0, 0),
            Padding = new Thickness(12, 5, 12, 5)
        };
        rulePacksButton.Click += (_, _) =>
        {
            var win = new RulePacksWindow(_rulePackStore) { Owner = this };
            win.ShowDialog();
        };
        panel.Children.Add(rulePacksButton);

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
            _result = await _orchestrator.ScanAsync(new LocalFilesystem(), packs: _rulePackStore.ActivePacks);
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
        _exportJsonButton.IsEnabled = hasResult;
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
