using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace TouchChan.WinUI.Converters;

partial class HalfValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double number)
        {
            return new CornerRadius(number / 2);
        }

        throw new InvalidOperationException();
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new InvalidOperationException();
}
