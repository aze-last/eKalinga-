using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.Windows;

namespace AttendanceShiftingManagement.Views
{
    public partial class HolidayDialogWindow : Window
    {
        public HolidayDialogWindow()
        {
            InitializeComponent();
            DataContext = new HolidayDialogViewModel(this);
        }

        public HolidayDialogWindow(Holiday holiday)
        {
            InitializeComponent();
            DataContext = new HolidayDialogViewModel(this, holiday);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
