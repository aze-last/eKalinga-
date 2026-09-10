using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using AttendanceShiftingManagement.Views.Dialog;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace AttendanceShiftingManagement.Views
{
    public partial class ProjectDistributionPage : UserControl
    {
        private readonly DispatcherTimer _scanDebounceTimer;
        private ProjectDistributionViewModel? _viewModel;

        public ProjectDistributionPage(User currentUser)
        {
            InitializeComponent();
            _viewModel = new ProjectDistributionViewModel(currentUser);
            DataContext = _viewModel;
            Loaded += ProjectDistributionPage_Loaded;
            Unloaded += ProjectDistributionPage_Unloaded;
            SizeChanged += ProjectDistributionPage_SizeChanged;

            // Debounce timer for no-suffix scanners (150ms)
            _scanDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _scanDebounceTimer.Tick += ScanDebounceTimer_Tick;

            // Global key & text capture to keep scanner armed
            PreviewKeyDown += UserControl_PreviewKeyDown;
            PreviewTextInput += UserControl_PreviewTextInput;
            PreviewMouseDown += UserControl_PreviewMouseDown;
        }

        private void ProjectDistributionPage_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel = DataContext as ProjectDistributionViewModel;
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                _viewModel.RequestScannerFocus += FocusScanner;
            }

            if (_viewModel?.SelectedProgram == null)
            {
                ShowProjectSelection();
            }

            // Hook scanner status events
            if (HiddenScannerTextBox != null)
            {
                HiddenScannerTextBox.GotFocus += (s, args) =>
                {
                    if (_viewModel != null) _viewModel.IsScannerActive = true;
                };
                HiddenScannerTextBox.LostFocus += (s, args) =>
                {
                    if (_viewModel != null) _viewModel.IsScannerActive = false;
                };
                HiddenScannerTextBox.TextChanged += HiddenScannerTextBox_TextChanged;
            }

            FocusScanner();

            if (_viewModel?.IsOnboardingOpen == true)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => UpdateSpotlight());
            }
        }

        private void ProjectDistributionPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _viewModel.RequestScannerFocus -= FocusScanner;
            }
            Loaded -= ProjectDistributionPage_Loaded;
            Unloaded -= ProjectDistributionPage_Unloaded;
            SizeChanged -= ProjectDistributionPage_SizeChanged;
        }

        private void ProjectDistributionPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_viewModel?.IsOnboardingOpen == true)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => UpdateSpotlight());
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProjectDistributionViewModel.IsOnboardingOpen) ||
                e.PropertyName == nameof(ProjectDistributionViewModel.OnboardingStep) ||
                e.PropertyName == nameof(ProjectDistributionViewModel.SelectedProgram))
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => UpdateSpotlight());
            }
        }

        private void UpdateSpotlight()
        {
            if (_viewModel == null || !_viewModel.IsOnboardingOpen || SpotlightOverlayGrid.ActualWidth <= 0 || SpotlightOverlayGrid.ActualHeight <= 0)
            {
                SpotlightMaskPath.Data = null;
                SpotlightBorder.Visibility = Visibility.Collapsed;
                return;
            }

            var targetName = _viewModel.OnboardingTargetName;
            var targetElement = FindName(targetName) as FrameworkElement;

            if (targetElement == null || !targetElement.IsVisible || targetElement.ActualWidth <= 0 || targetElement.ActualHeight <= 0)
            {
                PositionCardCentered();
                return;
            }

            try
            {
                var transform = targetElement.TransformToVisual(SpotlightOverlayGrid);
                var bounds = transform.TransformBounds(new Rect(0, 0, targetElement.ActualWidth, targetElement.ActualHeight));

                double pad = 8.0;
                var overlayWidth = SpotlightOverlayGrid.ActualWidth;
                var overlayHeight = SpotlightOverlayGrid.ActualHeight;

                var paddedRect = new Rect(
                    Math.Max(0, bounds.X - pad),
                    Math.Max(0, bounds.Y - pad),
                    Math.Min(overlayWidth - Math.Max(0, bounds.X - pad), bounds.Width + (pad * 2)),
                    Math.Min(overlayHeight - Math.Max(0, bounds.Y - pad), bounds.Height + (pad * 2))
                );

                // Build cutout geometry: full area minus target hole
                var fullGeometry = new RectangleGeometry(new Rect(0, 0, overlayWidth, overlayHeight));
                var cutoutGeometry = new RectangleGeometry(paddedRect, 10, 10);
                SpotlightMaskPath.Data = new CombinedGeometry(GeometryCombineMode.Exclude, fullGeometry, cutoutGeometry);

                // Position glowing spotlight focus ring
                SpotlightBorder.Visibility = Visibility.Visible;
                SpotlightBorder.Width = paddedRect.Width;
                SpotlightBorder.Height = paddedRect.Height;
                SpotlightBorder.Margin = new Thickness(paddedRect.X, paddedRect.Y, 0, 0);
                SpotlightBorder.HorizontalAlignment = HorizontalAlignment.Left;
                SpotlightBorder.VerticalAlignment = VerticalAlignment.Top;

                // Position floating instruction card
                PositionInstructionCard(paddedRect, _viewModel.OnboardingStep);
            }
            catch
            {
                PositionCardCentered();
            }
        }

        private void PositionInstructionCard(Rect targetRect, int step)
        {
            double cardWidth = 430;
            double cardEstimatedHeight = 280;
            double overlayWidth = SpotlightOverlayGrid.ActualWidth;
            double overlayHeight = SpotlightOverlayGrid.ActualHeight;

            double left;
            double top;

            switch (step)
            {
                case 1: // ProjectContextBar -> Float below project bar
                    left = Math.Max(20, Math.Min(overlayWidth - cardWidth - 30, targetRect.X + 40));
                    top = targetRect.Bottom + 20;
                    break;

                case 2: // ScannerSearchBarSection -> Float below search bar or to the right
                    left = Math.Max(20, Math.Min(overlayWidth - cardWidth - 30, targetRect.X + 20));
                    top = targetRect.Bottom + 16;
                    break;

                case 3: // DistributionColumnsGrid -> Float centered near top inside columns
                    left = Math.Max(20, Math.Min(overlayWidth - cardWidth - 30, (overlayWidth - cardWidth) / 2));
                    top = Math.Max(20, targetRect.Y + 40);
                    break;

                case 4: // Verification / Pending card -> Float to the left or above pending card
                    left = Math.Max(20, targetRect.Left - cardWidth - 20);
                    top = Math.Max(20, targetRect.Y + 20);
                    break;

                default:
                    left = (overlayWidth - cardWidth) / 2;
                    top = (overlayHeight - cardEstimatedHeight) / 2;
                    break;
            }

            // Keep within viewport boundaries
            left = Math.Max(16, Math.Min(overlayWidth - cardWidth - 16, left));
            top = Math.Max(16, Math.Min(overlayHeight - cardEstimatedHeight - 16, top));

            Canvas.SetLeft(InstructionCard, left);
            Canvas.SetTop(InstructionCard, top);
        }

        private void PositionCardCentered()
        {
            double cardWidth = 430;
            double cardHeight = 280;
            double overlayWidth = SpotlightOverlayGrid.ActualWidth > 0 ? SpotlightOverlayGrid.ActualWidth : 1200;
            double overlayHeight = SpotlightOverlayGrid.ActualHeight > 0 ? SpotlightOverlayGrid.ActualHeight : 800;

            SpotlightMaskPath.Data = new RectangleGeometry(new Rect(0, 0, overlayWidth, overlayHeight));
            SpotlightBorder.Visibility = Visibility.Collapsed;

            Canvas.SetLeft(InstructionCard, Math.Max(16, (overlayWidth - cardWidth) / 2));
            Canvas.SetTop(InstructionCard, Math.Max(16, (overlayHeight - cardHeight) / 2));
        }

        /// <summary>
        /// Redirects keyboard focus to the scanner textbox if no other visible textbox is focused.
        /// </summary>
        private void UserControl_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Do not steal focus if any overlay or modal is open
            if (_viewModel?.IsAnyOverlayOpen == true)
            {
                return;
            }

            // Don't redirect if user is typing in a visible search/filter textbox
            if (System.Windows.Input.Keyboard.FocusedElement is TextBox focusedTb &&
                focusedTb.Name != "HiddenScannerTextBox" &&
                focusedTb.IsVisible &&
                focusedTb.IsEnabled &&
                !focusedTb.IsReadOnly)
            {
                return;
            }

            if (HiddenScannerTextBox != null && !HiddenScannerTextBox.IsFocused)
            {
                Helpers.ScannerDiagnostics.Report(
                    Helpers.ScanErrorKind.ScannerNotFocused,
                    $"Focus was not on HiddenScannerTextBox (focused: {System.Windows.Input.Keyboard.FocusedElement?.GetType().Name ?? "none"}); auto-refocusing.");
                HiddenScannerTextBox.Focus();
                System.Windows.Input.Keyboard.Focus(HiddenScannerTextBox);
            }
        }

        private void UserControl_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            // Do not steal focus if any overlay or modal is open
            if (_viewModel?.IsAnyOverlayOpen == true)
            {
                return;
            }

            // Don't redirect if user is typing in a visible search/filter textbox
            if (System.Windows.Input.Keyboard.FocusedElement is TextBox focusedTb &&
                focusedTb.Name != "HiddenScannerTextBox" &&
                focusedTb.IsVisible &&
                focusedTb.IsEnabled &&
                !focusedTb.IsReadOnly)
            {
                return;
            }

            if (HiddenScannerTextBox != null && !HiddenScannerTextBox.IsFocused)
            {
                HiddenScannerTextBox.Focus();
                System.Windows.Input.Keyboard.Focus(HiddenScannerTextBox);
                HiddenScannerTextBox.AppendText(e.Text);
                HiddenScannerTextBox.CaretIndex = HiddenScannerTextBox.Text.Length;
                e.Handled = true;
            }
        }

        private void UserControl_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Do not steal focus if any overlay or modal is open
            if (_viewModel?.IsAnyOverlayOpen == true)
            {
                return;
            }

            // When clicking inside interactive elements (buttons, checkboxes, textboxes, etc.), don't steal focus
            if (IsInteractiveElement(e.OriginalSource as DependencyObject))
            {
                return;
            }

            Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                if (_viewModel?.IsAnyOverlayOpen == true)
                {
                    return;
                }

                if (System.Windows.Input.Keyboard.FocusedElement is not TextBox tb || tb.Name == "HiddenScannerTextBox")
                {
                    FocusScanner();
                }
            });
        }

        private static bool IsInteractiveElement(DependencyObject? element)
        {
            while (element != null)
            {
                if (element is System.Windows.Controls.Primitives.ButtonBase ||
                    element is TextBox ||
                    element is ComboBox ||
                    element is System.Windows.Controls.Primitives.ScrollBar ||
                    element is DataGrid ||
                    element is DataGridRow ||
                    element is DataGridCell)
                {
                    return true;
                }
                element = VisualTreeHelper.GetParent(element);
            }
            return false;
        }

        private void FocusScanner()
        {
            if (_viewModel?.IsAnyOverlayOpen == true)
            {
                return;
            }

            // Use Dispatcher to ensure focus happens after any pending layout updates
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (_viewModel?.IsAnyOverlayOpen == true)
                {
                    return;
                }

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

            // Queue protection: clear textbox if an overlay/dialog is active
            if (viewModel != null && (viewModel.IsAnyOverlayOpen || viewModel.IsScannedResultVisible || viewModel.IsReleaseSuccessState))
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

            // Queue protection: clear textbox if an overlay/dialog is active
            if (viewModel != null && (viewModel.IsAnyOverlayOpen || viewModel.IsScannedResultVisible || viewModel.IsReleaseSuccessState))
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
