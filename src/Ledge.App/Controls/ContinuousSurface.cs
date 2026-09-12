namespace Ledge.App.Controls;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ledge.Core.Models;

/// <summary>A shared superelliptic surface, with a continuous curve into each straight edge.</summary>
public class ContinuousSurface : Border
{
    public static readonly DependencyProperty ElevationProperty = DependencyProperty.Register(
        nameof(Elevation), typeof(double), typeof(ContinuousSurface), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Elevation { get => (double)GetValue(ElevationProperty); set => SetValue(ElevationProperty, value); }

    public static Geometry Outline(Rect rect, double radius)
    {
        radius = Math.Clamp(radius, 0, Math.Min(rect.Width, rect.Height) / 2);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var centers = new[] { new Point(rect.Right - radius, rect.Top + radius), new Point(rect.Right - radius, rect.Bottom - radius),
                new Point(rect.Left + radius, rect.Bottom - radius), new Point(rect.Left + radius, rect.Top + radius) };
            context.BeginFigure(new Point(rect.Left + radius, rect.Top), true, true);
            for (var corner = 0; corner < 4; corner++)
            {
                for (var step = 0; step <= 24; step++)
                {
                    var angle = (-90 + corner * 90 + step * 90.0 / 24) * Math.PI / 180;
                    var x = Math.Cos(angle); var y = Math.Sin(angle);
                    context.LineTo(new Point(centers[corner].X + radius * Math.CopySign(Math.Sqrt(Math.Abs(x)), x),
                        centers[corner].Y + radius * Math.CopySign(Math.Sqrt(Math.Abs(y)), y)), true, false);
                }
            }
        }
        geometry.Freeze();
        return geometry;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(.5, .5, Math.Max(0, ActualWidth - 1), Math.Max(0, ActualHeight - 1));
        var shape = Outline(bounds, CornerRadius.TopLeft);
        if (Elevation > 0 && !SystemParameters.HighContrast)
        {
            // Two shadow lobes: broad ambient separation and a small contact shadow.
            for (var i = 10; i >= 1; i--)
            {
                dc.PushTransform(new TranslateTransform(0, 2 + Elevation * .25));
                dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb((byte)(i < 3 ? 7 : 3), 0, 0, 0)), i * Elevation / 5), shape);
                dc.Pop();
            }
        }
        dc.DrawGeometry(Background, BorderBrush == null ? null : new Pen(BorderBrush, Math.Max(BorderThickness.Left, BorderThickness.Top)), shape);
        if (Elevation > 0 && !SystemParameters.HighContrast)
            dc.DrawGeometry(null, new Pen(new LinearGradientBrush(Color.FromArgb(95, 255, 255, 255), Colors.Transparent, 90), 1), shape);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        if (Child != null)
        {
            var x = Padding.Left + BorderThickness.Left;
            var y = Padding.Top + BorderThickness.Top;
            Child.Clip = Outline(new Rect(-x + .5, -y + .5, Math.Max(0, finalSize.Width - 1), Math.Max(0, finalSize.Height - 1)), CornerRadius.TopLeft);
        }
        return result;
    }

    public static Brush NoteFill(string? color)
    {
        var basis = (Color)ColorConverter.ConvertFromString(color.ToHex());
        var highlight = Color.FromRgb((byte)(basis.R + (255 - basis.R) * .22), (byte)(basis.G + (255 - basis.G) * .22), (byte)(basis.B + (255 - basis.B) * .22));
        var brush = new LinearGradientBrush(highlight, basis, 105);
        brush.Freeze();
        return brush;
    }
}
