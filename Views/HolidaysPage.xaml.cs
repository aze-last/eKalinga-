using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class HolidaysPage : UserControl
    {
        public HolidaysPage()
        {
            InitializeComponent();
            DataContext = new HolidaysViewModel();
        }
    }
}