using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AttendanceShiftingManagement.Views
{
    public partial class CashForWorkPayoutPage : UserControl
    {
        private readonly DispatcherTimer _scanDebounceTimer;
        private readonly CashForWorkPayoutViewModel _viewModel;

        public CashForWorkPayoutPage(User currentUser)
        {
            InitializeComponent();
            _viewModel = new CashForWorkPayoutViewModel(currentUser);
            DataContext = _viewModel;

            // Debounce timer for no-suffix scanners (150ms)
            _scanDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _scanDebounceTimer.Tick += ScanDebounceTimer_Tick;

            PreviewKeyDown += UserControl_PreviewKeyDown;
            Loaded += CashForWorkPayoutPage_Loaded;
            Unloaded += CashForWorkPayoutPage_Unloaded;
        }

        private void CashForWorkPayoutPage_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel.RequestScannerFocus += FocusScanner;
            HiddenScannerTextBox.TextChanged += HiddenScannerTextBox_TextChanged;
            HiddenScannerTextBox.KeyDown += HiddenScannerTextBox_KeyDown;
            FocusScanner();
        }

        private void CashForWorkPayoutPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _scanDebounceTimer.Stop();
            _viewModel.RequestScannerFocus -= FocusScanner;
            HiddenScannerTextBox.TextChanged -= HiddenScannerTextBox_TextChanged;
            HiddenScannerTextBox.KeyDown -= HiddenScannerTextBox_KeyDown;
        }

        /// <summary>
        /// Redirects keyboard focus to the scanner textbox if no other visible textbox is focused.
        /// </summary>
        private void UserControl_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (System.Windows.Input.Keyboard.FocusedElement is TextBox focusedTb &&
                focusedTb.Name != "HiddenScannerTextBox" &&
                focusedTb.IsVisible &&
                focusedTb.IsEnabled)
            {
                return;
            }

            if (HiddenScannerTextBox != null && !HiddenScannerTextBox.IsFocused)
            {
                HiddenScannerTextBox.Focus();
                System.Windows.Input.Keyboard.Focus(HiddenScannerTextBox);
            }
        }

        private void FocusScanner()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (HiddenScannerTextBox != null)
                {
                    HiddenScannerTextBox.Focus();
                    System.Windows.Input.Keyboard.Focus(HiddenScannerTextBox);
                }
            });
        }

        private void HiddenScannerTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Support Enter, Return, and Tab delimiters
            if (e.Key == System.Windows.Input.Key.Return ||
                e.Key == System.Windows.Input.Key.Enter ||
                e.Key == System.Windows.Input.Key.Tab)
            {
                _scanDebounceTimer.Stop();
                TriggerScan();
                e.Handled = true;
            }
        }

        private void HiddenScannerTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Restart debounce timer for no-suffix scanners
            _scanDebounceTimer.Stop();
            if (!string.IsNullOrWhiteSpace(HiddenScannerTextBox.Text))
            {
                _scanDebounceTimer.Start();
            }
        }

        private void ScanDebounceTimer_Tick(object? sender, EventArgs e)
        {
            _scanDebounceTimer.Stop();
            TriggerScan();
        }

        /// <summary>
        /// Sanitizes the textbox input and sends it to the ViewModel's ProcessScanCommand.
        /// </summary>
        private void TriggerScan()
        {
            var rawText = HiddenScannerTextBox.Text
                .Replace("\r", "")
                .Replace("\n", "")
                .Replace("\t", "")
                .Trim();

            HiddenScannerTextBox.Text = string.Empty;

            if (string.IsNullOrWhiteSpace(rawText)) return;

            if (_viewModel.ProcessScanCommand.CanExecute(rawText))
            {
                _viewModel.ProcessScanCommand.Execute(rawText);
            }
        }
    }
}
