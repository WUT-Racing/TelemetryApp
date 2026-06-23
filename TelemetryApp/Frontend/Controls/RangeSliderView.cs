using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace TelemetryApp.Frontend.Controls;

public sealed class RangeSliderView : GraphicsView, IDrawable
{
    public static readonly BindableProperty MinimumProperty = BindableProperty.Create(
        nameof(Minimum), typeof(double), typeof(RangeSliderView), 0.0, propertyChanged: OnRangePropertyChanged);

    public static readonly BindableProperty MaximumProperty = BindableProperty.Create(
        nameof(Maximum), typeof(double), typeof(RangeSliderView), 60.0, propertyChanged: OnRangePropertyChanged);

    public static readonly BindableProperty LowerValueProperty = BindableProperty.Create(
        nameof(LowerValue), typeof(double), typeof(RangeSliderView), 0.0, BindingMode.TwoWay, propertyChanged: OnRangePropertyChanged);

    public static readonly BindableProperty UpperValueProperty = BindableProperty.Create(
        nameof(UpperValue), typeof(double), typeof(RangeSliderView), 60.0, BindingMode.TwoWay, propertyChanged: OnRangePropertyChanged);

    private const float ThumbRadius = 10f;
    private const float HorizontalPadding = 18f;
    private bool _dragLower;
    private bool _dragUpper;
    private bool _isCoercing;

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double LowerValue
    {
        get => (double)GetValue(LowerValueProperty);
        set => SetValue(LowerValueProperty, value);
    }

    public double UpperValue
    {
        get => (double)GetValue(UpperValueProperty);
        set => SetValue(UpperValueProperty, value);
    }

    public RangeSliderView()
    {
        Drawable = this;
        HeightRequest = 44;
        BackgroundColor = Colors.Transparent;

        StartInteraction += HandleStartInteraction;
        DragInteraction += HandleDragInteraction;
        EndInteraction += HandleEndInteraction;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var min = Minimum;
        var max = Maximum <= Minimum ? Minimum + 1 : Maximum;
        var lower = Math.Clamp(LowerValue, min, max);
        var upper = Math.Clamp(UpperValue, min, max);

        if (lower > upper)
            (lower, upper) = (upper, lower);

        var y = dirtyRect.Center.Y;
        var x1 = HorizontalPadding;
        var x2 = Math.Max(x1 + 1, dirtyRect.Width - HorizontalPadding);
        var lowerX = ValueToX(lower, min, max, x1, x2);
        var upperX = ValueToX(upper, min, max, x1, x2);

        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var baseTrackColor = isDark ? Color.FromArgb("#334155") : Color.FromArgb("#CBD5E1");
        var selectedTrackColor = isDark ? Color.FromArgb("#38BDF8") : Color.FromArgb("#2563EB");
        var thumbColor = IsEnabled
            ? (isDark ? Color.FromArgb("#E0F2FE") : Color.FromArgb("#FFFFFF"))
            : Color.FromArgb("#94A3B8");
        var thumbStrokeColor = isDark ? Color.FromArgb("#0284C7") : Color.FromArgb("#1D4ED8");

        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeSize = 6;
        canvas.StrokeColor = baseTrackColor.WithAlpha(IsEnabled ? 0.95f : 0.45f);
        canvas.DrawLine(x1, y, x2, y);

        canvas.StrokeSize = 7;
        canvas.StrokeColor = selectedTrackColor.WithAlpha(IsEnabled ? 0.95f : 0.45f);
        canvas.DrawLine(lowerX, y, upperX, y);

        canvas.FillColor = thumbColor;
        canvas.StrokeSize = 2;
        canvas.StrokeColor = thumbStrokeColor.WithAlpha(IsEnabled ? 1.0f : 0.45f);
        canvas.FillCircle(lowerX, y, ThumbRadius);
        canvas.DrawCircle(lowerX, y, ThumbRadius);
        canvas.FillCircle(upperX, y, ThumbRadius);
        canvas.DrawCircle(upperX, y, ThumbRadius);
    }

    private static void OnRangePropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (RangeSliderView)bindable;
        control.CoerceValues();
        control.Invalidate();
    }

    private void CoerceValues()
    {
        if (_isCoercing)
            return;

        _isCoercing = true;
        try
        {
            var min = Minimum;
            var max = Maximum <= Minimum ? Minimum + 1 : Maximum;
            var lower = Math.Clamp(LowerValue, min, max);
            var upper = Math.Clamp(UpperValue, min, max);

            if (lower > upper)
            {
                var middle = (lower + upper) / 2.0;
                lower = middle;
                upper = middle;
            }

            if (Math.Abs(LowerValue - lower) > 0.0001)
                LowerValue = lower;

            if (Math.Abs(UpperValue - upper) > 0.0001)
                UpperValue = upper;
        }
        finally
        {
            _isCoercing = false;
        }
    }

    private void HandleStartInteraction(object? sender, TouchEventArgs e)
    {
        if (!IsEnabled || !e.Touches.Any())
            return;

        var point = e.Touches.First();
        var width = (float)Math.Max(1, Width);
        var min = Minimum;
        var max = Maximum <= Minimum ? Minimum + 1 : Maximum;
        var lowerX = ValueToX(LowerValue, min, max, HorizontalPadding, width - HorizontalPadding);
        var upperX = ValueToX(UpperValue, min, max, HorizontalPadding, width - HorizontalPadding);

        _dragLower = Math.Abs(point.X - lowerX) <= Math.Abs(point.X - upperX);
        _dragUpper = !_dragLower;

        UpdateDraggedThumb(point.X);
    }

    private void HandleDragInteraction(object? sender, TouchEventArgs e)
    {
        if (!IsEnabled || !e.Touches.Any())
            return;

        UpdateDraggedThumb(e.Touches.First().X);
    }

    private void HandleEndInteraction(object? sender, TouchEventArgs e)
    {
        _dragLower = false;
        _dragUpper = false;
    }

    private void UpdateDraggedThumb(float x)
    {
        var width = (float)Math.Max(1, Width);
        var min = Minimum;
        var max = Maximum <= Minimum ? Minimum + 1 : Maximum;
        var value = XToValue(x, min, max, HorizontalPadding, width - HorizontalPadding);

        if (_dragLower)
        {
            LowerValue = Math.Min(value, UpperValue);
        }
        else if (_dragUpper)
        {
            UpperValue = Math.Max(value, LowerValue);
        }
    }

    private static float ValueToX(double value, double min, double max, float x1, float x2)
    {
        var ratio = (value - min) / (max - min);
        ratio = Math.Clamp(ratio, 0, 1);
        return x1 + (float)ratio * (x2 - x1);
    }

    private static double XToValue(float x, double min, double max, float x1, float x2)
    {
        if (x2 <= x1)
            return min;

        var ratio = Math.Clamp((x - x1) / (x2 - x1), 0, 1);
        return min + ratio * (max - min);
    }
}
