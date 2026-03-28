using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class BarangayDashboardPage : UserControl
    {
        public BarangayDashboardPage()
        {
            InitializeComponent();
            DataContext = new BarangayDashboardViewModel();
        }
    }
}
