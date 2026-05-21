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

        private void Scanner_QrCodeScanned(string payload)
        {
            if (DataContext is CashForWorkOcrViewModel vm)
            {
                vm.ProcessPcScanCommand.Execute(payload);
            }
        }

        private void Scanner_Closed()
        {
            if (DataContext is CashForWorkOcrViewModel vm)
            {
                vm.IsPcScannerOpen = false;
            }
        }
    }
}
