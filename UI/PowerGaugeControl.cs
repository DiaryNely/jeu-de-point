using System.ComponentModel;

namespace jeuPoint.UI;

public sealed class PowerGaugeControl : Control
{
    private int _minPower = 1;
    private int _maxPower = 9;
    private int _power = 5;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int MinPower
    {
        get => _minPower;
        set
        {
            _minPower = value;
            if (_power < _minPower)
            {
                _power = _minPower;
            }

            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int MaxPower
    {
        get => _maxPower;
        set
        {
            _maxPower = value;
            if (_power > _maxPower)
            {
                _power = _maxPower;
            }

            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Power
    {
        get => _power;
        set
        {
            var clamped = Math.Clamp(value, _minPower, _maxPower);
            if (_power == clamped)
            {
                return;
            }

            _power = clamped;
            Invalidate();
        }
    }

    public PowerGaugeControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        Height = 34;
        BackColor = Color.FromArgb(255, 252, 254);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var gaugeRect = new RectangleF(2, 6, Math.Max(20, Width - 4), Math.Max(12, Height - 16));
        var borderRadius = Math.Min(8f, gaugeRect.Height / 2f);

        using var backgroundBrush = new SolidBrush(Color.FromArgb(251, 230, 241));
        using var borderPen = new Pen(Color.FromArgb(224, 170, 200), 1f);

        using (var backgroundPath = RoundedRect(gaugeRect, borderRadius))
        {
            e.Graphics.FillPath(backgroundBrush, backgroundPath);
            e.Graphics.DrawPath(borderPen, backgroundPath);
        }

        var ratio = (_power - _minPower) / (float)Math.Max(1, _maxPower - _minPower);
        var fillWidth = Math.Max(2f, gaugeRect.Width * ratio);
        var fillRect = new RectangleF(gaugeRect.X, gaugeRect.Y, fillWidth, gaugeRect.Height);

        using var fillBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
            fillRect,
            Color.FromArgb(244, 143, 177),
            Color.FromArgb(197, 36, 118),
            System.Drawing.Drawing2D.LinearGradientMode.Horizontal);

        using (var fillPath = RoundedRect(fillRect, borderRadius))
        {
            e.Graphics.FillPath(fillBrush, fillPath);
        }

        var text = $"Puissance {_power}/{_maxPower}";
        using var textFont = new Font("Segoe UI", 8.5f, FontStyle.Bold, GraphicsUnit.Point);
        using var textBrush = new SolidBrush(Color.FromArgb(109, 33, 79));
        e.Graphics.DrawString(text, textFont, textBrush, new RectangleF(0, 0, Width, Height), new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Near
        });
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(RectangleF bounds, float radius)
    {
        var diameter = radius * 2;
        var path = new System.Drawing.Drawing2D.GraphicsPath();

        if (diameter <= 0)
        {
            path.AddRectangle(bounds);
            path.CloseFigure();
            return path;
        }

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }
}
