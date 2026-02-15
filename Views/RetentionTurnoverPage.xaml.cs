using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class RetentionTurnoverPage : UserControl
    {
        public RetentionTurnoverPage(int recordedByUserId)
        {
            InitializeComponent();
            DataContext = new RetentionTurnoverViewModel(recordedByUserId);
        }
    }
}
