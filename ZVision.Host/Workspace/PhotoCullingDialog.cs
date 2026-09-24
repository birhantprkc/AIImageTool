using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ZeroUI.Wpf.Editors;
using ZVision.Core;

namespace ZVision.Host.Workspace;

/// <summary>
/// Interactive review and confirmation dialog for AI Smart Photo Culling.
/// Allows photographers to review blur, highlight clipping, and burst deduplication proposals
/// before committing Lightroom Pick (P) and Reject (X) flags.
/// </summary>
public sealed class PhotoCullingDialog : Window
{
    private readonly IReadOnlyList<string> _imagePaths;
    private readonly IPhotoCullingService _cullingService;
    private readonly CancellationTokenSource _cts = new();

    private ProgressBar _progressBar = null!;
    private TextBlock _txtStatus = null!;
    private Border _summaryCard = null!;
    private TextBlock _txtTotal = null!;
    private TextBlock _txtRejects = null!;
    private TextBlock _txtPicks = null!;
    private TextBlock _txtDuplicates = null!;
    private ListView _lvResults = null!;
    private CheckBox _chkApplyRejects = null!;
    private CheckBox _chkApplyPicks = null!;
    private CheckBox _chkApplyRatings = null!;
    private SimpleButton _btnApply = null!;
    private SimpleButton _btnCancel = null!;

    private PhotoCullSummary? _summary;

    public PhotoCullingDialog(IReadOnlyList<string> imagePaths, IPhotoCullingService cullingService)
    {
        _imagePaths = imagePaths ?? throw new ArgumentNullException(nameof(imagePaths));
        _cullingService = cullingService ?? throw new ArgumentNullException(nameof(cullingService));

        Title = "✨ Smart Photo Culling & Duplicate Detection";
        Width = 720;
        Height = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = ThemeManager.GetBrush("BgPanelBrush");
        Foreground = ThemeManager.GetBrush("TextPrimaryBrush");
        WindowStyle = WindowStyle.ToolWindow;

        BuildUi();
        Loaded += OnLoaded;
        Closing += (s, e) => _cts.Cancel();
    }

    private void BuildUi()
    {
        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Progress & Stats
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // List
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Options
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

        // 1. Header
        var headerPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        headerPanel.Children.Add(new TextBlock
        {
            Text = "✨ Intelligent Photo Culling & Burst Deduplication",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = ThemeManager.GetBrush("TextPrimaryBrush")
        });
        headerPanel.Children.Add(new TextBlock
        {
            Text = "Analyzes severe blur (out-of-focus), blown highlights, and burst sequences to propose Lightroom Pick (P) and Reject (X) flags.",
            FontSize = 11,
            Foreground = ThemeManager.GetBrush("TextSecondaryBrush"),
            Margin = new Thickness(0, 4, 0, 0)
        });
        Grid.SetRow(headerPanel, 0);
        root.Children.Add(headerPanel);

        // 2. Progress & Summary Card
        var progressPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

        _progressBar = new ProgressBar
        {
            Height = 6,
            Minimum = 0,
            Maximum = Math.Max(1, _imagePaths.Count),
            Value = 0,
            Foreground = ThemeManager.GetBrush("AccentBrush"),
            Background = ThemeManager.GetBrush("BgBaseBrush"),
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 0, 0, 6)
        };
        progressPanel.Children.Add(_progressBar);

