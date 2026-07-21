using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using AttendanceShiftingManagement.Views.Dialog;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AttendanceShiftingManagement.Views
{
    public partial class ProjectDistributionPage : UserControl
    {
        private readonly DispatcherTimer _scanDebounceTimer;

        public ProjectDistributionPage(User currentUser)
        {
            InitializeComponent();
            DataContext = new ProjectDistributionViewModel(currentUser);
            Loaded += ProjectDistributionPage_Loaded;

            // Debounce timer for no-suffix scanners (150ms)
            _scanDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _scanDebounceTimer.Tick += ScanDebounceTimer_Tick;

            // Global key capture to keep scanner armed
            PreviewKeyDown += UserControl_PreviewKeyDown;
        }

        private void ProjectDistributionPage_Loaded(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as ProjectDistributionViewModel;
            if (viewModel?.SelectedProgram == null)
            {
                ShowProjectSelection();
            }

            // Subscribe to ViewModel's RequestScannerFocus event
            if (viewModel != null)
            {
                viewModel.RequestScannerFocus += FocusScanner;
            }

            // Hook scanner status events
            if (HiddenScannerTextBox != null)
            {
                HiddenScannerTextBox.GotFocus += (s, args) =>
                {
                    if (viewModel != null) viewModel.IsScannerActive = true;
                };
                HiddenScannerTextBox.LostFocus += (s, args) =>
                {
                    if (viewModel != null) viewModel.IsScannerActive = false;
                };
                HiddenScannerTextBox.TextChanged += HiddenScannerTextBox_TextChanged;
            }

            FocusScanner();
        }

        /// <summary>
        /// Redirects keyboard focus to the scanner textbox if no other visible textbox is focused.
        /// </summary>
        private void UserControl_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Don't redirect if user is typing in a visible search/filter textbox
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
            // Use Dispatcher to ensure focus happens after any pending layout updates
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (HiddenScannerTextBox != null)
                {
                    HiddenScannerTextBox.Focus();
                    System.Windows.Input.Keyboard.Focus(HiddenScannerTextBox);
                }
            });
        }

        private void ChangeProject_Click(object sender, RoutedEventArgs e)
        {
            ShowProjectSelection();
        }

        private void ShowProjectSelection()
        {
            // Reset the picker search so the full project list is shown on open.
            if (DataContext is ProjectDistributionViewModel vm)
            {
                vm.ProgramSearchText = string.Empty;
            }

            var dialog = new ProjectSelectionDialog
            {
                DataContext = this.DataContext,
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                // Project selection is handled via binding to SelectedProgramSummary
            }

            // Re-arm scanner after dialog closes
            FocusScanner();
        }

        private void UnreleasedGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var viewModel = DataContext as ProjectDistributionViewModel;
            if (viewModel?.ConfirmUnreleasedCommand.CanExecute(null) == true)
            {
                viewModel.ConfirmUnreleasedCommand.Execute(null);
            }
        }

        private void PendingGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var viewModel = DataContext as ProjectDistributionViewModel;
            if (viewModel?.ConfirmReleaseCommand.CanExecute(null) == true)
            {
                viewModel.ConfirmReleaseCommand.Execute(null);
            }
        }

        private void ReleasedGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var viewModel = DataContext as ProjectDistributionViewModel;
            if (viewModel?.OpenReleasedBeneficiaryOverlayCommand.CanExecute(null) == true)
            {
                viewModel.OpenReleasedBeneficiaryOverlayCommand.Execute(null);
            }
        }

        private void ShowDetailDialog(int beneficiaryStagingId = 0)
        {
            var viewModel = DataContext as ProjectDistributionViewModel;

            // Load the household roster before showing so the dialog can flag members who
            // already received this assistance.
            if (viewModel != null && beneficiaryStagingId > 0)
            {
                _ = viewModel.LoadDetailDialogHouseholdAsync(beneficiaryStagingId);
            }

            var dialog = new ProjectDistributionDetailDialog
            {
                DataContext = this.DataContext,
                Owner = Window.GetWindow(this)
            };

            if (viewModel != null)
            {
                // Define the handler
                void OnRequestClose()
                {
                    dialog.Close();
                }

                // Subscribe
                viewModel.RequestCloseDialog += OnRequestClose;

                try
                {
                    dialog.ShowDialog();
                }
                finally
                {
                    // Unsubscribe to prevent memory leaks
                    viewModel.RequestCloseDialog -= OnRequestClose;
                }
            }
            else
            {
                dialog.ShowDialog();
            }

            // Re-arm scanner after dialog closes
            FocusScanner();
        }

        private void ProjectDistributionAddBeneficiaryPanel_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void HiddenScannerTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var viewModel = DataContext as ProjectDistributionViewModel;

            // Queue protection: clear textbox if a dialog is active
            if (viewModel != null && (viewModel.IsScannedResultVisible || viewModel.IsReleaseSuccessState))
            {
                HiddenScannerTextBox.Text = string.Empty;
                e.Handled = true;
                return;
            }

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
            var viewModel = DataContext as ProjectDistributionViewModel;

            // Queue protection: clear textbox if a dialog is active
            if (viewModel != null && (viewModel.IsScannedResultVisible || viewModel.IsReleaseSuccessState))
            {
                HiddenScannerTextBox.Text = string.Empty;
                return;
            }

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
            // Sanitize: strip control characters
            var rawText = HiddenScannerTextBox.Text
                .Replace("\r", "")
                .Replace("\n", "")
                .Replace("\t", "")
                .Trim();

            HiddenScannerTextBox.Text = string.Empty;

            if (string.IsNullOrWhiteSpace(rawText)) return;

            var viewModel = DataContext as ProjectDistributionViewModel;
            if (viewModel != null && viewModel.ProcessScanCommand.CanExecute(rawText))
            {
                viewModel.ProcessScanCommand.Execute(rawText);
            }
        }

        private void Scanner_QrCodeScanned(string payload)
        {
            var viewModel = DataContext as ProjectDistributionViewModel;
            if (viewModel != null && viewModel.ProcessScanCommand.CanExecute(payload))
            {
                viewModel.ProcessScanCommand.Execute(payload);
            }
        }

        private void Scanner_Closed()
        {
            var viewModel = DataContext as ProjectDistributionViewModel;
            if (viewModel != null)
            {
                viewModel.IsPcScannerOpen = false;
            }
            FocusScanner();
        }
    }
}
