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
            // Handle property changes if needed
        }

        private void OnBudgetPageUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            Unloaded -= OnBudgetPageUnloaded;
        }
    }
}
