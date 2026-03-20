using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class EngagementWellbeingPage : UserControl
    {
        public EngagementWellbeingPage() : this(0)
        {
        }

        public EngagementWellbeingPage(int actorUserId)
        {
            InitializeComponent();
            DataContext = new EngagementWellbeingViewModel(actorUserId);
        }
    }
}
