using System;
using System.Windows;
using System.Windows.Media;

namespace ePrescriptions.App
{
    /// <summary>
    /// تصغير واجهة النافذة تلقائياً على الشاشات الصغيرة (أو اللي عليها تكبير DPI) عشان العناصر ما تاخدش الشاشة كلها
    /// والجداول يبان فيها اسم الصنف. على الشاشات 1366 عرض فأكتر الحجم بيفضل 100%.
    /// </summary>
    public static class UiScale
    {
        /// <summary>عرض الشاشة (بوحدات WPF) اللي الواجهة مصممة له بحجمها الطبيعي.</summary>
        public const double ReferenceWidth = 1366.0;

        /// <summary>أقل نسبة تصغير مسموحة (أقل من كده الخط بيبقى صغير جداً).</summary>
        public const double MinScale = 0.8;

        public static double Compute(double workAreaWidth)
        {
            if (workAreaWidth <= 0 || double.IsNaN(workAreaWidth) || double.IsInfinity(workAreaWidth)) return 1.0;
            double s = workAreaWidth / ReferenceWidth;
            if (s > 1.0) s = 1.0;
            if (s < MinScale) s = MinScale;
            return s;
        }

        public static void Apply(Window window)
        {
            var root = window.Content as FrameworkElement;
            if (root == null) return;

            double scale = Compute(SystemParameters.WorkArea.Width);
            if (Math.Abs(scale - 1.0) < 0.01) return;

            root.LayoutTransform = new ScaleTransform(scale, scale);
        }
    }
}
