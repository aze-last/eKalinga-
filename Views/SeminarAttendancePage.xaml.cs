using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using AttendanceShiftingManagement.Views.Dialog;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AttendanceShiftingManagement.Views
{
    public partial class SeminarAttendancePage : UserControl
    {
        private readonly DispatcherTimer _scanDebounceTimer;

        public SeminarAttendancePage(User currentUser)
        {
            InitializeComponent();
            DataContext = new SeminarAttendanceViewModel(currentUser);

            _scanDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _scanDebounceTimer.Tick += ScanDebounceTimer_Tick;

            PreviewKeyDown += UserControl_PreviewKeyDown;
            Loaded += SeminarAttendancePage_Loaded;
            Unloaded += SeminarAttendancePage_Unloaded;
        }

        private void SeminarAttendancePage_Loaded(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as SeminarAttendanceViewModel;
            if (viewModel?.SelectedEvent == null)
            {
                Browse_Click(sender, e);
            }

            if (HiddenScannerTextBox != null)
            {
                HiddenScannerTextBox.TextChanged += HiddenScannerTextBox_TextChanged;
                HiddenScannerTextBox.KeyDown += HiddenScannerTextBox_KeyDown;
                FocusScanner();
            }
        }

        private void SeminarAttendancePage_Unloaded(object sender, RoutedEventArgs e)
        {
            _scanDebounceTimer.Stop();
            if (HiddenScannerTextBox != null)
            {
                HiddenScannerTextBox.TextChanged -= HiddenScannerTextBox_TextChanged;
                HiddenScannerTextBox.KeyDown -= HiddenScannerTextBox_KeyDown;
            }
        }

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
            _scanDebounceTimer.Stop();
            if (!string.IsNullOrWhiteSpace(HiddenScannerTextBox?.Text))
            {
                _scanDebounceTimer.Start();
            }
        }

        private void ScanDebounceTimer_Tick(object? sender, EventArgs e)
        {
            _scanDebounceTimer.Stop();
            TriggerScan();
        }

        private void TriggerScan()
        {
            if (HiddenScannerTextBox == null) return;

            var rawText = HiddenScannerTextBox.Text
                .Replace("\r", "")
                .Replace("\n", "")
                .Replace("\t", "")
                .Trim();

            HiddenScannerTextBox.Text = string.Empty;

            if (string.IsNullOrWhiteSpace(rawText)) return;

            if (DataContext is SeminarAttendanceViewModel vm)
            {
                vm.ProcessPcScanCommand.Execute(rawText);
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SeminarAttendanceViewModel vm)
            {
                var dialog = new SeminarEventListDialog(vm)
                {
                    Owner = Window.GetWindow(this)
                };
                dialog.ShowDialog();
            }
        }

        private void Scanner_QrCodeScanned(string payload)
        {
            if (DataContext is SeminarAttendanceViewModel vm)
            {
                vm.ProcessPcScanCommand.Execute(payload);
            }
        }

        private void Scanner_Closed()
        {
            if (DataContext is SeminarAttendanceViewModel vm)
            {
                vm.IsPcScannerOpen = false;
            }
            FocusScanner();
        }
    }
}
