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
            var interval = (now - _lastKeyTime).TotalMilliseconds;
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

            // Capture alphanumeric keys (standard for Code 128)
            var keyChar = GetCharFromKey(e.Key);
            if (keyChar != null)
            {
                // Optional: If the interval is too long, it might be a human typing
                // For a dedicated portal, we can be more lenient or strict.
                // For now, we just buffer everything until Enter.
                _barcodeBuffer.Append(keyChar);
                
                // If this was a fast burst, we can mark it as handled to prevent
                // it from triggering other UI elements or textboxes.
                if (interval < MaxKeyIntervalMs)
                {
                    // e.Handled = true; // Uncomment if we want to swallow the keys
                }
            }
        }

        private char? GetCharFromKey(Key key)
        {
            // Simple mapping for common barcode characters
            if (key >= Key.D0 && key <= Key.D9) return (char)('0' + (key - Key.D0));
            if (key >= Key.NumPad0 && key <= Key.NumPad9) return (char)('0' + (key - Key.NumPad0));
            if (key >= Key.A && key <= Key.Z) return (char)('A' + (key - Key.A));
            if (key == Key.OemMinus || key == Key.Subtract) return '-';
            
            return null;
        }

        private void ManualEntry_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Please use the hardware scanner gun. Manual entry is currently disabled.", "Scanning Portal", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
