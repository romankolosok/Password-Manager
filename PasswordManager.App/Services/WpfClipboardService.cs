using PasswordManager.Core.Services.Interfaces;
using System.Windows;
using System.Windows.Threading;

namespace PasswordManager.App.Services
{
    internal class WpfClipboardService : IClipboardService
    {
        private readonly DispatcherTimer _clearTimer;
        private string? _lastCopiedText;

        public WpfClipboardService()
        {
            _clearTimer = new DispatcherTimer();
            _clearTimer.Tick += OnClearTimerTick;
        }

        public void CopyWithAutoClear(string text, int secondsToClear = 15)
        {
            Clipboard.SetText(text);
            _lastCopiedText = text;

            _clearTimer.Stop();
            _clearTimer.Interval = TimeSpan.FromSeconds(secondsToClear);
            _clearTimer.Start();
        }

        public void ClearClipboard()
        {
            _clearTimer.Stop();
            try
            {
                Clipboard.Clear();
            }
            catch
            {
                // Clipboard might be locked by another app, ignore
            }
        }

        private void OnClearTimerTick(object? sender, EventArgs e)
        {
            _clearTimer.Stop();

            try
            {
                if (Clipboard.ContainsText() && Clipboard.GetText() == _lastCopiedText)
                    Clipboard.Clear();
            }
            catch
            {
                // Ignore clipboard errors
            }

            _lastCopiedText = null;
        }
    }
}
