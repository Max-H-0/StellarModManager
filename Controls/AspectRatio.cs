using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace StellarModManager.Controls;

public class AspectRatio : Decorator
{
    public static readonly StyledProperty<double> WidthParameterProperty =
        AvaloniaProperty.Register<AspectRatio, double>(nameof(WidthParameter), 16.0);
        
    public static readonly StyledProperty<double> HeightParameterProperty =
        AvaloniaProperty.Register<AspectRatio, double>(nameof(HeightParameter), 9.0);


    public double WidthParameter
    {
        get => GetValue(WidthParameterProperty);
        set => SetValue(WidthParameterProperty, value);
    }

    public double HeightParameter
    {
        get => GetValue(HeightParameterProperty);
        set => SetValue(HeightParameterProperty, value);
    }

    public Size Aspect => new(WidthParameter, HeightParameter);


    protected override Size MeasureOverride(Size availableSize)
    {
        var size = Stretch.Uniform.CalculateSize
        (
            destinationSize: availableSize,
            sourceSize: Aspect
        );
        Child?.Measure(size);

        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = Stretch.Uniform.CalculateSize
        (
            destinationSize: finalSize,
            sourceSize: Aspect
        );
        Child?.Arrange(new Rect(size));

        return size;
    }
}