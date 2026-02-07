using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class EmployeesPage : UserControl
    {
        public EmployeesPage(User user)
        {
            InitializeComponent();
            DataContext = new EmployeesViewModel(user);
        }
    }
}