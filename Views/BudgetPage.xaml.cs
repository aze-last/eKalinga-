using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace AttendanceShiftingManagement.Views
{
    public partial class BudgetPage : UserControl
    {
        private readonly BudgetViewModel _viewModel;
        private DispatcherTimer? _guidanceTimer;

        public BudgetPage(User currentUser, AyudaProgramType? initialProgramType = null)
        {
            InitializeComponent();
            _viewModel = new BudgetViewModel(currentUser);
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.ProjectCreatedGoToDistribution += OnProjectCreatedGoToDistribution;
            Loaded += BudgetPage_Loaded;
            Unloaded += OnBudgetPageUnloaded;
            SizeChanged += BudgetPage_SizeChanged;
            DataContext = _viewModel;

            // Direct click & input sensitivity on highlighted elements
            SyncGgmsButton.Click += (s, e) =>
            {
                if (_viewModel.IsOnboardingOpen && _viewModel.OnboardingStep == 2)
                {
                    _viewModel.OnboardingStep = 3;
                }
            };

            BudgetDataGrid.SelectionChanged += (s, e) =>
            {
                if (_viewModel.IsOnboardingOpen && _viewModel.OnboardingStep == 3 && _viewModel.SelectedBudget != null)
                {
                    _viewModel.OnboardingStep = 4;
                }
            };

            CreateProjectButton.Click += (s, e) =>
            {
                if (_viewModel.IsOnboardingOpen && _viewModel.OnboardingStep == 4)
                {
                    _viewModel.CloseOnboarding();
                }
            };

            ConfirmCreateProjectButton.Click += (s, e) =>
            {
                if (_viewModel.IsCreateProjectTourOpen)
                {
                    _viewModel.CloseCreateProjectTour();
                }
            };

            if (initialProgramType.HasValue)
            {
                _viewModel.SelectedProgramType = initialProgramType.Value;
                _viewModel.IsCreateProjectGuided = true;
                _viewModel.SetNeutralStatus($"Select a Private Donation or GGMS Fund row below, then click CREATE PROJECT to spawn your {(initialProgramType.Value == AyudaProgramType.Seminar ? "Seminar & Training" : "Cash-for-Work")} event.");

                _guidanceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
                _guidanceTimer.Tick += (sender, args) =>
                {
                    _viewModel.IsCreateProjectGuided = false;
                    _guidanceTimer?.Stop();
                };
                _guidanceTimer.Start();
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(BudgetViewModel.IsOnboardingOpen)
                or nameof(BudgetViewModel.OnboardingStep)
                or nameof(BudgetViewModel.IsCreateProjectTourOpen)
                or nameof(BudgetViewModel.CreateProjectTourStep)
                or nameof(BudgetViewModel.IsAnyTourOpen)
                or nameof(BudgetViewModel.ActiveTourStep))
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
                {
                    UpdateSpotlight();
                });
            }
        }

        private void BudgetPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel.IsAnyTourOpen)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
                {
                    UpdateSpotlight();
                });
            }
        }

        private void BudgetPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_viewModel.IsAnyTourOpen)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
                {
                    UpdateSpotlight();
                });
            }
        }

        private void UpdateSpotlight()
        {
            if (!_viewModel.IsAnyTourOpen || SpotlightOverlayGrid.ActualWidth <= 0 || SpotlightOverlayGrid.ActualHeight <= 0)
            {
                SpotlightMaskPath.Data = null;
                SpotlightBorder.Visibility = Visibility.Collapsed;
                return;
            }

            var targetName = _viewModel.ActiveTourTargetName;
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
                PositionInstructionCard(paddedRect, _viewModel.ActiveTourStep, _viewModel.IsCreateProjectTourOpen);
            }
            catch
            {
                PositionCardCentered();
            }
        }

        private void PositionInstructionCard(Rect targetRect, int step, bool isCreateProjectTour)
        {
            double cardWidth = 430;
            double cardEstimatedHeight = 280;
            double overlayWidth = SpotlightOverlayGrid.ActualWidth;
            double overlayHeight = SpotlightOverlayGrid.ActualHeight;

            double left;
            double top;

            if (isCreateProjectTour)
            {
                switch (step)
                {
                    case 1: // Basic Information -> Place to the right of column 0
                    case 2: // Release Settings -> Place to the right of column 0
                        left = targetRect.Right + 20;
                        top = Math.Max(20, targetRect.Y);
                        break;

                    case 3: // Funding Source -> Place to the right of column 2 or left if tight
                        if (targetRect.Right + cardWidth + 20 < overlayWidth)
                        {
                            left = targetRect.Right + 20;
                        }
                        else
                        {
                            left = Math.Max(20, targetRect.Left - cardWidth - 20);
                        }
                        top = Math.Max(20, targetRect.Y);
                        break;

                    case 4: // Beneficiary Enrollment -> Place to the left of column 4
                        left = Math.Max(20, targetRect.Left - cardWidth - 20);
                        top = Math.Max(20, targetRect.Y);
                        break;

                    case 5: // Confirm Create Project Button -> Place above button
                        left = Math.Max(20, targetRect.Right - cardWidth);
                        top = Math.Max(20, targetRect.Top - cardEstimatedHeight - 16);
                        break;

                    default:
                        left = (overlayWidth - cardWidth) / 2;
                        top = (overlayHeight - cardEstimatedHeight) / 2;
                        break;
                }
            }
            else
            {
                switch (step)
                {
                    case 1: // Financial Summary Grid -> Position below the summary cards
                        left = Math.Max(20, Math.Min(overlayWidth - cardWidth - 30, targetRect.X + 20));
                        top = targetRect.Bottom + 16;
                        break;

                    case 2: // Sync GGMS Button -> Float to the right of the sidebar
                        left = targetRect.Right + 20;
                        top = Math.Max(20, targetRect.Y - 10);
                        break;

                    case 3: // Budget Browser Card -> Float at top-right inside the browser area
                        left = Math.Max(20, Math.Min(overlayWidth - cardWidth - 30, targetRect.Right - cardWidth - 30));
                        top = Math.Max(20, targetRect.Top + 20);
                        break;

                    case 4: // Create Project Button -> Float to the right of the sidebar
                        left = targetRect.Right + 20;
                        top = Math.Max(20, targetRect.Y - 20);
                        break;

                    default:
                        left = (overlayWidth - cardWidth) / 2;
                        top = (overlayHeight - cardEstimatedHeight) / 2;
                        break;
                }
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

        private void OnProjectCreatedGoToDistribution(string projectName)
        {
            // CFW projects live in the Cash-for-Work Payout module, not Distribution.
            if (_viewModel._createdCfwBudgetId.HasValue)
            {
                var goToPayout = MessageBox.Show(
                    $"Cash-for-Work project \"{projectName}\" was created successfully.\n\nGo to Cash-for-Work Payout now to create events and manage worker attendance?",
                    "CFW Project Created",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (goToPayout == MessageBoxResult.Yes &&
                    Window.GetWindow(this) is MainWindow cfwMainWindow &&
                    cfwMainWindow.DataContext is BarangayMainViewModel cfwMainVm)
                {
                    cfwMainVm.ShowCashForWorkCommand.Execute(null);
                }
                return;
            }

            // Seminar projects live in the Seminar Attendance module, not Distribution.
            if (_viewModel._createdSeminarBudgetId.HasValue)
            {
                var goToSeminar = MessageBox.Show(
                    $"Seminar project \"{projectName}\" was created successfully.\n\nGo to Seminar Attendance now to manage attendee registration?",
                    "Seminar Project Created",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (goToSeminar == MessageBoxResult.Yes &&
                    Window.GetWindow(this) is MainWindow semMainWindow &&
                    semMainWindow.DataContext is BarangayMainViewModel semMainVm)
                {
                    semMainVm.ShowSeminarAttendanceCommand.Execute(null);
                }
                return;
            }

            var result = MessageBox.Show(
                $"Project \"{projectName}\" was created successfully.\n\nGo to Distribution now to add beneficiaries?",
                "Project Created",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result != MessageBoxResult.Yes) return;

            if (Window.GetWindow(this) is MainWindow mainWindow &&
                mainWindow.DataContext is BarangayMainViewModel mainVm)
            {
                mainVm.ShowDistributionCommand.Execute(null);
            }
        }

        private void OnBudgetPageUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _guidanceTimer?.Stop();
            _guidanceTimer = null;
            _viewModel.ProjectCreatedGoToDistribution -= OnProjectCreatedGoToDistribution;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            Loaded -= BudgetPage_Loaded;
            Unloaded -= OnBudgetPageUnloaded;
            SizeChanged -= BudgetPage_SizeChanged;
        }

        private void Browse_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var dialog = new Views.Dialog.BudgetListDialog(_viewModel);
            dialog.Owner = System.Windows.Window.GetWindow(this);
            dialog.ShowDialog();
        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {

        }
    }
}
