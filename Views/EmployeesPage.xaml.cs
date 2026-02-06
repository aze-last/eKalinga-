using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class EmployeesPage : UserControl
    {
        public EmployeesPage()
        {
            InitializeComponent();
            DataContext = new EmployeesViewModel();
        }
    }
}