using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using AttendanceShiftingManagement.Views.Dialog;
using System.Windows;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class CashForWorkOcrPage : UserControl
    {
        public CashForWorkOcrPage(User currentUser)
        {
            InitializeComponent();
            DataContext = new CashForWorkOcrViewModel(currentUser);
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CashForWorkOcrViewModel vm)
            {
                var dialog = new CashForWorkEventListDialog(vm)
                {
                    Owner = Window.GetWindow(this)
                };
                dialog.ShowDialog();
            }
        }
    }
}
