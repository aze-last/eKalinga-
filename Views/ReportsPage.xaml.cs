using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class ReportsPage : UserControl
    {
        private readonly ReportsViewModel _viewModel;

        public ReportsPage(User currentUser)
        {
            InitializeComponent();
            _viewModel = new ReportsViewModel(currentUser);
            DataContext = _viewModel;
        }

        private void ReportsPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Open the template selection dialog on load as per Pattern A
            OpenReportListDialog();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            OpenReportListDialog();
        }

        private void OpenReportListDialog()
        {
            var dialog = new Dialog.ReportListDialog(_viewModel)
            {
                Owner = Window.GetWindow(this)
            };
            dialog.ShowDialog();
        }
    }
}
