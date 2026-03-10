using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using PasswordManager.App.Services;
using PasswordManager.App.ViewModels;

namespace PasswordManager.App.Views;

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
            box.AddHandler(InputElement.KeyDownEvent, OnDigitKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
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
        box.CaretIndex = box.Text.Length;

        // Advance focus to next box, or move focus away when all digits are filled
        int index = Array.IndexOf(_digitBoxes, box);
        if (index < 0) return;

        if (index < _digitBoxes.Length - 1)
        {
            var nextBox = _digitBoxes[index + 1];
            nextBox.Focus();
            nextBox.CaretIndex = nextBox.Text?.Length ?? 0;
        }
        else if (_digitBoxes.All(b => !string.IsNullOrWhiteSpace(b.Text)))
        {
            // All digits entered – move focus away from the last box
            ConfirmButton?.Focus();
        }
    }

    private void OnDigitKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox box) return;

        if (e.Key is Key.Back or Key.Delete)
        {
            int index = Array.IndexOf(_digitBoxes, box);
            if (index < 0) return;

            if (!string.IsNullOrEmpty(box.Text))
            {
                // Clear current box and move focus to the previous box
                box.Text = string.Empty;
                if (index > 0)
                {
                    var previous = _digitBoxes[index - 1];
                    previous.Focus();
                    previous.CaretIndex = previous.Text?.Length ?? 0;
                }
            }
            else if (index > 0)
            {
                // Current box is empty: move back and clear previous box
                var previous = _digitBoxes[index - 1];
                previous.Focus();
                if (!string.IsNullOrEmpty(previous.Text))
                {
                    previous.Text = string.Empty;
                }
                previous.CaretIndex = previous.Text?.Length ?? 0;
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Left)
        {
            int index = Array.IndexOf(_digitBoxes, box);
            if (index > 0)
            {
                var previous = _digitBoxes[index - 1];
                previous.Focus();
                previous.CaretIndex = previous.Text?.Length ?? 0;
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            int index = Array.IndexOf(_digitBoxes, box);
            if (index < _digitBoxes.Length - 1)
            {
                var next = _digitBoxes[index + 1];
                next.Focus();
                next.CaretIndex = next.Text?.Length ?? 0;
            }
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