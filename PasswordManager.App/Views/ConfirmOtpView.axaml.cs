using Avalonia.Controls;
using Avalonia.Input;
using PasswordManager.App.Services;
using PasswordManager.App.ViewModels;

namespace PasswordManager.App;

public partial class ConfirmOtpView : Window
{
    private ConfirmOtpViewModel? _subscribedVm;
    public IAuthCoordinator? Coordinator { get; set; }

    private TextBox[] _digitBoxes = [];

    public ConfirmOtpView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);

        _digitBoxes = [Digit1Box, Digit2Box, Digit3Box, Digit4Box, Digit5Box, Digit6Box, Digit7Box, Digit8Box];

        foreach (var box in _digitBoxes)
        {
            box.AddHandler(TextInputEvent, OnDigitTextInput, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            box.KeyDown += OnDigitKeyDown;
        }

        _digitBoxes[0].Focus();
    }

    private void OnDigitTextInput(object? sender, TextInputEventArgs e)
    {
        if (sender is not TextBox box) return;

        // Only allow single digit
        e.Handled = true;
        if (string.IsNullOrEmpty(e.Text) || !char.IsDigit(e.Text[0])) return;

        box.Text = e.Text[0].ToString();

        // Advance focus to next box
        int index = Array.IndexOf(_digitBoxes, box);
        if (index < _digitBoxes.Length - 1)
            _digitBoxes[index + 1].Focus();
    }

    private void OnDigitKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox box) return;

        if (e.Key == Key.Back)
        {
            int index = Array.IndexOf(_digitBoxes, box);
            if (!string.IsNullOrEmpty(box.Text))
            {
                box.Text = string.Empty;
            }
            else if (index > 0)
            {
                // Move back and clear previous box
                _digitBoxes[index - 1].Text = string.Empty;
                _digitBoxes[index - 1].Focus();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Left)
        {
            int index = Array.IndexOf(_digitBoxes, box);
            if (index > 0) _digitBoxes[index - 1].Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            int index = Array.IndexOf(_digitBoxes, box);
            if (index < _digitBoxes.Length - 1) _digitBoxes[index + 1].Focus();
            e.Handled = true;
        }
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_subscribedVm != null)
            _subscribedVm.ConfirmOtpSuccessful -= OnConfirmOtpSuccessful;

        _subscribedVm = DataContext as ConfirmOtpViewModel;
        if (_subscribedVm != null)
            _subscribedVm.ConfirmOtpSuccessful += OnConfirmOtpSuccessful;
    }

    private void OnConfirmOtpSuccessful(object? sender, System.EventArgs e)
    {
        Coordinator?.OnConfirmOtpSuccess(this);
    }
}