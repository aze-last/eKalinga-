using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class ReportsPage : UserControl
    {
        public ReportsPage(User currentUser)
        {
            InitializeComponent();
            DataContext = new ReportsViewModel(currentUser);
        }
    }
}
