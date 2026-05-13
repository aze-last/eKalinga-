using System.Windows;
using System.Windows.Input;
using AttendanceShiftingManagement.ViewModels;

namespace AttendanceShiftingManagement.Views.Dialog
{
    public partial class AssistanceCaseListDialog : Window
    {
        public AssistanceCaseListDialog(AssistanceCaseManagementViewModel viewModel)
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
            if (DataContext is AssistanceCaseManagementViewModel vm && vm.SelectedCase != null)
            {
                DialogResult = true;
                Close();
            }
        }

        private void CasesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is AssistanceCaseManagementViewModel vm && vm.SelectedCase != null)
            {
                DialogResult = true;
                Close();
            }
        }
    }
}
