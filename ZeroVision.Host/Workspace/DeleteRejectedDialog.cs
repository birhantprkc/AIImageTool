using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ZeroVision.Host.Workspace;

public enum DeleteRejectedChoice
{
    Cancel,
    RecycleBin,
    CatalogOnly
}

/// <summary>
/// Confirmation dialog for Batch Reject Cleanup (Ctrl+Backspace).
/// Displays count of rejected photos and total disk space, with options to move to Recycle Bin or remove from Catalog.
/// </summary>
public sealed class DeleteRejectedDialog : Window
{
    public DeleteRejectedChoice Choice { get; private set; } = DeleteRejectedChoice.Cancel;

    public DeleteRejectedDialog(int count, long totalBytes)
    {
        Title = "Delete Rejected Photos";
        Width = 460;
        Height = 220;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = ThemeManager.GetBrush("BgPanelBrush");
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.ToolWindow;

        double mb = totalBytes / (1024.0 * 1024.0);
        string sizeStr = mb >= 1024 ? $"{mb / 1024.0:F2} GB" : $"{mb:F1} MB";

        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var titleBlock = new TextBlock
        {
            Text = $"Found {count} rejected photo{(count == 1 ? "" : "s")}",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = ThemeManager.GetBrush("TextPrimaryBrush"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(titleBlock, 0);
        root.Children.Add(titleBlock);

        var descBlock = new TextBlock
        {
            Text = $"Total disk space: {sizeStr}.\nWould you like to move these files to the Recycle Bin, or only remove them from the catalog/workspace view?",
            FontSize = 12,
            Foreground = ThemeManager.GetBrush("TextSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 18,
            Margin = new Thickness(0, 0, 0, 16)
        };
        Grid.SetRow(descBlock, 1);
        root.Children.Add(descBlock);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetRow(btnPanel, 3);

        var btnRecycle = new Button
        {
            Content = "Move to Recycle Bin",
            Padding = new Thickness(14, 6, 14, 6),
            Margin = new Thickness(0, 0, 8, 0),
            Background = ThemeManager.GetBrush("DangerBrush"),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand,
            IsDefault = true
        };
        btnRecycle.Click += (_, _) => { Choice = DeleteRejectedChoice.RecycleBin; DialogResult = true; Close(); };
        btnPanel.Children.Add(btnRecycle);

        var btnCatalog = new Button
        {
            Content = "Remove from Catalog",
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 0, 8, 0),
            Background = ThemeManager.GetBrush("BgHoverBrush"),
            Foreground = ThemeManager.GetBrush("TextPrimaryBrush"),
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        btnCatalog.Click += (_, _) => { Choice = DeleteRejectedChoice.CatalogOnly; DialogResult = true; Close(); };
        btnPanel.Children.Add(btnCatalog);

        var btnCancel = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(12, 6, 12, 6),
            Background = Brushes.Transparent,
            Foreground = ThemeManager.GetBrush("TextDimBrush"),
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            IsCancel = true
        };
        btnCancel.Click += (_, _) => { Choice = DeleteRejectedChoice.Cancel; DialogResult = false; Close(); };
        btnPanel.Children.Add(btnCancel);

        root.Children.Add(btnPanel);
        Content = root;
    }
}
