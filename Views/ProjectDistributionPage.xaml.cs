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
    }
}
