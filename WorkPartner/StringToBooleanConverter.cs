using System;
using System.Globalization;
using System.Windows.Data;

namespace WorkPartner
{
    public class StringToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString().Equals(parameter?.ToString(), StringComparison.InvariantCultureIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? parameter : Binding.DoNothing;
        }
    }
}