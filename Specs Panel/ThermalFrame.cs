using System.Diagnostics;

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
    private const double PulsePeriodSeconds = 3.2;
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
        if (criticalSince is not long start)
            return;

        var depth = Math.Min(Math.Min(Padding.Left, Padding.Right), Math.Min(Padding.Top, Padding.Bottom));
        if (depth == 0 || Width <= 2 * depth || Height <= 2 * depth)
            return;

        var elapsed = Stopwatch.GetElapsedTime(start).TotalSeconds;
        var pulse = .5 + .5 * Math.Cos(2 * Math.PI * elapsed / PulsePeriodSeconds);
        var opacity = 16 + 168 * pulse;
        using var pen = new Pen(Glow);
        for (var inset = 0; inset < depth; inset++)
        {
            var fade = 1 - (double)inset / depth;
            pen.Color = Color.FromArgb((int)(opacity * fade * fade), Glow);
            args.Graphics.DrawRectangle(pen, inset, inset, Width - 1 - 2 * inset, Height - 1 - 2 * inset);
        }
    }
}
