using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
            DataObject.AddPastingHandler(LengthBox, OnLengthBoxPasting);
        }

        private void LengthBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox box) return;
            string proposed = box.Text.Remove(box.SelectionStart, box.SelectionLength) + e.Text;
            e.Handled = proposed.Length > 0 && !proposed.All(char.IsDigit);
        }

        private void OnLengthBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                string? text = e.DataObject.GetData(DataFormats.Text) as string;
                if (text != null)
                {
                    string digitsOnly = new string(text.Where(char.IsDigit).ToArray());
                    if (digitsOnly != text)
                    {
                        e.CancelCommand();
                        if (sender is TextBox box)
                        {
                            int start = box.SelectionStart;
                            string current = box.Text.Remove(start, box.SelectionLength);
                            string result = current.Insert(start, digitsOnly);
                            box.Text = result;
                            box.SelectionStart = Math.Min(start + digitsOnly.Length, result.Length);
                        }
                    }
                }
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
