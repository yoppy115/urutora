using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace Simulation.App;

public sealed class WorldStatisticsChartPanel : Panel
{
    private readonly ToolTip _toolTip = new()
    {
        AutoPopDelay = 8_000,
        InitialDelay = 120,
        ReshowDelay = 50,
        ShowAlways = true
    };
    private IReadOnlyList<WorldMetricPoint> _metrics = Array.Empty<WorldMetricPoint>();
    private Rectangle _populationPlot;
    private Rectangle _agePlot;
    private int _hoverIndex = -1;
    private int _daysPerYear = 365;

    public WorldStatisticsChartPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        Padding = new Padding(10);
        MouseMove += HandleMouseMove;
        MouseLeave += (_, _) => ClearHover();
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<WorldMetricPoint> Metrics
    {
        get => _metrics;
        set
        {
            _metrics = value ?? Array.Empty<WorldMetricPoint>();
            _hoverIndex = -1;
            _toolTip.Hide(this);
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int DaysPerYear
    {
        get => _daysPerYear;
        set => _daysPerYear = Math.Max(1, value);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        eventArgs.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var bounds = Rectangle.Inflate(ClientRectangle, -Padding.Left, -Padding.Top);
        _populationPlot = Rectangle.Empty;
        _agePlot = Rectangle.Empty;
        if (bounds.Width < 220 || bounds.Height < 220)
        {
            return;
        }

        if (_metrics.Count == 0)
        {
            TextRenderer.DrawText(eventArgs.Graphics, "統計データなし", Font, bounds, Color.DimGray,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var gap = 12;
        var chartHeight = (bounds.Height - gap) / 2;
        var populationBounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, chartHeight);
        var ageBounds = new Rectangle(bounds.X, bounds.Y + chartHeight + gap, bounds.Width, chartHeight);
        _populationPlot = DrawChart(
            eventArgs.Graphics,
            populationBounds,
            "人口",
            _metrics.Select(item => (double)item.Population).ToArray(),
            Color.FromArgb(38, 112, 196),
            "0");
        _agePlot = DrawChart(
            eventArgs.Graphics,
            ageBounds,
            "平均年齢",
            _metrics.Select(item => item.AverageAgeYears).ToArray(),
            Color.FromArgb(211, 104, 43),
            "0.00");
    }

    private Rectangle DrawChart(
        Graphics graphics,
        Rectangle bounds,
        string title,
        IReadOnlyList<double> values,
        Color lineColor,
        string valueFormat)
    {
        using var cardBrush = new SolidBrush(Color.FromArgb(250, 251, 253));
        using var borderPen = new Pen(Color.FromArgb(208, 214, 222));
        using var gridPen = new Pen(Color.FromArgb(226, 231, 237));
        using var verticalGridPen = new Pen(Color.FromArgb(238, 241, 245));
        using var baselinePen = new Pen(Color.FromArgb(130, lineColor), 1f) { DashStyle = DashStyle.Dash };
        using var linePen = new Pen(lineColor, 2.3f) { LineJoin = LineJoin.Round };
        using var areaBrush = new SolidBrush(Color.FromArgb(34, lineColor));
        using var pointBrush = new SolidBrush(lineColor);
        using var currentValueBrush = new SolidBrush(lineColor);
        using var textBrush = new SolidBrush(Color.FromArgb(50, 57, 66));
        using var mutedBrush = new SolidBrush(Color.FromArgb(105, 113, 124));
        using var titleFont = new Font(Font.FontFamily, Font.Size + 0.5f, FontStyle.Bold);
        using var valueFont = new Font(Font.FontFamily, Font.Size + 2.5f, FontStyle.Bold);

        graphics.FillRectangle(cardBrush, bounds);
        graphics.DrawRectangle(borderPen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);

        var current = values[^1];
        var start = values[0];
        var observedMinimum = values.Min();
        var observedMaximum = values.Max();
        var totalDelta = current - start;
        var percent = Math.Abs(start) < 1e-12 ? 0 : totalDelta / start;
        graphics.DrawString(title, titleFont, textBrush, bounds.X + 8, bounds.Y + 5);
        graphics.DrawString(current.ToString(valueFormat, CultureInfo.InvariantCulture), valueFont,
            currentValueBrush, bounds.X + 82, bounds.Y + 1);
        var deltaText = $"開始比 {FormatSigned(totalDelta, valueFormat)} ({percent:+0.0%;-0.0%;0.0%})";
        graphics.DrawString(deltaText, Font, totalDelta switch
        {
            > 0 => Brushes.SeaGreen,
            < 0 => Brushes.Firebrick,
            _ => mutedBrush
        }, bounds.X + 168, bounds.Y + 7);
        graphics.DrawString(
            $"範囲 {observedMinimum.ToString(valueFormat, CultureInfo.InvariantCulture)}–" +
            observedMaximum.ToString(valueFormat, CultureInfo.InvariantCulture),
            Font, mutedBrush, bounds.X + 8, bounds.Y + 27);

        var plot = new Rectangle(
            bounds.X + 54,
            bounds.Y + 48,
            Math.Max(1, bounds.Width - 66),
            Math.Max(1, bounds.Height - 72));
        var span = observedMaximum - observedMinimum;
        var padding = span < 1e-9 ? Math.Max(Math.Abs(observedMaximum) * 0.05, title == "人口" ? 1 : 0.05) : span * 0.10;
        var scaleMinimum = Math.Max(0, observedMinimum - padding);
        var scaleMaximum = observedMaximum + padding;
        if (scaleMaximum - scaleMinimum < 1e-9)
        {
            scaleMaximum = scaleMinimum + 1;
        }

        for (var index = 0; index <= 4; index++)
        {
            var y = plot.Top + index * plot.Height / 4f;
            graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            var labelValue = scaleMaximum - index * (scaleMaximum - scaleMinimum) / 4;
            var label = labelValue.ToString(valueFormat, CultureInfo.InvariantCulture);
            var labelSize = graphics.MeasureString(label, Font);
            graphics.DrawString(label, Font, mutedBrush, plot.Left - labelSize.Width - 5, y - labelSize.Height / 2);
        }

        for (var index = 0; index <= 4; index++)
        {
            var x = plot.Left + index * plot.Width / 4f;
            graphics.DrawLine(verticalGridPen, x, plot.Top, x, plot.Bottom);
        }

        graphics.DrawRectangle(borderPen, plot);
        var points = values.Select((value, index) => new PointF(
            values.Count == 1 ? plot.Left : plot.Left + index * plot.Width / (float)(values.Count - 1),
            plot.Bottom - (float)((value - scaleMinimum) / (scaleMaximum - scaleMinimum) * plot.Height))).ToArray();
        var baselineY = plot.Bottom - (float)((start - scaleMinimum) / (scaleMaximum - scaleMinimum) * plot.Height);
        graphics.DrawLine(baselinePen, plot.Left, baselineY, plot.Right, baselineY);
        if (points.Length == 1)
        {
            graphics.FillEllipse(pointBrush, points[0].X - 3, points[0].Y - 3, 6, 6);
        }
        else
        {
            using var area = new GraphicsPath();
            area.AddLines(points);
            area.AddLine(points[^1].X, points[^1].Y, points[^1].X, plot.Bottom);
            area.AddLine(points[^1].X, plot.Bottom, points[0].X, plot.Bottom);
            area.CloseFigure();
            graphics.FillPath(areaBrush, area);
            graphics.DrawLines(linePen, points);
        }

        var currentPoint = points[^1];
        graphics.FillEllipse(Brushes.White, currentPoint.X - 4, currentPoint.Y - 4, 8, 8);
        graphics.DrawEllipse(linePen, currentPoint.X - 4, currentPoint.Y - 4, 8, 8);
        if (_hoverIndex >= 0 && _hoverIndex < points.Length)
        {
            var hover = points[_hoverIndex];
            using var hoverPen = new Pen(Color.FromArgb(115, 123, 134), 1f) { DashStyle = DashStyle.Dot };
            graphics.DrawLine(hoverPen, hover.X, plot.Top, hover.X, plot.Bottom);
            graphics.FillEllipse(Brushes.White, hover.X - 4, hover.Y - 4, 8, 8);
            graphics.DrawEllipse(linePen, hover.X - 4, hover.Y - 4, 8, 8);
        }

        var firstLabel = FormatTick(_metrics[0].Tick);
        var lastLabel = FormatTick(_metrics[^1].Tick);
        graphics.DrawString(firstLabel, Font, mutedBrush, plot.Left, plot.Bottom + 3);
        var lastSize = graphics.MeasureString(lastLabel, Font);
        graphics.DrawString(lastLabel, Font, mutedBrush, plot.Right - lastSize.Width, plot.Bottom + 3);
        return plot;
    }

    private void HandleMouseMove(object? sender, MouseEventArgs eventArgs)
    {
        var plot = _populationPlot.Contains(eventArgs.Location)
            ? _populationPlot
            : _agePlot.Contains(eventArgs.Location) ? _agePlot : Rectangle.Empty;
        if (plot.IsEmpty || _metrics.Count == 0)
        {
            ClearHover();
            return;
        }

        var ratio = plot.Width <= 1 ? 0 : (eventArgs.X - plot.Left) / (double)plot.Width;
        var index = Math.Clamp((int)Math.Round(ratio * (_metrics.Count - 1)), 0, _metrics.Count - 1);
        if (index == _hoverIndex)
        {
            return;
        }

        _hoverIndex = index;
        Cursor = Cursors.Cross;
        var point = _metrics[index];
        var previous = index > 0 ? _metrics[index - 1] : point;
        var text = string.Join(Environment.NewLine,
            FormatTick(point.Tick),
            $"人口 {point.Population:N0}  日次 {point.Population - previous.Population:+#;-#;0}",
            $"平均年齢 {point.AverageAgeYears:0.00}年  日次 " +
            $"{point.AverageAgeYears - previous.AverageAgeYears:+0.00;-0.00;0.00}");
        _toolTip.Show(text, this, eventArgs.X + 14, eventArgs.Y + 16, 8_000);
        Invalidate();
    }

    private void ClearHover()
    {
        if (_hoverIndex < 0)
        {
            return;
        }

        _hoverIndex = -1;
        Cursor = Cursors.Default;
        _toolTip.Hide(this);
        Invalidate();
    }

    private string FormatTick(int tick) =>
        $"{tick / _daysPerYear}年 {tick % _daysPerYear + 1}日";

    private static string FormatSigned(double value, string format) => value switch
    {
        > 0 => $"+{value.ToString(format, CultureInfo.InvariantCulture)}",
        < 0 => value.ToString(format, CultureInfo.InvariantCulture),
        _ => value.ToString(format, CultureInfo.InvariantCulture)
    };
}
