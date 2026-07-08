using AttendanceShiftingManagement.Data;
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
            
            // Note: In a fully DI architecture, LocalDbContext should be injected.
            // For now, instantiate directly as done throughout the app where DI is missing.
            var context = new LocalDbContext();
            
            DataContext = new AssistanceCaseManagementViewModel(currentUser, context);
        }
    }
}
