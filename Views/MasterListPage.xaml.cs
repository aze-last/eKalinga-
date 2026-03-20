using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class MasterListPage : UserControl
    {
        public MasterListPage()
        {
            InitializeComponent();
            DataContext = new MasterListViewModel();
        }
    }
}
