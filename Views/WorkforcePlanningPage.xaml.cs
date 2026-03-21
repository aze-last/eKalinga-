using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class WorkforcePlanningPage : UserControl
    {
        public WorkforcePlanningPage()
        {
            InitializeComponent();
            DataContext = new WorkforcePlanningViewModel();
        }
    }
}
