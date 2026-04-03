using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AttendanceShiftingManagement.Views
{
    public partial class BudgetPage : UserControl
    {
        private readonly BudgetViewModel _viewModel;

        public BudgetPage(User currentUser)
        {
            InitializeComponent();
            _viewModel = new BudgetViewModel(currentUser);
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            Unloaded += OnBudgetPageUnloaded;
            DataContext = _viewModel;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(BudgetViewModel.IsAnySetupPanelOpen) || !_viewModel.IsAnySetupPanelOpen)
            {
                return;
            }

            Dispatcher.BeginInvoke(
                () => BudgetScrollViewer.ScrollToTop(),
                DispatcherPriority.Background);
        }

        private void OnBudgetPageUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            Unloaded -= OnBudgetPageUnloaded;
        }
    }
}
