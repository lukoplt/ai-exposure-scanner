using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using AIExposureScanner.Scanner;
using AIExposureScanner.Scanner.Filesystem;
using AIExposureScanner.Scanner.Models;
using AIExposureScanner.Scanner.Reporting;
using AIExposureScanner.Scanner.RulePacks;
using AIExposureScanner.Scanner.Rules;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Markup;

// Alias the WPF-UI versions of common controls so we get Fluent styling
// by default without having to qualify every type.
using Button = Wpf.Ui.Controls.Button;
using TextBox = Wpf.Ui.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;
using ComboBox = System.Windows.Controls.ComboBox;
using ListBox = System.Windows.Controls.ListBox;
using ListView = System.Windows.Controls.ListView;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace AIExposureScanner.App;

internal static class AppIcons
{
    /// Cached lazy-loaded app icon used for every window's title bar +
    /// taskbar entry. Loaded from the pack URI which resolves to the
    /// Resource-marked AppIcon.ico inside the compiled assembly.
    public static System.Windows.Media.ImageSource AppIcon
    {
        get
        {
            _appIcon ??= System.Windows.Media.Imaging.BitmapFrame.Create(
                new Uri("pack://application:,,,/AppIcon.ico", UriKind.Absolute));
            return _appIcon;
        }
    }
    private static System.Windows.Media.ImageSource? _appIcon;
}

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var app = new Application
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose
        };

        // Inject the WPF-UI theme + control resource dictionaries so that
        // every Wpf.Ui.Controls.* element resolves its Fluent styles.
        // Light theme by default — feels less heavy for a desktop audit
        // tool that is primarily read text and badges.
        app.Resources.MergedDictionaries.Add(new ThemesDictionary
        {
            Theme = ApplicationTheme.Light
        });
        app.Resources.MergedDictionaries.Add(new ControlsDictionary());
        ApplicationThemeManager.Apply(ApplicationTheme.Light);

        app.Run(new MainWindow());
    }
}

// MARK: - Rule Pack Store ---------------------------------------------------

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

    /// Returns an error message if the rule pack was rejected (e.g. contains
    /// a recognized secret pattern); null on success.
    public string? Add(string yaml)
    {
        if (SecretPatterns.ContainsSecret(yaml))
        {
            return "Pack contains an API key or token. Remove secrets before saving — rule pack is stored in plaintext.";
        }

        var result = RulePackLoader.Load(yaml);
        var entry = result switch
        {
            RulePackLoadResult.Valid v => new RulePackEntry { Name = v.Pack.Name, Yaml = yaml },
            RulePackLoadResult.Invalid e => new RulePackEntry { Name = "Invalid pack", Yaml = yaml, IsEnabled = false, IsValid = false, ValidationError = e.Error },
            _ => throw new UnreachableException()
        };
        Entries.Add(entry);
        Save();
        return null;
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

// MARK: - Visual helpers ----------------------------------------------------

internal static class SeverityVisuals
{
    public static Brush Brush(Severity severity) => severity switch
    {
        Severity.Critical => new SolidColorBrush(Color.FromRgb(0xE0, 0x1E, 0x5A)),
        Severity.High     => new SolidColorBrush(Color.FromRgb(0xE8, 0x6A, 0x17)),
        Severity.Medium   => new SolidColorBrush(Color.FromRgb(0xCE, 0xA2, 0x07)),
        Severity.Low      => new SolidColorBrush(Color.FromRgb(0x2C, 0x9E, 0xD9)),
        _                 => new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x70))
    };

    public static Border Pill(Severity severity)
    {
        var label = new TextBlock
        {
            Text = severity.ToString().ToUpperInvariant(),
            Foreground = Brushes.White,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        return new Border
        {
            Background = Brush(severity),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 2, 8, 2),
            Child = label,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
    }
}

// MARK: - Rule Packs window -------------------------------------------------
//
// Plain System.Windows.Window — NOT FluentWindow — so the Mica backdrop and
// the custom Wpf.Ui.Controls.TitleBar do not destabilise the dialog opening
// path. Previous FluentWindow + Mica + custom-TitleBar combo crashed on
// click. Items are built imperatively per entry; no FrameworkElementFactory.

public sealed class RulePacksWindow : Window
{
    private readonly WinRulePackStore _store;
    private readonly StackPanel _list = new();

    public RulePacksWindow(WinRulePackStore store)
    {
        _store = store;
        Title = "Rule Packs";
        Width = 580;
        Height = 520;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)Application.Current.Resources["ApplicationBackgroundBrush"];
        Icon = AppIcons.AppIcon;

