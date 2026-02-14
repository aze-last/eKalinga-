using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class PayrollPage : UserControl
    {
        public PayrollPage(int generatedByUserId = 1)
        {
            InitializeComponent();
            DataContext = new PayrollViewModel(generatedByUserId);
        }
    }
}
