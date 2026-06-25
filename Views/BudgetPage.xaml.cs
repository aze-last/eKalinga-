using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.ComponentModel;
using System.Windows;
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
            _viewModel.ProjectCreatedGoToDistribution += OnProjectCreatedGoToDistribution;
            Unloaded += OnBudgetPageUnloaded;
            DataContext = _viewModel;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Handle property changes if needed
        }

        private void OnProjectCreatedGoToDistribution(string projectName)
        {
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
            _viewModel.ProjectCreatedGoToDistribution -= OnProjectCreatedGoToDistribution;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            Unloaded -= OnBudgetPageUnloaded;
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