        var addButton = new Button
        {
            Content = "Add rule pack",
            Icon = new SymbolIcon(SymbolRegular.Add24),
            Appearance = ControlAppearance.Primary
        };
        addButton.Click += (_, _) => ShowAddDialog();

        var closeButton = new Button { Content = "Close" };
        closeButton.Click += (_, _) => Close();

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        buttonRow.Children.Add(addButton);
        buttonRow.Children.Add(new Border { Width = 8 });
        buttonRow.Children.Add(closeButton);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _list
        };

        var root = new DockPanel { Margin = new Thickness(20) };
        DockPanel.SetDock(buttonRow, Dock.Bottom);
        root.Children.Add(buttonRow);
        root.Children.Add(scroll);
        Content = root;

        store.Entries.CollectionChanged += (_, _) => RefreshList();
        RefreshList();
    }

    private void RefreshList()
    {
        _list.Children.Clear();
        if (_store.Entries.Count == 0)
        {
            _list.Children.Add(new TextBlock
            {
                Text = "No rule packs configured.",
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                Margin = new Thickness(0, 16, 0, 0)
            });
            return;
        }
        foreach (var entry in _store.Entries)
        {
            _list.Children.Add(BuildEntryCard(entry));
        }
    }

    private Border BuildEntryCard(RulePackEntry entry)
    {
        var name = new TextBlock
        {
            Text = entry.Name,
            FontWeight = FontWeights.SemiBold
        };
        var stack = new StackPanel();
        stack.Children.Add(name);
        if (!string.IsNullOrEmpty(entry.ValidationError))
        {
            stack.Children.Add(new TextBlock
            {
                Text = entry.ValidationError,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0x1E, 0x5A)),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
        }

        var deleteBtn = new Button
        {
            Icon = new SymbolIcon(SymbolRegular.Delete24),
            Appearance = ControlAppearance.Transparent
        };
        deleteBtn.Click += (_, _) => _store.Remove(entry);

        var dock = new DockPanel();
        DockPanel.SetDock(deleteBtn, Dock.Right);
        dock.Children.Add(deleteBtn);
        dock.Children.Add(stack);

        return new Border
        {
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8),
            CornerRadius = new CornerRadius(6),
            Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
            Child = dock
        };
    }

    private void ShowAddDialog()
    {
        var dialog = new Window
        {
            Title = "Add Rule Pack",
            Width = 600,
            Height = 480,
            ResizeMode = ResizeMode.NoResize,
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = (Brush)Application.Current.Resources["ApplicationBackgroundBrush"],
            Icon = AppIcons.AppIcon
        };

        var editor = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            PlaceholderText = "version: \"1.0\"\nname: \"My Rules\"\nrules:\n  - id: ACME-001\n    ...",
            MinHeight = 240
        };

        var hint = new TextBlock
        {
            Text = "Paste YAML rule pack content. The dialog refuses YAML containing API key patterns.",
            Margin = new Thickness(0, 0, 0, 8),
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        };

        var errorBar = new InfoBar
        {
            Severity = InfoBarSeverity.Error,
            IsClosable = false,
            IsOpen = false,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var addBtn = new Button { Content = "Add", Appearance = ControlAppearance.Primary, IsDefault = true };
        var cancelBtn = new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(8, 0, 0, 0) };

        addBtn.Click += (_, _) =>
        {
            var yaml = editor.Text.Trim();
            if (string.IsNullOrWhiteSpace(yaml)) return;
            var error = _store.Add(yaml);
            if (error is not null)
            {
                errorBar.Message = error;
                errorBar.IsOpen = true;
                return;
            }
            dialog.Close();
        };
        cancelBtn.Click += (_, _) => dialog.Close();

        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        btnRow.Children.Add(addBtn);
        btnRow.Children.Add(cancelBtn);

        var stack = new DockPanel { Margin = new Thickness(20) };
        DockPanel.SetDock(btnRow, Dock.Bottom);
        DockPanel.SetDock(errorBar, Dock.Bottom);
        DockPanel.SetDock(hint, Dock.Top);
        stack.Children.Add(btnRow);
        stack.Children.Add(errorBar);
        stack.Children.Add(hint);
        stack.Children.Add(editor);

        dialog.Content = stack;
        dialog.ShowDialog();
    }
}

// MARK: - Settings window ---------------------------------------------------

