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

            var dialog = new AssistanceCaseListDialog(viewModel)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                // SelectedCase is already updated via binding in the dialog
            }
        }
    }
}
