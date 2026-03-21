using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class PerformanceMetricsPage : UserControl
    {
        public PerformanceMetricsPage() : this(0)
        {
        }

        public PerformanceMetricsPage(int actorUserId)
        {
            InitializeComponent();
            DataContext = new PerformanceMetricsViewModel(actorUserId);
        }
    }
}
