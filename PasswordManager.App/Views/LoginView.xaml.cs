using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using PasswordManager.App.Services;
using PasswordManager.App.ViewModels;

namespace PasswordManager.App.Views
{
    /// <summary>
    /// Interaction logic for LoginView.xaml
    /// </summary>
    public partial class LoginView : Window
    {
        private bool _isSyncing = false;

        public IAuthCoordinator? Coordinator { get; set; }

        public LoginView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is LoginViewModel oldVm)
                oldVm.LoginSuccessful -= OnLoginSuccessful;
            if (e.NewValue is LoginViewModel newVm)
                newVm.LoginSuccessful += OnLoginSuccessful;
        }

        private void OnLoginSuccessful(object? sender, EventArgs e)
        {
            Coordinator?.OnLoginSuccess((Window)this);
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            Coordinator?.RequestRegister((Window)this);
        }

        private void HiddenPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!_isSyncing)
            {
                _isSyncing = true;
                VisibleTextBox.Text = HiddenPasswordBox.Password;
                if (DataContext is LoginViewModel vm)
                    vm.MasterPassword = HiddenPasswordBox.Password;
                _isSyncing = false;
            }
        }

        private void VisibleTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isSyncing)
            {
                _isSyncing = true;
                HiddenPasswordBox.Password = VisibleTextBox.Text;
                if (DataContext is LoginViewModel vm)
                    vm.MasterPassword = VisibleTextBox.Text;
                _isSyncing = false;
            }
        }

        private void PeekButton_Checked(object sender, RoutedEventArgs e)
        {
            HiddenPasswordBox.Visibility = Visibility.Collapsed;
            VisibleTextBox.Visibility = Visibility.Visible;

            // Move focus to the visible box and put the cursor at the end
            VisibleTextBox.Focus();
            VisibleTextBox.CaretIndex = VisibleTextBox.Text.Length;
        }

        private void PeekButton_Unchecked(object sender, RoutedEventArgs e)
        {
            VisibleTextBox.Visibility = Visibility.Collapsed;
            HiddenPasswordBox.Visibility = Visibility.Visible;

            // Move focus back to the hidden password box
            HiddenPasswordBox.Focus();
        }

        private void VisibleTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Don't uncheck when focus moved to the peek button—let the button click handle toggling
            if (IsFocusWithin(PeekButton))
                return;
            if (PeekButton.IsChecked == true)
                PeekButton.IsChecked = false;
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
    }
}
