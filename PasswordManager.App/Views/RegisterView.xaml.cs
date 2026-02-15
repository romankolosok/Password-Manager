using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using PasswordManager.App.ViewModels;

namespace PasswordManager.App.Views
{
    public partial class RegisterView : Window
    {
        private bool _syncingPassword;
        private bool _syncingConfirm;

        public RegisterView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is RegisterViewModel oldVm)
                oldVm.RegisterSuccessful -= OnRegisterSuccessful;
            if (e.NewValue is RegisterViewModel newVm)
                newVm.RegisterSuccessful += OnRegisterSuccessful;
        }

        private void OnRegisterSuccessful(object? sender, EventArgs e)
        {
            Coordinator?.OnRegisterSuccess((Window)this);
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!_syncingPassword)
            {
                _syncingPassword = true;
                PasswordVisibleBox.Text = PasswordBox.Password;
                if (DataContext is RegisterViewModel vm)
                    vm.MasterPassword = PasswordBox.Password;
                _syncingPassword = false;
            }
        }

        private void PasswordVisibleBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_syncingPassword)
            {
                _syncingPassword = true;
                PasswordBox.Password = PasswordVisibleBox.Text;
                if (DataContext is RegisterViewModel vm)
                    vm.MasterPassword = PasswordVisibleBox.Text;
                _syncingPassword = false;
            }
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!_syncingConfirm)
            {
                _syncingConfirm = true;
                ConfirmVisibleBox.Text = ConfirmPasswordBox.Password;
                if (DataContext is RegisterViewModel vm)
                    vm.ConfirmPassword = ConfirmPasswordBox.Password;
                _syncingConfirm = false;
            }
        }

        private void ConfirmVisibleBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_syncingConfirm)
            {
                _syncingConfirm = true;
                ConfirmPasswordBox.Password = ConfirmVisibleBox.Text;
                if (DataContext is RegisterViewModel vm)
                    vm.ConfirmPassword = ConfirmVisibleBox.Text;
                _syncingConfirm = false;
            }
        }

        private void PeekPassword_Checked(object sender, RoutedEventArgs e)
        {
            PasswordBox.Visibility = Visibility.Collapsed;
            PasswordVisibleBox.Visibility = Visibility.Visible;
            PasswordVisibleBox.Focus();
            PasswordVisibleBox.CaretIndex = PasswordVisibleBox.Text.Length;
        }

        private void PeekPassword_Unchecked(object sender, RoutedEventArgs e)
        {
            PasswordVisibleBox.Visibility = Visibility.Collapsed;
            PasswordBox.Visibility = Visibility.Visible;
            PasswordBox.Focus();
        }

        private void PeekConfirm_Checked(object sender, RoutedEventArgs e)
        {
            ConfirmPasswordBox.Visibility = Visibility.Collapsed;
            ConfirmVisibleBox.Visibility = Visibility.Visible;
            ConfirmVisibleBox.Focus();
            ConfirmVisibleBox.CaretIndex = ConfirmVisibleBox.Text.Length;
        }

        private void PeekConfirm_Unchecked(object sender, RoutedEventArgs e)
        {
            ConfirmVisibleBox.Visibility = Visibility.Collapsed;
            ConfirmPasswordBox.Visibility = Visibility.Visible;
            ConfirmPasswordBox.Focus();
        }

        private void PasswordVisibleBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (IsFocusWithin(PeekPassword)) return;
            if (PeekPassword.IsChecked == true)
                PeekPassword.IsChecked = false;
        }

        private void ConfirmVisibleBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (IsFocusWithin(PeekConfirm)) return;
            if (PeekConfirm.IsChecked == true)
                PeekConfirm.IsChecked = false;
        }

        private static bool IsFocusWithin(ToggleButton button)
        {
            var window = Window.GetWindow(button);
            var focused = FocusManager.GetFocusedElement(window) as DependencyObject;
            while (focused != null)
            {
                if (focused == button) return true;
                focused = VisualTreeHelper.GetParent(focused);
            }
            return false;
        }

        public Services.IAuthCoordinator? Coordinator { get; set; }

        private void BackToLogin_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
