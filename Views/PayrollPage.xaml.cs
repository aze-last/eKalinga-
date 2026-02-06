using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class PayrollPage : UserControl
    {
        public PayrollPage()
        {
            InitializeComponent();
            DataContext = new PayrollViewModel();
        }
    }
}
