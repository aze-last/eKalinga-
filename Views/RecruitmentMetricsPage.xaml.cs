using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class RecruitmentMetricsPage : UserControl
    {
        public RecruitmentMetricsPage()
        {
            InitializeComponent();
            DataContext = new RecruitmentMetricsViewModel();
        }
    }
}
