using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
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
    }
}
