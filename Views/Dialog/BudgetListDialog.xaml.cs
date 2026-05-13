using System.Windows;
using System.Windows.Input;
using AttendanceShiftingManagement.ViewModels;

namespace AttendanceShiftingManagement.Views.Dialog
{
    public partial class BudgetListDialog : Window
    {
        public BudgetListDialog(BudgetViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is BudgetViewModel vm && vm.SelectedBudget != null)
            {
                DialogResult = true;
                Close();
            }
        }

        private void BudgetDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is BudgetViewModel vm && vm.SelectedBudget != null)
            {
                DialogResult = true;
                Close();
            }
        }
    }
}
