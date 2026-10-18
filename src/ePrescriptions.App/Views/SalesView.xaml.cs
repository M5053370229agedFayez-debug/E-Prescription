using System.Windows.Controls;

namespace ePrescriptions.App.Views
{
    public partial class SalesView : UserControl
    {
        public SalesView()
        {
            InitializeComponent();
            DataContextChanged += (s, e) =>
            {
                if (e.NewValue is ViewModels.SalesViewModel vm)
                {
                    // لما الـ ViewModel يمسح كلمة الموافقة بعد المحاولة، بنفضّي الخانة كمان
                    vm.PropertyChanged += (_, args) =>
                    {
                        if (args.PropertyName == nameof(ViewModels.SalesViewModel.ApproverPassword) && vm.ApproverPassword.Length == 0)
                            ApproverPasswordBox.Clear();
                    };
                }
            };
        }

        // PasswordBox لا يدعم الربط المباشر: بننقل الكلمة للـ ViewModel أول بأول (ما بتتخزّن، وبتتمسح بعد كل محاولة)
        private void ApproverPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ViewModels.SalesViewModel vm && sender is PasswordBox box)
            {
                vm.ApproverPassword = box.Password;
            }
        }

        // ===== اقتراحات أسماء العملاء =====

        // لما المستخدم يدخل خانة اسم العميل: نحدّث قايمة العملاء من القاعدة (عشان العملاء الجدد يظهروا)
        private void CustomerNameBox_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            if (DataContext is ViewModels.SalesViewModel vm) vm.RefreshCustomersList();
        }

        // الاقتراحات بتظهر بس لما المستخدم هو اللي بيكتب (مش لما البرنامج يغيّر الاسم بنفسه، زي فاتورة جديدة)
        private void CustomerNameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb && tb.IsKeyboardFocusWithin && DataContext is ViewModels.SalesViewModel vm)
                vm.UpdateCustomerSuggestions(tb.Text);
        }

        private void CustomerNameBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!(DataContext is ViewModels.SalesViewModel vm) || !vm.IsCustomerSuggestionsOpen) return;

            var list = CustomerSuggestionsList;
            switch (e.Key)
            {
                case System.Windows.Input.Key.Down:
                    list.SelectedIndex = System.Math.Min(list.SelectedIndex + 1, list.Items.Count - 1);
                    list.ScrollIntoView(list.SelectedItem);
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.Up:
                    list.SelectedIndex = System.Math.Max(list.SelectedIndex - 1, 0);
                    list.ScrollIntoView(list.SelectedItem);
                    e.Handled = true;
                    break;
                case System.Windows.Input.Key.Return: // Key.Enter هو نفس القيمة (alias) فمينفعش يتكتب كـ case تاني
                    if (list.SelectedItem is Domain.Entities.Customer chosen)
                    {
                        vm.SelectCustomerSuggestion(chosen);
                        ((TextBox)sender).CaretIndex = ((TextBox)sender).Text.Length;
                        e.Handled = true;
                    }
                    break;
                case System.Windows.Input.Key.Escape:
                    vm.IsCustomerSuggestionsOpen = false;
                    e.Handled = true;
                    break;
            }
        }

        private void CustomerSuggestionsList_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var node = e.OriginalSource as System.Windows.DependencyObject;
            while (node != null && !(node is ListBoxItem))
            {
                node = (node is System.Windows.Media.Visual || node is System.Windows.Media.Media3D.Visual3D)
                    ? System.Windows.Media.VisualTreeHelper.GetParent(node)
                    : System.Windows.LogicalTreeHelper.GetParent(node);
            }

            if (node is ListBoxItem item && item.DataContext is Domain.Entities.Customer customer
                && DataContext is ViewModels.SalesViewModel vm)
            {
                vm.SelectCustomerSuggestion(customer);
                CustomerNameBox.CaretIndex = CustomerNameBox.Text.Length;
                e.Handled = true;
            }
        }

        private void SearchInputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Return || e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Tab)
            {
                if (DataContext is ViewModels.SalesViewModel vm)
                {
                    vm.HandleBarcodeEnter();
                    e.Handled = true;
                    if (sender is TextBox tb)
                    {
                        tb.Focus();
                        tb.SelectAll();
                    }
                }
            }
        }

        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.Key == System.Windows.Input.Key.F4)
            {
                if (DataContext is ViewModels.SalesViewModel vm)
                {
                    vm.OpenItemSearch();
                    SearchInputBox?.Focus();
                    SearchInputBox?.SelectAll();
                    e.Handled = true;
                }
            }
        }
    }
}