        _txtStatus = new TextBlock
        {
            Text = $"Preparing to analyze {_imagePaths.Count} photos...",
            FontSize = 11,
            Foreground = ThemeManager.GetBrush("TextDimBrush"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        progressPanel.Children.Add(_txtStatus);

        // Stats Card
        _summaryCard = new Border
        {
            Background = ThemeManager.GetBrush("BgPanelAltBrush"),
            BorderBrush = ThemeManager.GetBrush("BorderBrush_"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 12, 8),
            Visibility = Visibility.Collapsed
        };

        var statsGrid = new UniformGrid { Columns = 4 };
        statsGrid.Children.Add(CreateStatBadge("Total Photos", out _txtTotal, ThemeManager.GetBrush("TextPrimaryBrush")));
        statsGrid.Children.Add(CreateStatBadge("Recommended Rejects", out _txtRejects, new SolidColorBrush(Color.FromRgb(244, 67, 54))));
        statsGrid.Children.Add(CreateStatBadge("Recommended Picks", out _txtPicks, new SolidColorBrush(Color.FromRgb(76, 175, 80))));
        statsGrid.Children.Add(CreateStatBadge("Burst Groups", out _txtDuplicates, new SolidColorBrush(Color.FromRgb(33, 150, 243))));

        _summaryCard.Child = statsGrid;
        progressPanel.Children.Add(_summaryCard);

        Grid.SetRow(progressPanel, 1);
        root.Children.Add(progressPanel);

        // 3. Results ListView
        _lvResults = new ListView
        {
            Background = ThemeManager.GetBrush("BgBaseBrush"),
            Foreground = ThemeManager.GetBrush("TextPrimaryBrush"),
            BorderBrush = ThemeManager.GetBrush("BorderBrush_"),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 12)
        };

        var gridView = new GridView();
        gridView.Columns.Add(new GridViewColumn
        {
            Header = "Flag",
            Width = 70,
            DisplayMemberBinding = new System.Windows.Data.Binding(nameof(CullItemViewModel.FlagDisplay))
        });
        gridView.Columns.Add(new GridViewColumn
        {
            Header = "Rating",
            Width = 60,
            DisplayMemberBinding = new System.Windows.Data.Binding(nameof(CullItemViewModel.RatingDisplay))
        });
        gridView.Columns.Add(new GridViewColumn
        {
            Header = "File Name",
            Width = 160,
            DisplayMemberBinding = new System.Windows.Data.Binding(nameof(CullItemViewModel.FileName))
        });
        gridView.Columns.Add(new GridViewColumn
        {
            Header = "TQI",
            Width = 50,
            DisplayMemberBinding = new System.Windows.Data.Binding(nameof(CullItemViewModel.TqiDisplay))
        });
        gridView.Columns.Add(new GridViewColumn
        {
            Header = "Reason / Diagnosis",
            Width = 340,
            DisplayMemberBinding = new System.Windows.Data.Binding(nameof(CullItemViewModel.ReasonDisplay))
        });
        _lvResults.View = gridView;

        Grid.SetRow(_lvResults, 2);
        root.Children.Add(_lvResults);

        // 4. Options checkboxes
        var optionsPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };

        _chkApplyRejects = new CheckBox
        {
            Content = "Set Reject flag (X) for blur, blown exposure & redundant duplicates",
            IsChecked = true,
            Foreground = ThemeManager.GetBrush("TextPrimaryBrush"),
            Margin = new Thickness(0, 0, 16, 4)
        };
        optionsPanel.Children.Add(_chkApplyRejects);

        _chkApplyPicks = new CheckBox
        {
            Content = "Set Pick flag (P) for best shots",
            IsChecked = true,
            Foreground = ThemeManager.GetBrush("TextPrimaryBrush"),
            Margin = new Thickness(0, 0, 16, 4)
        };
        optionsPanel.Children.Add(_chkApplyPicks);

        _chkApplyRatings = new CheckBox
        {
            Content = "Apply Quality Star Ratings (1★ - 5★)",
            IsChecked = true,
            Foreground = ThemeManager.GetBrush("TextPrimaryBrush"),
            Margin = new Thickness(0, 0, 16, 4)
        };
        optionsPanel.Children.Add(_chkApplyRatings);

        Grid.SetRow(optionsPanel, 3);
        root.Children.Add(optionsPanel);

        // 5. Actions Footer
        var btnPanel = new DockPanel();

        _btnCancel = new SimpleButton
        {
            Content = "Cancel",
            Width = 90,
            Height = 28,
            Margin = new Thickness(8, 0, 0, 0)
        };
        DockPanel.SetDock(_btnCancel, Dock.Right);
        _btnCancel.Click += (s, e) => Close();
        btnPanel.Children.Add(_btnCancel);

