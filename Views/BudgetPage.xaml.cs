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
                    cfwMainVm.ShowCashForWorkPayoutCommand.Execute(null);
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
