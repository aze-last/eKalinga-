using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using AttendanceShiftingManagement.Views.Dialog;
using System.Windows;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class AssistanceCaseManagementPage : UserControl
    {
        public AssistanceCaseManagementPage(User currentUser)
        {
            InitializeComponent();
            DataContext = new AssistanceCaseManagementViewModel(currentUser);
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not AssistanceCaseManagementViewModel viewModel)
            {
                return;
            }

            viewModel.IsBrowsePanelOpen = true;
            try
            {
                var dialog = new AssistanceCaseListDialog(viewModel)
                {
                    Owner = Window.GetWindow(this)
                };

                if (dialog.ShowDialog() == true)
                {
                    // SelectedCase is already updated via binding in the dialog.
                    // The center view will now automatically show the summary of the selected case.
                }
            }
            finally
            {
                viewModel.IsBrowsePanelOpen = false;
            }
        }
        private void Scanner_Closed()
        {
            if (DataContext is AssistanceCaseManagementViewModel vm)
            {
                vm.IsPcScannerOpen = false;
            }
        }

        private void Scanner_QrCodeScanned(string payload)
        {
            if (DataContext is AssistanceCaseManagementViewModel vm)
            {
                vm.ProcessPcScanCommand.Execute(payload);
            }
        }
    }
}