public sealed class SettingsWindow : Window
{
    private static readonly Brush CardBg =
        (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
    private static readonly Brush CardStroke =
        (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
    private static readonly Brush SecondaryText =
        (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];

    public SettingsWindow()
    {
        Title = "Settings";
        Width = 540;
        Height = 600;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)Application.Current.Resources["ApplicationBackgroundBrush"];
        Icon = AppIcons.AppIcon;

        var assemblyVersion =
            System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
            ?? "0.0.0";

        // Close button (bottom row).
        var closeBtn = new Button
        {
            Content = "Close",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
            IsDefault = true,
            IsCancel = true,
            Appearance = ControlAppearance.Primary,
            Padding = new Thickness(20, 6, 20, 6)
        };
        closeBtn.Click += (_, _) => Close();

        var stack = new StackPanel();

        // Title header
        stack.Children.Add(new TextBlock
        {
            Text = "Settings",
            FontSize = 28,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        });
        stack.Children.Add(new TextBlock
        {
            Text = "AI Exposure Scanner preferences and links",
            Foreground = SecondaryText,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 20)
        });

        // About card
        stack.Children.Add(BuildCard("About", new[]
        {
            KeyValueRow("Application", "AI Exposure Scanner"),
            KeyValueRow("Version", assemblyVersion),
            KeyValueRow("Author", "Lukáš Oplt"),
            KeyValueRow("License", "Apache-2.0")
        }));

        // Links card
        stack.Children.Add(BuildCard("Links", new UIElement[]
        {
            LinkRow(SymbolRegular.ArrowDownload24, "GitHub Releases",
                "https://github.com/lukoplt/ai-exposure-scanner/releases"),
            LinkRow(SymbolRegular.Code24, "Source code on GitHub",
                "https://github.com/lukoplt/ai-exposure-scanner"),
            LinkRow(SymbolRegular.Bug24, "Report an issue",
                "https://github.com/lukoplt/ai-exposure-scanner/issues/new")
        }));

        // Support card
        stack.Children.Add(BuildCard("Support", new UIElement[]
        {
            new TextBlock
            {
                Text = "Made with ❤ by Lukáš Oplt",
                Foreground = SecondaryText,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 10)
            }
        }));

        stack.Children.Add(closeBtn);

        // Outer scroll so we never clip on smaller DPI.
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(28, 24, 28, 24),
            Content = stack
        };
        Content = scroll;
    }

    private static Border BuildCard(string heading, UIElement[] body)
    {
        var headingBlock = new TextBlock
        {
            Text = heading,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = SecondaryText,
            Margin = new Thickness(0, 0, 0, 10)
        };
        // Letter-spacing emulation: use uppercase + slight tracking via Padding,
        // since plain WPF TextBlock has no CharacterSpacing pre-.NET 9.
        headingBlock.Text = heading.ToUpperInvariant();

        var inner = new StackPanel();
        inner.Children.Add(headingBlock);
        foreach (var elem in body) inner.Children.Add(elem);

        return new Border
        {
            Background = CardBg,
            BorderBrush = CardStroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 16),
            Child = inner
        };
    }

    private static UIElement KeyValueRow(string label, string value)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelBlock = new TextBlock
        {
            Text = label,
            Foreground = SecondaryText,
            VerticalAlignment = VerticalAlignment.Center
        };
        var valueBlock = new TextBlock
        {
            Text = value,
            FontWeight = FontWeights.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(labelBlock, 0);
        Grid.SetColumn(valueBlock, 1);
        grid.Children.Add(labelBlock);
        grid.Children.Add(valueBlock);
        return grid;
    }

    private static UIElement LinkRow(SymbolRegular icon, string text, string url)
    {
        var iconControl = new SymbolIcon(icon)
        {
            FontSize = 16,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
        };

        var run = new Run(text);
        var hyperlink = new Hyperlink(run) { NavigateUri = new Uri(url) };
        hyperlink.RequestNavigate += (_, e) =>
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        };
        var textBlock = new TextBlock
        {
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        textBlock.Inlines.Add(hyperlink);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 4, 0, 4)
        };
        row.Children.Add(iconControl);
        row.Children.Add(textBlock);
        return row;
    }

}

// MARK: - Main window -------------------------------------------------------

public sealed class MainWindow : FluentWindow
{
    private readonly ScanOrchestrator _orchestrator = new();
    private readonly ReportBuilder _reportBuilder = new();
    private readonly WinRulePackStore _rulePackStore = new();
    private readonly ObservableCollection<FindingListItem> _visibleFindings = [];

    private ScanResult? _result;
    private Finding? _selectedFinding;

