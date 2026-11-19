using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace SpecsPanel;

internal sealed class MetricCard : Control
{
    private const double HistorySeconds = 60;
    private static readonly Color TextColor = Color.FromArgb(237, 245, 255);
    private static readonly Color Subtle = Color.FromArgb(163, 180, 202);
    private readonly Color accent;
    private readonly string title;
    private readonly string unit;
    private readonly string readingLabel;
    private readonly Queue<(long Timestamp, float? Value)> history = new();
    private string device = "Iskanje naprave …";
    private string detail = "—";
    private float? value;

    public MetricCard(string title, string unit, Color accent)
    {
        this.title = title;
        this.unit = unit;
        this.accent = accent;
        readingLabel = unit == "%" ? "ZASEDENOST" : "TEMPERATURA";
        AccessibleName = title;
        Dock = DockStyle.Fill;
        Margin = new Padding(0, 0, 10, 0);
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    public void SetReading(float? next, string deviceName, string detailText)
    {
        var now = Stopwatch.GetTimestamp();
        if (device != deviceName)
            history.Clear();
        while (history.TryPeek(out var sample) && Stopwatch.GetElapsedTime(sample.Timestamp, now).TotalSeconds > HistorySeconds)
            history.Dequeue();
        history.Enqueue((now, next));

        value = next;
        device = deviceName;
        detail = detailText;
        AccessibleDescription = $"{device}, {readingLabel.ToLowerInvariant()} {(value is float measurement ? $"{measurement:0.#} {unit}" : "ni na voljo")}, {detail}";
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 35 || Height < 45)
            return;

        var canvas = e.Graphics;
        canvas.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = Math.Clamp(Math.Min(Width / 274f, Height / 365f), .38f, 2.15f);
        float pad = 19 * scale;
        float bodyWidth = Width - 2 * pad;
        using var outline = CardOutline(Width - 1f, Height - 1f, 16 * scale);
        using var fill = new LinearGradientBrush(ClientRectangle, Color.FromArgb(22, 35, 52), Color.FromArgb(10, 17, 29), 70f);
        using var border = new Pen(Color.FromArgb(100, accent));
        using var highlight = new Pen(accent, Math.Max(1.5f, 2 * scale));
        canvas.FillPath(fill, outline);
        canvas.DrawPath(border, outline);
        canvas.DrawLine(highlight, pad, 2 * scale, pad + 47 * scale, 2 * scale);

        DrawText(canvas, title, accent, 18 * scale, FontStyle.Bold,
            new(pad, 17 * scale, bodyWidth, 28 * scale));
        DrawText(canvas, device, Subtle, 13 * scale, FontStyle.Regular,
            new(pad, 50 * scale, bodyWidth, 26 * scale));
        DrawText(canvas, readingLabel, Subtle, 10.5f * scale, FontStyle.Bold,
            new(pad, 88 * scale, bodyWidth, 18 * scale));
        DrawText(canvas, value is float measurement ? $"{measurement:0.#} {unit}" : "—", TextColor,
            46 * scale, FontStyle.Bold, new(pad, 106 * scale, bodyWidth, 65 * scale), "Segoe UI");
        DrawText(canvas, detail, accent, 12 * scale, FontStyle.Regular,
            new(pad, 173 * scale, bodyWidth, 25 * scale));

        float plotTop = 225 * scale;
        float plotBottom = Height - 41 * scale;
        if (plotBottom > plotTop + 8)
        {
            DrawHistory(canvas, new(pad, plotTop, bodyWidth, plotBottom - plotTop), scale);
            DrawText(canvas, "ZADNJIH 60 S", Subtle, 10.5f * scale, FontStyle.Bold,
                new(pad, plotBottom + 10 * scale, bodyWidth, 20 * scale));
        }
    }

    private static GraphicsPath CardOutline(float width, float height, float corner)
    {
        var path = new GraphicsPath();
        PointF[] points = [new(0, 0), new(width - corner, 0), new(width, corner),
            new(width, height), new(corner, height), new(0, height - corner)];
        path.AddPolygon(points);
        return path;
    }

    private void DrawHistory(Graphics canvas, RectangleF chart, float scale)
    {
        using var well = new SolidBrush(Color.FromArgb(83, 5, 11, 22));
        using var grid = new Pen(Color.FromArgb(54, 129, 152, 179));
        using var trend = new Pen(accent, Math.Max(1.4f, 2 * scale)) { LineJoin = LineJoin.Round };
        using var dot = new SolidBrush(accent);
        canvas.FillRectangle(well, chart);
        for (var row = 0; row <= 4; row++)
        {
            var y = chart.Top + chart.Height * row / 4;
            canvas.DrawLine(grid, chart.Left, y, chart.Right, y);
        }
        for (var column = 0; column <= 6; column++)
        {
            var x = chart.Left + chart.Width * column / 6;
            canvas.DrawLine(grid, x, chart.Top, x, chart.Bottom);
        }

        if (!history.Any(sample => sample.Value.HasValue))
            return;

        var highest = history.Max(sample => sample.Value) ?? 0;
        var lowest = history.Min(sample => sample.Value) ?? 0;
        var min = unit == "%" ? 0 : Math.Max(0, Math.Floor(lowest / 10 - 1) * 10);
        var max = unit == "%" ? 100 : Math.Max(min + 30, Math.Ceiling(highest / 10 + 1) * 10);
        var now = history.Last().Timestamp;
        using var area = new LinearGradientBrush(chart, Color.FromArgb(80, accent), Color.FromArgb(0, accent), 90f);
        var segment = new List<PointF>();
        foreach (var sample in history)
        {
            if (sample.Value is not float pointValue)
            {
                DrawSegment(canvas, segment, chart.Bottom, area, trend, dot);
                segment.Clear();
                continue;
            }
            var fraction = Math.Clamp(1 - Stopwatch.GetElapsedTime(sample.Timestamp, now).TotalSeconds / HistorySeconds, 0, 1);
            var relative = Math.Clamp((pointValue - min) / (max - min), 0, 1);
            segment.Add(new(chart.Left + chart.Width * (float)fraction, chart.Bottom - chart.Height * (float)relative));
        }
        DrawSegment(canvas, segment, chart.Bottom, area, trend, dot);
    }

    private static void DrawSegment(Graphics canvas, List<PointF> points, float baseline, Brush fill, Pen line, Brush dot)
    {
        if (points.Count == 0)
            return;
        if (points.Count > 1)
        {
            PointF[] polygon = [new(points[0].X, baseline), .. points, new(points[^1].X, baseline)];
            canvas.FillPolygon(fill, polygon);
            canvas.DrawLines(line, points.ToArray());
        }
        var point = points[^1];
        var radius = line.Width * 1.25f;
        canvas.FillEllipse(dot, point.X - radius, point.Y - radius, 2 * radius, 2 * radius);
    }

    private static void DrawText(Graphics canvas, string text, Color color, float size, FontStyle style,
        RectangleF bounds, string family = "Consolas")
    {
        using var font = new Font(family, size, style, GraphicsUnit.Pixel);
        TextRenderer.DrawText(canvas, text, font, Rectangle.Round(bounds), color,
            TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine |
            TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);
    }
}
