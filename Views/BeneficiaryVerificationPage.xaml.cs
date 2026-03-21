using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class BeneficiaryVerificationPage : UserControl
    {
        public BeneficiaryVerificationPage()
        {
            InitializeComponent();
            DataContext = new BeneficiaryVerificationViewModel();
        }
    }
}