        _btnApply = new SimpleButton
        {
            Content = "Apply Flags to Workspace",
            Width = 180,
            Height = 28,
            IsEnabled = false
        };
        DockPanel.SetDock(_btnApply, Dock.Right);
        _btnApply.Click += BtnApply_Click;
        btnPanel.Children.Add(_btnApply);

        Grid.SetRow(btnPanel, 4);
        root.Children.Add(btnPanel);

        Content = root;
    }

    private static FrameworkElement CreateStatBadge(string label, out TextBlock valBlock, Brush valueColor)
    {
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        valBlock = new TextBlock
        {
            Text = "0",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = valueColor,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var lblBlock = new TextBlock
        {
            Text = label,
            FontSize = 10,
            Foreground = ThemeManager.GetBrush("TextSecondaryBrush"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        panel.Children.Add(valBlock);
        panel.Children.Add(lblBlock);
        return panel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var progress = new Progress<(int current, int total, string currentFile)>(info =>
        {
            _progressBar.Value = info.current;
            _txtStatus.Text = $"Analyzing ({info.current}/{info.total}): {info.currentFile}...";
        });

        try
        {
            _summary = await _cullingService.AnalyzePhotosAsync(_imagePaths, progress, _cts.Token);

            _progressBar.Visibility = Visibility.Collapsed;
            _txtStatus.Text = $"Analysis complete: {_summary.TotalAnalyzed} photos scanned.";
            _summaryCard.Visibility = Visibility.Visible;

            _txtTotal.Text = _summary.TotalAnalyzed.ToString();
            _txtRejects.Text = _summary.RecommendedRejections.ToString();
            _txtPicks.Text = _summary.RecommendedPicks.ToString();
            _txtDuplicates.Text = _summary.DuplicateClustersCount.ToString();

            var vms = _summary.Items
                .OrderByDescending(i => i.ProposedFlag == PickFlag.Reject)
                .ThenByDescending(i => i.ProposedFlag == PickFlag.Pick)
                .ThenBy(i => i.TqiScore)
                .Select(i => new CullItemViewModel(i))
                .ToList();

            _lvResults.ItemsSource = vms;
            _btnApply.IsEnabled = _summary.TotalAnalyzed > 0;
        }
        catch (OperationCanceledException)
        {
            _txtStatus.Text = "Analysis canceled.";
        }
        catch (Exception ex)
        {
            _txtStatus.Text = $"Analysis error: {ex.Message}";
        }
    }

    private async void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        if (_summary == null || _summary.Items.Count == 0)
        {
            Close();
            return;
        }

        _btnApply.IsEnabled = false;
        _btnCancel.IsEnabled = false;
        _txtStatus.Text = "Applying flags and ratings to workspace...";

        bool applyRejects = _chkApplyRejects.IsChecked == true;
        bool applyPicks = _chkApplyPicks.IsChecked == true;
        bool applyRatings = _chkApplyRatings.IsChecked == true;

        await _cullingService.ApplyCullingFlagsAsync(_summary.Items, applyRejects, applyPicks, applyRatings);

        DialogResult = true;
        Close();
    }

    private sealed class CullItemViewModel
    {
        public PhotoCullItem Item { get; }

        public CullItemViewModel(PhotoCullItem item)
        {
            Item = item;
        }

        public string FileName => Item.FileName;

        public string FlagDisplay => Item.ProposedFlag switch
        {
            PickFlag.Reject => "✗ Reject",
            PickFlag.Pick => "✓ Pick",
            _ => "Keep"
        };

        public string RatingDisplay => Item.StarRating > 0 ? $"{Item.StarRating}★" : "";

        public string TqiDisplay => $"{Item.TqiScore:F0}";

        public string ReasonDisplay => Item.Reasons.Count > 0
            ? string.Join("; ", Item.Reasons)
            : "Balanced exposure and sharpness";
    }
}
