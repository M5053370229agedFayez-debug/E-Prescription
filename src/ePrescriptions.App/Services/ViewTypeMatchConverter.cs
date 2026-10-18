using System;
using System.Globalization;
using System.Windows.Data;

namespace ePrescriptions.App.Services
{
    /// <summary>
    /// بيرجّع true لو نوع الشاشة الحالية (CurrentView) اسمه ضمن القائمة اللي في ConverterParameter
    /// (أسماء مفصولة بـ | مثل "SalesViewModel|PurchasesViewModel"). بنستخدمه لتمييز التصنيف والعنصر الحالي في القائمة العلوية.
    /// </summary>
    public class ViewTypeMatchConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || !(parameter is string names)) return false;
            string current = value.GetType().Name;
            foreach (var n in names.Split('|'))
            {
                if (string.Equals(n.Trim(), current, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
