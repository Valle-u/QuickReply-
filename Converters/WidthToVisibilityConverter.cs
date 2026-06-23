using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace QuickReply.Converters;

/// <summary>
/// Devuelve Visible si el ancho recibido es ≥ al umbral (parámetro); si no, Collapsed.
/// Se usa para ocultar el texto de los botones cuando la ventana se achica (quedan solo íconos).
/// </summary>
public sealed class WidthToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double width = value is double d ? d : 0;
        double threshold = 0;
        if (parameter is string s)
            double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out threshold);

        return width >= threshold ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
