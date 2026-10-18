using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ePrescriptions.App.Views
{
    /// <summary>
    /// شريط اختصارات لوحة المفاتيح السفلي (يختفي ويظهر بالماوس). مكوّن مشترك بين النافذة الرئيسية والنوافذ المستقلة.
    /// Target = الـ ViewModel اللي الأزرار بتنفذ أوامره (CurrentView في الرئيسية، أو CurrentViewModel في المستقلة).
    /// شارات المفاتيح للعرض فقط؛ الاختصارات الفعلية هي InputBindings في النافذة.
    /// </summary>
    public partial class ShortcutsBar : UserControl
    {
        public static readonly DependencyProperty TargetProperty =
            DependencyProperty.Register(nameof(Target), typeof(object), typeof(ShortcutsBar), new PropertyMetadata(null));

        public object? Target
        {
            get => GetValue(TargetProperty);
            set => SetValue(TargetProperty, value);
        }

        // بعد ~300ms من خروج الماوس من الشريط واللوحة معاً بتتقفل اللوحة (عشان ما تتقفلش بالغلط لو الماوس خرج لحظة)
        private readonly DispatcherTimer _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        private bool _isOpen;

        public ShortcutsBar()
        {
            InitializeComponent();
            _closeTimer.Tick += (_, __) =>
            {
                _closeTimer.Stop();
                AnimatePanel(open: false);
            };
        }

        private void Shortcuts_MouseEnter(object sender, MouseEventArgs e)
        {
            _closeTimer.Stop();
            AnimatePanel(open: true);
        }

        private void Shortcuts_MouseLeave(object sender, MouseEventArgs e)
        {
            _closeTimer.Stop();
            _closeTimer.Start();
        }

        /// <summary>أنيميشن على الارتفاع (من 0 للارتفاع الكامل أو العكس) في حوالي 200ms.</summary>
        private void AnimatePanel(bool open)
        {
            if (open == _isOpen) return;
            _isOpen = open;

            double target = 0;
            if (open)
            {
                // الارتفاع الكامل = ارتفاع المحتوى لما يتلف على عرض النافذة
                double width = Math.Max(1, Overlay.ActualWidth);
                ShortcutsPanelContent.Measure(new Size(width, double.PositiveInfinity));
                target = ShortcutsPanelContent.DesiredSize.Height + 1; // +1 لخط الحد العلوي
            }

            var anim = new DoubleAnimation
            {
                From = ShortcutsPanel.ActualHeight,
                To = target,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            ShortcutsPanel.BeginAnimation(FrameworkElement.HeightProperty, anim);
        }
    }
}
