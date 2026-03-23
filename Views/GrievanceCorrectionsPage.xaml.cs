using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class GrievanceCorrectionsPage : UserControl
    {
        public GrievanceCorrectionsPage(User currentUser)
        {
            InitializeComponent();
            DataContext = new GrievanceCorrectionsViewModel(currentUser);
        }
    }
}
