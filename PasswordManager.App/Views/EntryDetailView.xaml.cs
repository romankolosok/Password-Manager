using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace PasswordManager.App.Views
{
    public partial class EntryDetailView : UserControl
    {
        private bool _isSyncing;

        public EntryDetailView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.EntryDetailViewModel vm)
            {
                vm.PropertyChanged += OnViewModelPropertyChanged;
                HiddenPasswordBox.Password = vm.Password ?? string.Empty;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ViewModels.EntryDetailViewModel.Password) || _isSyncing)
                return;
            if (DataContext is ViewModels.EntryDetailViewModel vm)
            {
                _isSyncing = true;
                var p = vm.Password ?? string.Empty;
                HiddenPasswordBox.Password = p;
                VisiblePasswordBox.Text = p;
                _isSyncing = false;
            }
        }

        private void HiddenPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isSyncing) return;
            _isSyncing = true;
            if (DataContext is ViewModels.EntryDetailViewModel vm)
                vm.Password = HiddenPasswordBox.Password;
            _isSyncing = false;
        }

        private void VisiblePasswordBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncing) return;
            _isSyncing = true;
            HiddenPasswordBox.Password = VisiblePasswordBox.Text;
            _isSyncing = false;
        }
    }
}