    private readonly TextBlock _statusValue = new() { FontSize = 22, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock _toolsValue = new() { FontSize = 22, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock _mcpValue = new() { FontSize = 22, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock _criticalValue = new() { FontSize = 22, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock _highValue = new() { FontSize = 22, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock _mediumValue = new() { FontSize = 22, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock _lowValue = new() { FontSize = 22, FontWeight = FontWeights.SemiBold };

    private readonly ComboBox _severityFilter = new() { MinWidth = 140 };
    private readonly ComboBox _appFilter = new() { MinWidth = 200, Margin = new Thickness(8, 0, 0, 0) };
    private readonly ListBox _findingList = new()
    {
        BorderThickness = new Thickness(0),
        Background = Brushes.Transparent,
        HorizontalContentAlignment = HorizontalAlignment.Stretch
    };
    private readonly StackPanel _detailContent = new();

    private readonly Button _scanButton = new() { Appearance = ControlAppearance.Primary };
    private readonly Button _exportMarkdownButton = new();
    private readonly Button _exportHtmlButton = new();
    private readonly Button _exportPdfButton = new();
    private readonly Button _exportJsonButton = new();

    // "Last scan: 12:34:56" timestamp shown in the toolbar so the user
    // gets visible feedback even when a re-scan returns identical findings.
    private readonly TextBlock _lastScanLabel = new()
    {
        FontSize = 11,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(16, 0, 0, 0)
    };

    // Detected tools surfaced via the clickable Tools metric card (popup).
    private readonly System.Collections.ObjectModel.ObservableCollection<string> _detectedTools = [];
    private readonly System.Windows.Controls.Primitives.Popup _toolsPopup = new()
    {
        Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
        StaysOpen = false,
        AllowsTransparency = true,
        PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Fade
    };

    public MainWindow()
    {
        Title = "AI Exposure Scanner";
        Width = 1200;
        Height = 800;
        MinWidth = 980;
        MinHeight = 680;
        Icon = AppIcons.AppIcon;
        WindowBackdropType = WindowBackdropType.Mica;
        ExtendsContentIntoTitleBar = true;

        Content = BuildLayout();
        Loaded += (_, _) => Scan();
    }

    private UIElement BuildLayout()
    {
        var titleBar = new Wpf.Ui.Controls.TitleBar
        {
            Title = "AI Exposure Scanner",
            VerticalAlignment = VerticalAlignment.Top,
            Icon = new SymbolIcon(SymbolRegular.Shield24)
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // titlebar
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // toolbar
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // summary cards
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });  // main
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });   // disclaimer footer

        Grid.SetRow(titleBar, 0);
        root.Children.Add(titleBar);

        var toolbar = BuildToolbar();
        Grid.SetRow(toolbar, 1);
        root.Children.Add(toolbar);

        var summary = BuildSummary();
        Grid.SetRow(summary, 2);
        root.Children.Add(summary);

        var content = BuildContentGrid();
        Grid.SetRow(content, 3);
        root.Children.Add(content);

        var disclaimer = BuildDisclaimerBar();
        Grid.SetRow(disclaimer, 4);
        root.Children.Add(disclaimer);

        return root;
    }

    private UIElement BuildDisclaimerBar()
    {
        var icon = new SymbolIcon(SymbolRegular.Info24)
        {
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var label = new TextBlock
        {
            Text = "This tool cannot guarantee 100% security. Its purpose is to help you spot common AI exposure risks — it does not replace a full security audit.",
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");

        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(20, 8, 20, 8)
        };
        stack.Children.Add(icon);
        stack.Children.Add(label);

        var border = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            Background = (Brush)Application.Current.Resources["LayerOnAcrylicFillColorDefaultBrush"],
            Child = stack
        };
        return border;
    }

    private UIElement BuildToolbar()
    {
        _scanButton.Content = "Scan";
        _scanButton.Icon = new SymbolIcon(SymbolRegular.Play24);
        _scanButton.Click += (_, _) => Scan();

        _exportMarkdownButton.Content = "Markdown";
        _exportMarkdownButton.Icon = new SymbolIcon(SymbolRegular.DocumentText24);
        _exportMarkdownButton.Click += (_, _) => Export("AIExposureScanner-Report.md", "Markdown report (*.md)|*.md", r => _reportBuilder.Markdown(r));

        _exportHtmlButton.Content = "HTML";
        _exportHtmlButton.Icon = new SymbolIcon(SymbolRegular.Code24);
        _exportHtmlButton.Click += (_, _) => Export("AIExposureScanner-Report.html", "HTML report (*.html)|*.html", r => _reportBuilder.Html(r));

        _exportPdfButton.Content = "PDF";
        _exportPdfButton.Icon = new SymbolIcon(SymbolRegular.DocumentPdf24);
        _exportPdfButton.Click += (_, _) => ExportBytes("AIExposureScanner-Report.pdf", "PDF report (*.pdf)|*.pdf", r => _reportBuilder.Pdf(r));

        _exportJsonButton.Content = "JSON";
        _exportJsonButton.Icon = new SymbolIcon(SymbolRegular.Braces24);
        _exportJsonButton.Click += (_, _) => Export("AIExposureScanner-Report.json", "JSON report (*.json)|*.json", r => _reportBuilder.Json(r));

        var rulePacksBtn = new Button
        {
            Content = "Rule Packs",
            Icon = new SymbolIcon(SymbolRegular.DocumentBulletList24),
            Appearance = ControlAppearance.Secondary,
            Margin = new Thickness(16, 0, 0, 0)
        };
        rulePacksBtn.Click += (_, _) =>
        {
            try
            {
                var win = new RulePacksWindow(_rulePackStore) { Owner = this };
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, ex.ToString(), "Rule Packs failed to open",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        var settingsBtn = new Button
        {
            Content = "Settings",
            Icon = new SymbolIcon(SymbolRegular.Settings24),
            Appearance = ControlAppearance.Secondary,
            Margin = new Thickness(8, 0, 0, 0)
        };
        settingsBtn.Click += (_, _) =>
        {
            try
            {
                var win = new SettingsWindow { Owner = this };
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, ex.ToString(), "Settings failed to open",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        var leftStack = new StackPanel { Orientation = Orientation.Horizontal };
        leftStack.Children.Add(_scanButton);
        leftStack.Children.Add(Spacer());
        leftStack.Children.Add(_exportMarkdownButton);
        leftStack.Children.Add(Spacer());
        leftStack.Children.Add(_exportHtmlButton);
        leftStack.Children.Add(Spacer());
        leftStack.Children.Add(_exportPdfButton);
        leftStack.Children.Add(Spacer());
        leftStack.Children.Add(_exportJsonButton);
        leftStack.Children.Add(rulePacksBtn);
        leftStack.Children.Add(settingsBtn);

        _lastScanLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");

        var dock = new DockPanel { Margin = new Thickness(20, 48, 20, 12) };
        DockPanel.SetDock(leftStack, Dock.Left);
        dock.Children.Add(leftStack);
        dock.Children.Add(_lastScanLabel);

        UpdateExportButtons();
        return dock;
    }

    private static Border Spacer() => new() { Width = 8 };

    private UIElement BuildSummary()
    {
        var panel = new UniformGrid
        {
            Rows = 1,
            Columns = 7,
            Margin = new Thickness(20, 0, 20, 12)
        };
        panel.Children.Add(MetricCard("Status", _statusValue));
        panel.Children.Add(ToolsMetricCard());
        panel.Children.Add(MetricCard("MCP", _mcpValue));
        panel.Children.Add(MetricCard("Critical", _criticalValue, Severity.Critical));
        panel.Children.Add(MetricCard("High", _highValue, Severity.High));
        panel.Children.Add(MetricCard("Medium", _mediumValue, Severity.Medium));
        panel.Children.Add(MetricCard("Low", _lowValue, Severity.Low));
        return panel;
    }

    private UIElement ToolsMetricCard()
    {
        var labelBlock = new TextBlock
        {
            Text = "Tools",
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            Margin = new Thickness(0, 0, 0, 4)
        };

        _toolsValue.Text = "—";

        var chevron = new SymbolIcon(SymbolRegular.ChevronDown16)
        {
            FontSize = 12,
            Margin = new Thickness(6, 4, 0, 0),
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        };

        var valueRow = new StackPanel { Orientation = Orientation.Horizontal };
        valueRow.Children.Add(_toolsValue);
        valueRow.Children.Add(chevron);

        var stack = new StackPanel();
        stack.Children.Add(labelBlock);
        stack.Children.Add(valueRow);

        var border = new Border
        {
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(4, 0, 4, 0),
            CornerRadius = new CornerRadius(8),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            Child = stack,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        // Build the popup body — a vertically stacked list of detected app ids.
        var listItems = new ItemsControl
        {
            ItemsSource = _detectedTools,
            ItemTemplate = BuildToolItemTemplate()
        };
        // Popup uses AllowsTransparency=true so the rounded corners can
        // clip cleanly. That means the inner Border MUST paint a fully
        // opaque background — the WPF-UI "Card" brushes are translucent
        // acrylic by design and look see-through on a transparent popup.
        // SolidBackgroundFillColorBaseBrush is the opaque base layer.
        var popupCard = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(8),
            Background = (Brush)Application.Current.Resources["SolidBackgroundFillColorBaseBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 16,
                ShadowDepth = 2,
                Opacity = 0.35,
                Color = Colors.Black
            },
            Child = listItems,
            MinWidth = 220
        };
        _toolsPopup.Child = popupCard;
        _toolsPopup.PlacementTarget = border;

        border.MouseLeftButtonUp += (_, _) =>
        {
            if (_detectedTools.Count == 0) return;
            _toolsPopup.IsOpen = !_toolsPopup.IsOpen;
        };

        return border;
    }

    private static DataTemplate BuildToolItemTemplate()
    {
        // <StackPanel Orientation="Horizontal" Margin="0,3,0,3">
        //   <SymbolIcon Symbol="CheckmarkCircle24" Foreground="Green"/>
        //   <TextBlock Text="{Binding}" Margin="8,0,0,0"/>
        // </StackPanel>
        var row = new FrameworkElementFactory(typeof(StackPanel));
        row.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        row.SetValue(StackPanel.MarginProperty, new Thickness(0, 3, 0, 3));

        var icon = new FrameworkElementFactory(typeof(SymbolIcon));
        icon.SetValue(SymbolIcon.SymbolProperty, SymbolRegular.CheckmarkCircle24);
        icon.SetValue(SymbolIcon.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x2E, 0xA0, 0x43)));
        icon.SetValue(SymbolIcon.VerticalAlignmentProperty, VerticalAlignment.Center);

        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding());
        text.SetValue(TextBlock.MarginProperty, new Thickness(8, 0, 0, 0));
        text.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

        row.AppendChild(icon);
        row.AppendChild(text);
        return new DataTemplate { VisualTree = row };
    }

    private static Border MetricCard(string label, TextBlock value, Severity? severity = null)
    {
        var labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            Margin = new Thickness(0, 0, 0, 4)
        };

        if (severity is Severity sev)
        {
            value.Foreground = SeverityVisuals.Brush(sev);
        }
        value.Text = "—";

        var stack = new StackPanel();
        stack.Children.Add(labelBlock);
        stack.Children.Add(value);

        return new Border
        {
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(4, 0, 4, 0),
            CornerRadius = new CornerRadius(8),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            Child = stack
        };
    }

    private UIElement BuildContentGrid()
    {
        var grid = new Grid { Margin = new Thickness(20, 0, 20, 20) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var sidebar = BuildSidebar();
        Grid.SetColumn(sidebar, 0);
        grid.Children.Add(sidebar);

        var detail = BuildDetailPane();
        Grid.SetColumn(detail, 1);
        grid.Children.Add(detail);

        return grid;
    }

    private UIElement BuildSidebar()
    {
        _severityFilter.ItemsSource = new[] { "All", "Critical", "High", "Medium", "Low" };
        _severityFilter.SelectedIndex = 0;
        _severityFilter.SelectionChanged += (_, _) => RefreshVisibleFindings();

        _appFilter.SelectionChanged += (_, _) => RefreshVisibleFindings();

        var filters = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 12)
        };
        filters.Children.Add(_severityFilter);
        filters.Children.Add(_appFilter);

        _findingList.ItemsSource = _visibleFindings;
        _findingList.ItemTemplate = BuildFindingTemplate();
        _findingList.SelectionChanged += (_, _) =>
        {
            _selectedFinding = (_findingList.SelectedItem as FindingListItem)?.Finding;
            RefreshDetail();
        };

        var card = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(8),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 8, 0)
        };

        var dock = new DockPanel();
        DockPanel.SetDock(filters, Dock.Top);
        dock.Children.Add(filters);
        dock.Children.Add(_findingList);
        card.Child = dock;
        return card;
    }

