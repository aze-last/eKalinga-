using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class BudgetPage : UserControl
    {
        public BudgetPage(User currentUser)
        {
            InitializeComponent();
            DataContext = new BudgetViewModel(currentUser);
        }
    }
}
