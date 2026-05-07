using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class ProjectDistributionPage : UserControl
    {
        public ProjectDistributionPage(User currentUser)
        {
            InitializeComponent();
            DataContext = new ProjectDistributionViewModel(currentUser);
        }

        private void ManageButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }
    }
}
