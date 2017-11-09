namespace LostTech.Stack.DataBinding
{
    using System;
    using System.Globalization;
    using System.Windows;
    using ValueConverters;

    public sealed class UniformThicknessConverter: ConverterBase
    {
        protected override object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (targetType != typeof(Thickness))
                throw new NotSupportedException($"Can only convert to {typeof(Thickness).FullName}");
            
            if (value == null)
                return new Thickness();

            if (value is double d)
                return new Thickness(d);

            if (value is float f)
                return new Thickness(f);
            if (value is int i)
                return new Thickness(i);
            
            throw new NotSupportedException("Value is not of supported type");
        }
    }
}
