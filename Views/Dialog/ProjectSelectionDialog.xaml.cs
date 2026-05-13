using System.Windows;
using System.Windows.Input;

namespace AttendanceShiftingManagement.Views.Dialog
{
    public partial class ProjectSelectionDialog : Window
    {
        public ProjectSelectionDialog()
        {
            InitializeComponent();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            if (ProjectDataGrid.SelectedItem != null)
            {
                DialogResult = true;
                Close();
            }
        }

        private void ProjectDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ProjectDataGrid.SelectedItem != null)
            {
                DialogResult = true;
                Close();
            }
        }
    }
}
