using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace SpecsPanel;

internal enum TemperatureAlert
{
    Normal,
    Warning,
    Critical
}

internal static class ThermalAlerts
{
    private const float WarningThreshold = 80;
    private const float CriticalThreshold = 88;
    private const float ClearMargin = 2;

    public static TemperatureAlert Update(float? temperature, TemperatureAlert previous)
    {
        if (temperature is not float value || !float.IsFinite(value))
            return TemperatureAlert.Normal;
        if (value >= CriticalThreshold || (previous == TemperatureAlert.Critical && value > CriticalThreshold - ClearMargin))
            return TemperatureAlert.Critical;
        if (value >= WarningThreshold || (previous != TemperatureAlert.Normal && value > WarningThreshold - ClearMargin))
            return TemperatureAlert.Warning;
        return TemperatureAlert.Normal;
    }
}

internal sealed class ThermalFrame : Panel
{
    private static readonly Color Glow = Color.FromArgb(255, 49, 72);
    private long? criticalSince;

    public ThermalFrame()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(18, 12, 18, 12);
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    public void SetCritical(bool critical)
    {
        if (critical == criticalSince.HasValue)
            return;
        criticalSince = critical ? Stopwatch.GetTimestamp() : null;
        InvalidateEdges();
    }

    public void Animate()
    {
        if (criticalSince.HasValue)
            InvalidateEdges();
    }

    private void InvalidateEdges()
    {
        Invalidate(new Rectangle(0, 0, Width, Padding.Top));
        Invalidate(new Rectangle(0, Height - Padding.Bottom, Width, Padding.Bottom));
        Invalidate(new Rectangle(0, 0, Padding.Left, Height));
        Invalidate(new Rectangle(Width - Padding.Right, 0, Padding.Right, Height));
    }

    protected override void OnPaint(PaintEventArgs args)
    {
        base.OnPaint(args);
        if (criticalSince is not long start || Width < 2 * Padding.Horizontal || Height < 2 * Padding.Vertical)
            return;

        var elapsed = Stopwatch.GetElapsedTime(start).TotalSeconds;
        var pulse = .5 + .5 * Math.Cos(2 * Math.PI * elapsed / 1.4);
        var red = Color.FromArgb((int)(28 + 138 * pulse), Glow);
        var length = Math.Min(Math.Min(Width, Height) / 3, LogicalToDeviceUnits(190));
        var right = Width - length;
        var bottom = Height - length;
        PaintGlow(args.Graphics, new(0, 0, length, Padding.Top), LinearGradientMode.Horizontal, false, red);
        PaintGlow(args.Graphics, new(right, 0, length, Padding.Top), LinearGradientMode.Horizontal, true, red);
        PaintGlow(args.Graphics, new(0, Height - Padding.Bottom, length, Padding.Bottom), LinearGradientMode.Horizontal, false, red);
        PaintGlow(args.Graphics, new(right, Height - Padding.Bottom, length, Padding.Bottom), LinearGradientMode.Horizontal, true, red);
        PaintGlow(args.Graphics, new(0, Padding.Top, Padding.Left, length - Padding.Top), LinearGradientMode.Vertical, false, red);
        PaintGlow(args.Graphics, new(Width - Padding.Right, Padding.Top, Padding.Right, length - Padding.Top), LinearGradientMode.Vertical, false, red);
        PaintGlow(args.Graphics, new(0, bottom, Padding.Left, length - Padding.Bottom), LinearGradientMode.Vertical, true, red);
        PaintGlow(args.Graphics, new(Width - Padding.Right, bottom, Padding.Right, length - Padding.Bottom), LinearGradientMode.Vertical, true, red);
    }

    private static void PaintGlow(Graphics canvas, Rectangle bounds, LinearGradientMode direction, bool reverse, Color red)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        var clear = Color.FromArgb(0, red);
        using var brush = new LinearGradientBrush(bounds, reverse ? clear : red, reverse ? red : clear, direction)
        {
            WrapMode = WrapMode.TileFlipXY
        };
        canvas.FillRectangle(brush, bounds);
    }
}
