using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AttendanceShiftingManagement.Views
{
    public partial class AssistanceCaseManagementPage : UserControl
    {
        public AssistanceCaseManagementPage(User currentUser)
        {
            InitializeComponent();
            DataContext = new AssistanceCaseManagementViewModel(currentUser);
            CasesDataGrid.PreviewMouseLeftButtonUp += CasesDataGrid_PreviewMouseLeftButtonUp;
        }

        private void CasesDataGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not AssistanceCaseManagementViewModel viewModel)
            {
                return;
            }

            var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
            if (row?.DataContext is not AssistanceCaseListItem caseItem)
            {
                return;
            }

            if (viewModel.OpenCasePanelCommand.CanExecute(caseItem))
            {
                viewModel.OpenCasePanelCommand.Execute(caseItem);
            }
        }

        private static T? FindAncestor<T>(DependencyObject? source)
            where T : DependencyObject
        {
            while (source != null)
            {
                if (source is T match)
                {
                    return match;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return null;
        }
    }
}
