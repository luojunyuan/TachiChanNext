using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace TouchChan.WinUI;

partial class TouchLayerMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double number && parameter is string factorString && TryParseFraction(factorString, out double factor))
        {
            return new Thickness(number * factor);
        }

        throw new InvalidOperationException();
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new InvalidOperationException();

    private static bool TryParseFraction(string fraction, out double factor)
    {
        string[] parts = fraction.Split('/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], out double numerator) &&
            double.TryParse(parts[1], out double denominator))
        {
            factor = numerator / denominator;
            return true;
        }

        throw new InvalidCastException();
    }
}
