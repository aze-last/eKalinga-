using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class MasterListPage : UserControl
    {
        public MasterListPage(User currentUser)
        {
            InitializeComponent();
            DataContext = new MasterListViewModel(currentUser);
        }
    }
}
