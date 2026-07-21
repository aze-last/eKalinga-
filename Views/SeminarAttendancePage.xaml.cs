using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using AttendanceShiftingManagement.Views.Dialog;
using System.Windows;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class SeminarAttendancePage : UserControl
    {
        public SeminarAttendancePage(User currentUser)
        {
            InitializeComponent();
            DataContext = new SeminarAttendanceViewModel(currentUser);
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SeminarAttendanceViewModel vm)
            {
                var dialog = new SeminarEventListDialog(vm)
                {
                    Owner = Window.GetWindow(this)
                };
                dialog.ShowDialog();
            }
        }

        private void Scanner_QrCodeScanned(string payload)
        {
            if (DataContext is SeminarAttendanceViewModel vm)
            {
                vm.ProcessPcScanCommand.Execute(payload);
            }
        }

        private void Scanner_Closed()
        {
            if (DataContext is SeminarAttendanceViewModel vm)
            {
                vm.IsPcScannerOpen = false;
            }
        }
    }
}
