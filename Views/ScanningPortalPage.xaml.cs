using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AttendanceShiftingManagement.Views
{
    public partial class ScanningPortalPage : UserControl
    {
        private readonly ScanningPortalViewModel _viewModel;
        private readonly StringBuilder _barcodeBuffer = new();
        private DateTime _lastKeyTime = DateTime.MinValue;
        private const int MaxKeyIntervalMs = 50; // Threshold for hardware vs human typing

        public ScanningPortalPage(User currentUser)
        {
            InitializeComponent();
            _viewModel = new ScanningPortalViewModel(currentUser);
            DataContext = _viewModel;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            Focus(); // Ensure the control has focus to capture key events
            
            // Register global key handler on the parent window
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.PreviewKeyDown += Window_PreviewKeyDown;
                this.Unloaded += (s, args) => window.PreviewKeyDown -= Window_PreviewKeyDown;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // If an overlay is open, don't capture global keys for scanning
            if (_viewModel.IsOverlayVisible) return;

            var now = DateTime.Now;
            var intervalMs = (now - _lastKeyTime).TotalMilliseconds;
            _lastKeyTime = now;

            // Scanners usually send 'Enter' (Return) as a terminator
            if (e.Key == Key.Enter)
            {
                if (_barcodeBuffer.Length > 0)
                {
                    var barcode = _barcodeBuffer.ToString();
                    _barcodeBuffer.Clear();
                    _ = _viewModel.ProcessScanAsync(barcode);
                    e.Handled = true;
                }
                return;
            }

            // Stale-buffer timeout: if more than 200ms passed between characters while
            // a buffer was accumulating, the previous partial scan is stale — reset before
            // appending so it doesn't corrupt the new scan.
            if (_barcodeBuffer.Length > 0 && intervalMs > 200)
            {
                var stale = _barcodeBuffer.ToString();
                _barcodeBuffer.Clear();
                ScannerDiagnostics.Report(ScanErrorKind.StaleBufferReset,
                    $"Buffer cleared after {intervalMs:F0}ms gap (stale partial: \"{stale}\").");
            }

            // Attempt to map the key to a character
            var keyChar = GetCharFromKey(e.Key);

            if (keyChar == null)
            {
                // An unmapped key during an active scan means the scanner emitted a character
                // this mapper cannot decode. Reset the buffer with a visible error rather than
                // silently building a corrupted payload.
                if (_barcodeBuffer.Length > 0 && intervalMs < MaxKeyIntervalMs)
                {
                    var partial = _barcodeBuffer.ToString();
                    _barcodeBuffer.Clear();
                    ScannerDiagnostics.Report(ScanErrorKind.UnmappedCharacter,
                        $"Unmapped key '{e.Key}' during scan; buffer reset. Partial was: \"{partial}\".");
                    _viewModel.SetScanError("Unsupported character in scan — check scanner keyboard layout.");
                    e.Handled = true;
                }
                return;
            }

            _barcodeBuffer.Append(keyChar);

            // Max buffer length guard: catches a stuck key or runaway input loop.
            if (_barcodeBuffer.Length > 250)
            {
                _barcodeBuffer.Clear();
                ScannerDiagnostics.Report(ScanErrorKind.StaleBufferReset,
                    "Buffer exceeded 250 characters; reset. Possible stuck key or scanner malfunction.");
                _viewModel.SetScanError("Scanner buffer overflow — scan timed out or a key is stuck.");
            }

            // Mark as handled when the interval is clearly scanner-speed to prevent
            // stray characters from triggering other UI elements.
            if (intervalMs < MaxKeyIntervalMs)
            {
                e.Handled = true;
            }
        }

        private char? GetCharFromKey(Key key)
        {
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

            // Digits (top row): respect Shift for symbols
            if (key >= Key.D0 && key <= Key.D9)
            {
                if (shift)
                {
                    const string shiftDigits = ")!@#$%^&*(";
                    return shiftDigits[key - Key.D0];
                }
                return (char)('0' + (key - Key.D0));
            }

            // Numpad digits (always unshifted)
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
                return (char)('0' + (key - Key.NumPad0));

            // Letters: uppercase when Shift, lowercase otherwise
            if (key >= Key.A && key <= Key.Z)
                return shift ? (char)('A' + (key - Key.A)) : (char)('a' + (key - Key.A));

            // Symbols needed for GUID / base64 / JSON payloads and common barcode formats
            return key switch
            {
                Key.OemMinus or Key.Subtract   => shift ? '_' : '-',
                Key.OemPeriod                   => shift ? '>' : '.',
                Key.OemPlus or Key.Add          => shift ? '+' : '=',
                Key.OemQuestion                 => shift ? '?' : '/',
                Key.OemOpenBrackets             => shift ? '{' : '[',
                Key.OemCloseBrackets            => shift ? '}' : ']',
                Key.OemSemicolon                => shift ? ':' : ';',
                Key.OemComma                    => shift ? '<' : ',',
                Key.OemQuotes                   => shift ? '"' : '\'',
                Key.OemBackslash or Key.Oem5    => shift ? '|' : '\\',
                Key.Space                       => ' ',
                Key.Multiply                    => '*',
                Key.Divide                      => '/',
                _                               => null
            };
        }

        private void ManualEntry_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Please use the hardware scanner gun. Manual entry is currently disabled.", "Scanning Portal", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
