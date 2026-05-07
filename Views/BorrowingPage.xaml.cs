using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class BorrowingPage : UserControl
    {
        public BorrowingPage(User currentUser)
        {
            InitializeComponent();
            DataContext = new BorrowingViewModel(currentUser);
        }
    }
}
