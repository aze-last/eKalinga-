using AttendanceShiftingManagement.ViewModels;
using AttendanceShiftingManagement.Models;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class BeneficiaryVerificationPage : UserControl
    {
        public BeneficiaryVerificationPage(User currentUser)
        {
            InitializeComponent();
            DataContext = new BeneficiaryVerificationViewModel(currentUser);
        }
    }
}
