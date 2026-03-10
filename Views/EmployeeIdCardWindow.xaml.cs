using System;
using System.Windows;
using System.Windows.Input;
using AttendanceShiftingManagement.Models;

namespace AttendanceShiftingManagement.Views
{
    public partial class EmployeeIdCardWindow : Window
    {
        public EmployeeIdCardWindow(Employee employee)
        {
            InitializeComponent();

            EmpName.Text = employee.FullName.ToUpperInvariant();
            EmpPosition.Text = (employee.Position?.Name ?? "Position Not Set").ToUpperInvariant();
            EmpIdNumber.Text = BuildDisplayEmployeeId(employee);
            EmpDept.Text = BuildDepartmentName(employee);
            IssueDate.Text = employee.DateHired.ToString("MMM yyyy");
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Printing is not configured in this build. Use the badge window for on-screen presentation.",
                "Print Unavailable",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private static string BuildDisplayEmployeeId(Employee employee)
        {
            return $"EMP-{employee.Id:D4}";
        }

        private static string BuildDepartmentName(Employee employee)
        {
            if (employee.Position == null)
            {
                return "Unassigned";
            }

            return employee.Position.Area switch
            {
                PositionArea.DT => "Drive-Thru",
                PositionArea.POS => "Point of Sale",
                _ => employee.Position.Area.ToString()
            };
        }
    }
}
