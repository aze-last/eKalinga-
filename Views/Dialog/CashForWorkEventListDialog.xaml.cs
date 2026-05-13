using System.Windows;
using System.Windows.Input;
using AttendanceShiftingManagement.ViewModels;

namespace AttendanceShiftingManagement.Views.Dialog
{
    public partial class CashForWorkEventListDialog : Window
    {
        public CashForWorkEventListDialog(CashForWorkOcrViewModel viewModel)
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
            if (DataContext is CashForWorkOcrViewModel vm && vm.SelectedEvent != null)
            {
                DialogResult = true;
                Close();
            }
        }

        private void EventsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is CashForWorkOcrViewModel vm && vm.SelectedEvent != null)
            {
                DialogResult = true;
                Close();
            }
        }
    }
}
