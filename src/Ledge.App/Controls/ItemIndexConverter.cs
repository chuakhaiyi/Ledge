namespace Ledge.App.Controls;

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

public sealed class ItemIndexConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is System.Collections.IList list && parameter is FrameworkElement element)
        {
            return list.IndexOf(element.DataContext);
        }
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}