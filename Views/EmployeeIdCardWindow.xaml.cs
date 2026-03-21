using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AttendanceShiftingManagement.Models;
using System.IO;

namespace AttendanceShiftingManagement.Views
{
    public partial class EmployeeIdCardWindow : Window
    {
        public EmployeeIdCardWindow(Employee employee, string? photoPath = null)
        {
            ArgumentNullException.ThrowIfNull(employee);

            InitializeComponent();

            EmpName.Text = BuildDisplayName(employee);
            EmpPosition.Text = BuildDisplayPosition(employee);
            EmpIdNumber.Text = BuildDisplayEmployeeId(employee);
            EmpDept.Text = BuildDepartmentName(employee);
            IssueDate.Text = BuildDisplayHireDate(employee);
            LoadPhoto(photoPath);
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

        private static string BuildDisplayName(Employee employee)
        {
            return string.IsNullOrWhiteSpace(employee.FullName)
                ? "UNNAMED EMPLOYEE"
                : employee.FullName.Trim().ToUpperInvariant();
        }

        private static string BuildDisplayPosition(Employee employee)
        {
            var positionName = employee.Position?.Name;
            return string.IsNullOrWhiteSpace(positionName)
                ? "POSITION NOT SET"
                : positionName.Trim().ToUpperInvariant();
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

        private static string BuildDisplayHireDate(Employee employee)
        {
            return employee.DateHired == default
                ? "N/A"
                : employee.DateHired.ToString("MMM yyyy");
        }

        private void LoadPhoto(string? photoPath)
        {
            var image = BuildImage(photoPath);
            if (image == null)
            {
                EmpPhotoImage.Source = null;
                EmpPhotoImage.Visibility = Visibility.Collapsed;
                EmpPhotoPlaceholder.Visibility = Visibility.Visible;
                return;
            }

            EmpPhotoImage.Source = image;
            EmpPhotoImage.Visibility = Visibility.Visible;
            EmpPhotoPlaceholder.Visibility = Visibility.Collapsed;
        }

        private static ImageSource? BuildImage(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            using var stream = File.OpenRead(path);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}
