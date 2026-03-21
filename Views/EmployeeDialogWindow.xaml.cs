using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.Windows;

namespace AttendanceShiftingManagement.Views
{
    public partial class EmployeeDialogWindow : Window
    {
        public EmployeeDialogWindow(Employee? employee = null)
        {
            InitializeComponent();
            DataContext = new EmployeeDialogViewModel(employee);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}