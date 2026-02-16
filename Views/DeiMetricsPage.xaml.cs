using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class DeiMetricsPage : UserControl
    {
        public DeiMetricsPage()
        {
            InitializeComponent();
            DataContext = new DeiMetricsViewModel();
        }
    }
}
