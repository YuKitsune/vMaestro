﻿using System.Globalization;
using System.Windows.Data;

namespace TFMS.Wpf.Converters;

[ValueConversion(typeof(DateTimeOffset), typeof(string))]
[ValueConversion(typeof(TimeSpan), typeof(string))]
class MinutesConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeSpan timeSpan)
        {
            return timeSpan.Minutes.ToString("00");
        }

        if (value is DateTimeOffset dateTime)
        {
            return dateTime.Minute.ToString("00");
        }

        throw new NotSupportedException();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