    private static DataTemplate BuildFindingTemplate()
    {
        // <Border Padding="12" Margin="0,0,0,8" CornerRadius="6"
        //         Background="{DynamicResource ControlFillColorSecondaryBrush}">
        //   <StackPanel>
        //     <StackPanel Orientation="Horizontal">
        //       <SeverityPill/>  <TextBlock Text="{Binding RuleId}"/>
        //     </StackPanel>
        //     <TextBlock Text="{Binding Title}" FontWeight="SemiBold" TextWrapping="Wrap"/>
        //     <TextBlock Text="{Binding Subtitle}" Foreground="Secondary"/>
        //   </StackPanel>
        // </Border>

        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.PaddingProperty, new Thickness(12));
        border.SetValue(Border.MarginProperty, new Thickness(0, 0, 0, 8));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetResourceReference(Border.BackgroundProperty, "ControlFillColorSecondaryBrush");

        var stack = new FrameworkElementFactory(typeof(StackPanel));

        var topRow = new FrameworkElementFactory(typeof(StackPanel));
        topRow.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        topRow.SetValue(StackPanel.MarginProperty, new Thickness(0, 0, 0, 6));

        var pillHost = new FrameworkElementFactory(typeof(ContentControl));
        pillHost.SetBinding(ContentControl.ContentProperty, new Binding("SeverityPill"));
        pillHost.SetValue(ContentControl.VerticalAlignmentProperty, VerticalAlignment.Center);

