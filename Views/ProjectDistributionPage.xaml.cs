using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using AttendanceShiftingManagement.Views.Dialog;
using System.Windows;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class ProjectDistributionPage : UserControl
    {
        public ProjectDistributionPage(User currentUser)
        {
            InitializeComponent();
            DataContext = new ProjectDistributionViewModel(currentUser);
            Loaded += ProjectDistributionPage_Loaded;
        }

        private void ProjectDistributionPage_Loaded(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as ProjectDistributionViewModel;
            if (viewModel?.SelectedProgram == null)
            {
                ShowProjectSelection();
            }
        }

        private void ChangeProject_Click(object sender, RoutedEventArgs e)
        {
            ShowProjectSelection();
        }

        private void ShowProjectSelection()
        {
            var dialog = new ProjectSelectionDialog
            {
                DataContext = this.DataContext,
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                // Project selection is handled via binding to SelectedProgramSummary
            }
        }

        private void PendingGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ShowDetailDialog();
        }

        private void ReleasedGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ShowDetailDialog();
        }

        private void ShowDetailDialog()
        {
            var dialog = new ProjectDistributionDetailDialog
            {
                DataContext = this.DataContext,
                Owner = Window.GetWindow(this)
            };

            dialog.ShowDialog();
        }
    }
}
