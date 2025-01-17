﻿using System;
using Microsoft.UI.Xaml.Data;

namespace ClipboardCanvas.ValueConverters
{
    public class BooleanInvertConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not bool boolVal)
            {
                return false;
            }

            if (parameter is string strParam && strParam.ToLower() == "invert") // For debugging purposes
            {
                return boolVal;
            }

            return !boolVal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
