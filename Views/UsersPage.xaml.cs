using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class UsersPage : UserControl
    {
        // Parameterless constructor kept for designer/runtime compatibility.
        public UsersPage() : this(new User { Id = 0, Username = "admin", Email = "admin@local", Role = UserRole.Admin, IsActive = true })
        {
        }

        public UsersPage(User currentUser)
        {
            InitializeComponent();
            DataContext = new UsersViewModel(currentUser);
        }
    }
}
