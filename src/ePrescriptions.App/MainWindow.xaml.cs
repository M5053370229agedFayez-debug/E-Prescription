using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ePrescriptions.App.Services;
using ePrescriptions.App.ViewModels;

namespace ePrescriptions.App
{
    public partial class MainWindow : Window
    {
        private readonly IdleLockMonitor? _idleMonitor;
        private readonly DispatcherTimer? _idleTimer;

        // ===== القائمة العلوية بالتصنيفات =====
        private readonly DispatcherTimer _menuOpenTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        private readonly DispatcherTimer _menuCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        private readonly Dictionary<Button, Popup> _menuMap = new Dictionary<Button, Popup>();
        private Popup? _openPopup;
        private Popup? _pendingPopup;

        public MainWindow(MainViewModel viewModel, IdleLockMonitor? idleMonitor = null)
        {
            InitializeComponent();
            DataContext = viewModel;
            UiScale.Apply(this);

            InitMainMenu();

            // الحجم ما يزيدش عن مساحة الشاشة المتاحة (بدون شريط المهام) عشان الأطراف والأزرار ما تتقطعش
            var work = SystemParameters.WorkArea;
            Width = System.Math.Min(Width, work.Width);
            Height = System.Math.Min(Height, work.Height);
            MinWidth = System.Math.Min(MinWidth, work.Width);
            MinHeight = System.Math.Min(MinHeight, work.Height);
            WindowState = WindowState.Maximized;

            // قفل تلقائي بعد خمول: أي حركة ماوس/كيبورد/ضغط بتصفّر العدّاد
            _idleMonitor = idleMonitor;
            if (_idleMonitor != null)
            {
                PreviewMouseMove += (_, __) => _idleMonitor.NotifyActivity();
                PreviewMouseDown += (_, __) => _idleMonitor.NotifyActivity();
                PreviewKeyDown += (_, __) => _idleMonitor.NotifyActivity();

                _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
                _idleTimer.Tick += (_, __) => _idleMonitor.Tick();
                _idleTimer.Start();
            }
        }

        // ================= القائمة العلوية بالتصنيفات =================

        private void InitMainMenu()
        {
            _menuMap[CatTrade] = PopTrade;
            _menuMap[CatStock] = PopStock;
            _menuMap[CatAccounts] = PopAccounts;
            _menuMap[CatAdmin] = PopAdmin;

            // تفتح بعد ~150ms من وقوف الماوس على الزر، وتتقفل بعد ~250ms من خروجه من الزر والقائمة معاً
            _menuOpenTimer.Tick += (_, __) =>
            {
                _menuOpenTimer.Stop();
                if (_pendingPopup != null) OpenMenu(_pendingPopup);
            };
            _menuCloseTimer.Tick += (_, __) =>
            {
                _menuCloseTimer.Stop();
                CloseMenus();
            };

            // Esc يقفل القائمة، والضغط خارجها أو تغيير النافذة النشطة يقفلها
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape && _openPopup != null)
                {
                    CloseMenus();
                    e.Handled = true;
                }
            };
            PreviewMouseDown += (s, e) =>
            {
                if (_openPopup != null && !IsInsideMenu(e.OriginalSource as DependencyObject))
                    CloseMenus();
            };
            Deactivated += (_, __) => CloseMenus();
        }

        private void OpenMenu(Popup popup)
        {
            _menuCloseTimer.Stop();
            _menuOpenTimer.Stop();
            _pendingPopup = null;
            if (ReferenceEquals(_openPopup, popup)) return;

            // قايمة واحدة بس مفتوحة في نفس الوقت
            if (_openPopup != null) _openPopup.IsOpen = false;
            popup.IsOpen = true;
            _openPopup = popup;
        }

        private void CloseMenus()
        {
            _menuOpenTimer.Stop();
            _menuCloseTimer.Stop();
            _pendingPopup = null;
            foreach (var p in _menuMap.Values) p.IsOpen = false;
            _openPopup = null;
        }

        private bool IsInsideMenu(DependencyObject? node)
        {
            while (node != null)
            {
                if (node is Button b && _menuMap.ContainsKey(b)) return true;
                if (node is Popup) return true;
                if (node is Border br && br.Style == FindResource("MenuPopupBorder")) return true;

                DependencyObject? parent = null;
                if (node is Visual || node is System.Windows.Media.Media3D.Visual3D)
                    parent = VisualTreeHelper.GetParent(node);
                if (parent == null)
                    parent = LogicalTreeHelper.GetParent(node);
                node = parent;
            }
            return false;
        }

        private void Category_MouseEnter(object sender, MouseEventArgs e)
        {
            _menuCloseTimer.Stop();
            if (!(sender is Button btn) || !_menuMap.TryGetValue(btn, out var popup)) return;

            if (_openPopup != null)
            {
                // فيه قايمة مفتوحة: التبديل فوري
                OpenMenu(popup);
            }
            else
            {
                _pendingPopup = popup;
                _menuOpenTimer.Stop();
                _menuOpenTimer.Start();
            }
        }

        private void Category_MouseLeave(object sender, MouseEventArgs e)
        {
            _menuOpenTimer.Stop();
            _pendingPopup = null;
            if (_openPopup != null)
            {
                _menuCloseTimer.Stop();
                _menuCloseTimer.Start();
            }
        }

        private void Category_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && _menuMap.TryGetValue(btn, out var popup))
                OpenMenu(popup);
        }

        private void PopupContent_MouseEnter(object sender, MouseEventArgs e)
        {
            _menuCloseTimer.Stop();
        }

        private void PopupContent_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_openPopup != null)
            {
                _menuCloseTimer.Stop();
                _menuCloseTimer.Start();
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            // الأمر نفسه بيتنفذ من الـ Command؛ هنا بنقفل القايمة بس
            CloseMenus();
        }
    }
}
