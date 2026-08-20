using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using AttendanceShiftingManagement.Views.Dialog;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace AttendanceShiftingManagement.Views
{
    public partial class CashForWorkOcrPage : UserControl
    {
        private readonly CashForWorkOcrViewModel _viewModel;
        private readonly DispatcherTimer _scanDebounceTimer;

        public CashForWorkOcrPage(User currentUser)
        {
            InitializeComponent();
            _viewModel = new CashForWorkOcrViewModel(currentUser);
            DataContext = _viewModel;

            _scanDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _scanDebounceTimer.Tick += ScanDebounceTimer_Tick;

            PreviewKeyDown += UserControl_PreviewKeyDown;
            Loaded += CashForWorkOcrPage_Loaded;
            Unloaded += CashForWorkOcrPage_Unloaded;
            SizeChanged += CashForWorkOcrPage_SizeChanged;
        }

        private void CashForWorkOcrPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }

            if (_viewModel?.SelectedEvent == null)
            {
                Browse_Click(sender, e);
            }

            if (HiddenScannerTextBox != null)
            {
                HiddenScannerTextBox.TextChanged += HiddenScannerTextBox_TextChanged;
                HiddenScannerTextBox.KeyDown += HiddenScannerTextBox_KeyDown;
                FocusScanner();
            }

            if (_viewModel?.IsOnboardingOpen == true)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => UpdateSpotlight());
            }
        }

        private void CashForWorkOcrPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }
            _scanDebounceTimer.Stop();
            if (HiddenScannerTextBox != null)
            {
                HiddenScannerTextBox.TextChanged -= HiddenScannerTextBox_TextChanged;
                HiddenScannerTextBox.KeyDown -= HiddenScannerTextBox_KeyDown;
            }
            SizeChanged -= CashForWorkOcrPage_SizeChanged;
        }

        private void CashForWorkOcrPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_viewModel?.IsOnboardingOpen == true)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => UpdateSpotlight());
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CashForWorkOcrViewModel.IsOnboardingOpen) ||
                e.PropertyName == nameof(CashForWorkOcrViewModel.OnboardingStep) ||
                e.PropertyName == nameof(CashForWorkOcrViewModel.SelectedEvent))
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

                var fullGeometry = new RectangleGeometry(new Rect(0, 0, overlayWidth, overlayHeight));
                var cutoutGeometry = new RectangleGeometry(paddedRect, 10, 10);
                SpotlightMaskPath.Data = new CombinedGeometry(GeometryCombineMode.Exclude, fullGeometry, cutoutGeometry);

                SpotlightBorder.Visibility = Visibility.Visible;
                SpotlightBorder.Width = paddedRect.Width;
                SpotlightBorder.Height = paddedRect.Height;
                SpotlightBorder.Margin = new Thickness(paddedRect.X, paddedRect.Y, 0, 0);
                SpotlightBorder.HorizontalAlignment = HorizontalAlignment.Left;
                SpotlightBorder.VerticalAlignment = VerticalAlignment.Top;

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
                case 1: // SidebarContainer -> float to the right
                case 2: // LiveScannerDockBorder -> float to the right
                    left = Math.Max(20, Math.Min(overlayWidth - cardWidth - 20, targetRect.Right + 24));
                    top = Math.Max(20, Math.Min(overlayHeight - cardEstimatedHeight - 20, targetRect.Y + 10));
                    break;

                case 3: // AttendanceGridBorder -> float centered inside/above grid
                    left = Math.Max(20, (overlayWidth - cardWidth) / 2);
                    top = Math.Max(20, targetRect.Y + 30);
                    break;

                case 4: // EventHeaderActions -> float below or to the left
                    left = Math.Max(20, targetRect.Left - cardWidth - 20);
                    top = Math.Max(20, targetRect.Bottom + 16);
                    break;

                default:
                    left = (overlayWidth - cardWidth) / 2;
                    top = (overlayHeight - cardEstimatedHeight) / 2;
                    break;
            }

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

            if (DataContext is CashForWorkOcrViewModel vm)
            {
                vm.ProcessPcScanCommand.Execute(rawText);
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CashForWorkOcrViewModel vm)
            {
                var owner = Window.GetWindow(this);
                var dialog = new CashForWorkEventListDialog(vm)
                {
                    Owner = owner
                };

                var ownerContent = owner?.Content as UIElement;
                var previousEffect = ownerContent?.Effect;
                if (ownerContent != null)
                {
                    ownerContent.Effect = new BlurEffect { Radius = 15.0 };
                }

                try
                {
                    dialog.ShowDialog();
                }
                finally
                {
                    if (ownerContent != null)
                    {
                        ownerContent.Effect = previousEffect;
                    }
                }
            }
        }

        private void Scanner_QrCodeScanned(string payload)
        {
            if (DataContext is CashForWorkOcrViewModel vm)
            {
                vm.ProcessPcScanCommand.Execute(payload);
            }
        }

        private void Scanner_Closed()
        {
            if (DataContext is CashForWorkOcrViewModel vm)
            {
                vm.IsPcScannerOpen = false;
            }
            FocusScanner();
        }
    }
}
