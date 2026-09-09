namespace Ledge.App.Controls;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

public sealed class EnumToListConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Enum enumValue)
        {
            return Enum.GetValues(enumValue.GetType());
        }
        return new List<object>();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}