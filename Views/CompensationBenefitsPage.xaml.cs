using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class CompensationBenefitsPage : UserControl
    {
        public CompensationBenefitsPage()
        {
            InitializeComponent();
            DataContext = new CompensationBenefitsViewModel();
        }
    }
}
