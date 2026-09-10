using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace AttendanceShiftingManagement.Views
{
    public partial class MasterListPage : UserControl
    {
        private readonly MasterListViewModel _viewModel;

        public MasterListPage(User currentUser)
        {
            InitializeComponent();
            _viewModel = new MasterListViewModel(currentUser);
            DataContext = _viewModel;

            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            Loaded += MasterListPage_Loaded;
            Unloaded += MasterListPage_Unloaded;
            SizeChanged += MasterListPage_SizeChanged;

            // Direct click & input sensitivity on highlighted elements
            SidebarSearchTextBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && _viewModel.IsOnboardingOpen && _viewModel.OnboardingStep == 1)
                {
                    _viewModel.NextOnboardingStep();
                }
            };

            ApprovedDataGrid.SelectionChanged += (s, e) =>
            {
                if (_viewModel.IsOnboardingOpen && _viewModel.OnboardingStep == 2 && _viewModel.SelectedBeneficiary != null)
                {
                    _viewModel.OnboardingStep = 3;
                }
            };

            ApprovedDataGrid.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && _viewModel.SelectedBeneficiary != null)
                {
                    _viewModel.OpenFullProfileCommand.Execute(_viewModel.SelectedBeneficiary);
                    e.Handled = true;
                }
            };

            ViewFullProfileButton.Click += (s, e) =>
            {
                if (_viewModel.IsOnboardingOpen && _viewModel.OnboardingStep == 3)
                {
                    _viewModel.OnboardingStep = 4;
                }
            };

            ApproveButton.Click += (s, e) =>
            {
                if (_viewModel.IsOnboardingOpen && _viewModel.OnboardingStep == 3)
                {
                    _viewModel.OnboardingStep = 4;
                }
            };

            EnrollmentButton.Click += (s, e) =>
            {
                if (_viewModel.IsOnboardingOpen && _viewModel.OnboardingStep == 4)
                {
                    _viewModel.CloseOnboarding();
                }
            };
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(MasterListViewModel.IsOnboardingOpen) or nameof(MasterListViewModel.OnboardingStep))
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
                {
                    UpdateSpotlight();
                });
            }
        }

        private void MasterListPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel.IsOnboardingOpen)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
                {
                    UpdateSpotlight();
                });
            }
        }

        private void MasterListPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            Loaded -= MasterListPage_Loaded;
            Unloaded -= MasterListPage_Unloaded;
            SizeChanged -= MasterListPage_SizeChanged;
        }

        private void MasterListPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_viewModel.IsOnboardingOpen)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
                {
                    UpdateSpotlight();
                });
            }
        }

        private void UpdateSpotlight()
        {
            if (!_viewModel.IsOnboardingOpen || SpotlightOverlayGrid.ActualWidth <= 0 || SpotlightOverlayGrid.ActualHeight <= 0)
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
                case 1: // Sidebar Search -> Float directly to right of sidebar
                    left = targetRect.Right + 20;
                    top = Math.Max(20, targetRect.Y);
                    break;

                case 2: // Masterlist DataGrid -> Float at top-right inside datagrid
                    left = Math.Max(20, Math.Min(overlayWidth - cardWidth - 30, targetRect.X + 30));
                    top = Math.Max(20, Math.Min(overlayHeight - cardEstimatedHeight - 30, targetRect.Y + 30));
                    break;

                case 3: // Quick Actions -> Float to the left of the right sidebar
                    left = Math.Max(20, targetRect.Left - cardWidth - 20);
                    top = Math.Max(20, targetRect.Y);
                    break;

                case 4: // Enrollment Button -> Float to the left of button
                    left = Math.Max(20, targetRect.Left - cardWidth - 20);
                    top = Math.Max(20, targetRect.Y - 100);
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

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            OpenFilterDialog();
        }

        private void OpenFilterDialog()
        {
            _viewModel.IsFilterPanelOpen = true;
            try
            {
                var dialog = new Dialog.MasterListFilterDialog(_viewModel)
                {
                    Owner = Window.GetWindow(this)
                };
                dialog.ShowDialog();
            }
            finally
            {
                _viewModel.IsFilterPanelOpen = false;
            }
        }

        private void DataGridRow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.Item is MasterListBeneficiary beneficiary)
            {
                _viewModel.SelectedApprovedBeneficiary = beneficiary;
                _viewModel.OpenFullProfileCommand.Execute(beneficiary);
            }
        }

        private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var grid = sender as DataGrid;
            if (grid?.SelectedItem == null) return;

            _viewModel.OpenFullProfileCommand.Execute(grid.SelectedItem);
        }

        private void Scanner_QrCodeScanned(string payload)
        {
            _viewModel.ProcessPcScanCommand.Execute(payload);
        }

        private void Scanner_Closed()
        {
            _viewModel.IsPcScannerOpen = false;
        }
    }
}
