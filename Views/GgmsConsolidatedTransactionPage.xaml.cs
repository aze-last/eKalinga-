using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class GgmsConsolidatedTransactionPage : UserControl
    {
        public GgmsConsolidatedTransactionPage(User currentUser)
        {
            InitializeComponent();
            DataContext = new GgmsConsolidatedTransactionViewModel(currentUser);
        }
    }
}
