using System.Windows;

namespace ZVision.Host.Workspace;

// Quick rotate/flip buttons on center mode bar — delegates to DevelopPanel (history-aware, non-destructive).
public partial class CenterPreview
{
    private void BtnRotateLeft_Click(object sender, RoutedEventArgs e) => _developPanel?.RotateActive(-1);
    private void BtnRotateRight_Click(object sender, RoutedEventArgs e) => _developPanel?.RotateActive(1);
    private void BtnFlipH_Click(object sender, RoutedEventArgs e) => _developPanel?.FlipActive(true);
}
