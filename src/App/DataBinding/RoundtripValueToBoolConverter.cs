namespace LostTech.Stack.DataBinding {
    using System;
    using System.Globalization;
    using System.Windows;
    using ValueConverters;
    public class RoundtripValueToBoolConverter<T>: ValueToBoolConverter<T> {
        /// <summary>
        /// Only used in <see cref="ConvertBack"/>
        /// </summary>
        public virtual T FalseValue {
            get {
                return (T)this.GetValue(FalseValueProperty);
            }
            set {
                this.SetValue(FalseValueProperty, value);
            }
        }

        public static readonly DependencyProperty FalseValueProperty =
            PropertyHelper.Create<T, ValueToBoolConverter<T>>(nameof(FalseValue));

        protected override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            bool isTrue = Equals(value, this.TrueValue);
            return isTrue ? this.TrueValue : this.FalseValue;
        }
    }

    public class RoundtripValueToBoolConverter: RoundtripValueToBoolConverter<object> { }
}
