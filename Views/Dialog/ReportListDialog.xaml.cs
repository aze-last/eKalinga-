using AttendanceShiftingManagement.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace AttendanceShiftingManagement.Views.Dialog
{
    public partial class ReportListDialog : Window
    {
        private readonly ReportsViewModel _viewModel;

        public ReportListDialog(ReportsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        private void ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.SelectedReportType != null)
            {
                DialogResult = true;
                Close();
            }
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedReportType != null)
            {
                DialogResult = true;
                Close();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
