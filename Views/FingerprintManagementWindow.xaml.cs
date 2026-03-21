using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.Windows;

namespace AttendanceShiftingManagement.Views
{
    public partial class FingerprintManagementWindow : Window
    {
        public FingerprintManagementWindow(User currentUser, User targetUser)
        {
            InitializeComponent();
            DataContext = new FingerprintManagementViewModel(currentUser, targetUser);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
