using System;
using System.Windows.Media;

namespace ZeroVision.Host.Workspace;

public class ViewportChangedEventArgs : EventArgs
{
    public ImageSource? Source { get; }
    public double Zoom { get; }
    public double NormX { get; }
    public double NormY { get; }
    public double NormW { get; }
    public double NormH { get; }

    public ViewportChangedEventArgs(ImageSource? source, double zoom, double normX, double normY, double normW, double normH)
    {
        Source = source;
        Zoom = zoom;
        NormX = normX;
        NormY = normY;
        NormW = normW;
        NormH = normH;
    }
}
