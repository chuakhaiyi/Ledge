namespace Ledge.App.Controls;

using System.Windows.Data;
using Ledge.Core.Models;

public sealed class NoteToDockTabConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}