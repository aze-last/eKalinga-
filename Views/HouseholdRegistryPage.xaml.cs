using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class HouseholdRegistryPage : UserControl
    {
        public HouseholdRegistryPage()
        {
            InitializeComponent();
            DataContext = new HouseholdRegistryViewModel();
        }
    }
}
