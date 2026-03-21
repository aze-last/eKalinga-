using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class UserDialogWindow : Window
    {
        private UserDialogViewModel ViewModel => (UserDialogViewModel)DataContext;

        public UserDialogWindow(User? user = null)
        {
            InitializeComponent();
            DataContext = new UserDialogViewModel(user);
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                ViewModel.Password = passwordBox.Password;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}