        var ruleId = new FrameworkElementFactory(typeof(TextBlock));
        ruleId.SetBinding(TextBlock.TextProperty, new Binding("Finding.RuleId"));
        ruleId.SetValue(TextBlock.FontSizeProperty, 11.0);
        ruleId.SetValue(TextBlock.MarginProperty, new Thickness(8, 0, 0, 0));
        ruleId.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        ruleId.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");

        topRow.AppendChild(pillHost);
        topRow.AppendChild(ruleId);

        var title = new FrameworkElementFactory(typeof(TextBlock));
        title.SetBinding(TextBlock.TextProperty, new Binding("Finding.Title"));
        title.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        title.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);

        var subtitle = new FrameworkElementFactory(typeof(TextBlock));
        subtitle.SetBinding(TextBlock.TextProperty, new Binding("Subtitle"));
        subtitle.SetValue(TextBlock.FontSizeProperty, 11.0);
        subtitle.SetValue(TextBlock.MarginProperty, new Thickness(0, 2, 0, 0));
        subtitle.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");

        stack.AppendChild(topRow);
        stack.AppendChild(title);
        stack.AppendChild(subtitle);
        border.AppendChild(stack);

        return new DataTemplate { VisualTree = border };
    }

    private UIElement BuildDetailPane()
    {
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(24),
            Margin = new Thickness(8, 0, 0, 0)
        };

        var card = new Border
        {
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(8),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1)
        };
        scroll.Content = _detailContent;
        card.Child = scroll;
        return card;
    }

    // MARK: - Scan / refresh ------------------------------------------------

    private async void Scan()
    {
        try
        {
            _scanButton.IsEnabled = false;
            _scanButton.Content = "Scanning…";
            _result = await _orchestrator.ScanAsync(new LocalFilesystem(), packs: _rulePackStore.ActivePacks);
            RefreshSummary();
            RefreshAppFilter();
            RefreshDetectedTools();
            RefreshVisibleFindings();
            UpdateExportButtons();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "Scan failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _scanButton.IsEnabled = true;
            _scanButton.Content = "Scan";

            // Always update the timestamp so the user sees visible feedback
            // even when a re-scan produces the exact same findings as the
            // previous one.
            _lastScanLabel.Text = $"Last scan: {DateTime.Now:HH:mm:ss}";
        }
    }

    private void RefreshDetectedTools()
    {
        _detectedTools.Clear();
        if (_result is null) return;

        var ids = _result.Facts.McpServers.Select(m => m.AppId)
            .Concat(_result.Facts.Settings.Select(s => s.AppId))
            .Concat(_result.Facts.AuthFiles.Select(a => a.AppId))
            .Concat(_result.Facts.Extensions.Select(e => e.AppId))
            .Concat(_result.Facts.ConfigFiles.Select(c => c.AppId))
            .Concat(_result.Facts.AppInstallations.Where(a => a.Installed).Select(a => a.AppId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal);

        foreach (var id in ids) _detectedTools.Add(id);
    }

    private void RefreshSummary()
    {
        if (_result is null) return;
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
            .Concat((_result?.Findings ?? []).Select(f => f.App).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            .ToArray();
        _appFilter.ItemsSource = apps;
        _appFilter.SelectedItem = apps.Contains(selected, StringComparer.Ordinal) ? selected : "All";
    }

    private void RefreshVisibleFindings()
    {
        if (_result is null) return;

        var severity = _severityFilter.SelectedItem as string ?? "All";
        var app = _appFilter.SelectedItem as string ?? "All";

        var findings = _result.Findings
            .Where(f => severity == "All" || f.Severity.ToString() == severity)
            .Where(f => app == "All" || f.App == app)
            .OrderBy(f => f.Severity.SortRank())
            .ThenBy(f => f.RuleId, StringComparer.Ordinal)
            .ThenBy(f => f.App, StringComparer.Ordinal)
            .Select(f => new FindingListItem(f))
            .ToArray();

        _visibleFindings.Clear();
        foreach (var f in findings) _visibleFindings.Add(f);

        _findingList.SelectedIndex = _visibleFindings.Count > 0 ? 0 : -1;
        _selectedFinding = (_findingList.SelectedItem as FindingListItem)?.Finding;
        RefreshDetail();
    }

    private void RefreshDetail()
    {
        _detailContent.Children.Clear();

        if (_selectedFinding is null)
        {
            _detailContent.Children.Add(new TextBlock
            {
                Text = "No findings",
                FontSize = 22,
                FontWeight = FontWeights.SemiBold
            });
            _detailContent.Children.Add(new TextBlock
            {
                Text = "Run a scan or adjust the filters above.",
                Margin = new Thickness(0, 8, 0, 0),
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
            return;
        }

        var finding = _selectedFinding;

        // Severity pill + rule id
        var topRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 12)
        };
        topRow.Children.Add(SeverityVisuals.Pill(finding.Severity));
        topRow.Children.Add(new TextBlock
        {
            Text = finding.RuleId,
            FontSize = 12,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        });
        _detailContent.Children.Add(topRow);

        // Title
        _detailContent.Children.Add(new TextBlock
        {
            Text = finding.Title,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });

        // Meta grid
        _detailContent.Children.Add(BuildMetaGrid(finding));

        // Why this matters — InfoBar
        _detailContent.Children.Add(new InfoBar
        {
            Title = "Why this matters",
            Message = finding.Explanation,
            Severity = InfoBarSeverity.Warning,
            IsClosable = false,
            IsOpen = true,
            Margin = new Thickness(0, 16, 0, 0)
        });

        // Recommended fix — InfoBar
        _detailContent.Children.Add(new InfoBar
        {
            Title = "Recommended fix",
            Message = finding.Recommendation,
            Severity = InfoBarSeverity.Success,
            IsClosable = false,
            IsOpen = true,
            Margin = new Thickness(0, 8, 0, 0)
        });

        // Open config file
        if (finding.AffectedPath is not null)
        {
            var openBtn = new Button
            {
                Content = "Open config file",
                Icon = new SymbolIcon(SymbolRegular.Open24),
                Appearance = ControlAppearance.Secondary,
                Margin = new Thickness(0, 16, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            openBtn.Click += (_, _) => OpenSelectedConfig();
            _detailContent.Children.Add(openBtn);
        }
    }

    private UIElement BuildMetaGrid(Finding finding)
    {
        var rows = new List<(string Label, string? Value)>
        {
            ("App", finding.App),
            ("Server", finding.ServerName),
            ("Extension", finding.ExtensionId),
            ("Path", finding.AffectedPath),
            ("Masked value", finding.MaskedValue)
        }.Where(r => !string.IsNullOrEmpty(r.Value)).ToList();

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < rows.Count; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = new TextBlock
            {
                Text = rows[i].Label,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                Margin = new Thickness(0, 0, 8, 4)
            };
            var value = new TextBlock
            {
                Text = rows[i].Value,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            };
            Grid.SetRow(label, i);
            Grid.SetColumn(label, 0);
            Grid.SetRow(value, i);
            Grid.SetColumn(value, 1);
            grid.Children.Add(label);
            grid.Children.Add(value);
        }
        return grid;
    }

    // MARK: - Actions -------------------------------------------------------

    private void OpenSelectedConfig()
    {
        var path = _selectedFinding?.AffectedPath;
        if (string.IsNullOrEmpty(path)) return;

        // Defense in depth — paths come from detectors, but validate
        // anyway before handing to the Windows shell.
        if (!Path.IsPathFullyQualified(path)) return;
        if (!File.Exists(path)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = false
            });
        }
        catch
        {
            // Opening Explorer is best-effort; never crash the app on failure.
        }
    }

    private void Export(string fileName, string filter, Func<ScanResult, string> render)
    {
        if (_result is null) return;
        var dialog = new SaveFileDialog
        {
            FileName = fileName,
            Filter = filter,
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true) return;
        File.WriteAllText(dialog.FileName, render(_result));
    }

    private void ExportBytes(string fileName, string filter, Func<ScanResult, byte[]> render)
    {
        if (_result is null) return;
        var dialog = new SaveFileDialog
        {
            FileName = fileName,
            Filter = filter,
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true) return;
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
}

// MARK: - View model --------------------------------------------------------

internal sealed class FindingListItem
{
    public Finding Finding { get; }
    public UIElement SeverityPill => SeverityVisuals.Pill(Finding.Severity);
    public string Subtitle =>
        string.Join(" · ", new[] { Finding.App, Finding.ServerName, Finding.ExtensionId }
            .Where(s => !string.IsNullOrEmpty(s)));

    public FindingListItem(Finding finding) { Finding = finding; }
}
