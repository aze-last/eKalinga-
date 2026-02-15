using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using System.Collections.Generic;
using System.Windows;

namespace AttendanceShiftingManagement.Views
{
    public partial class RoleSwitchWindow : Window
    {
        public int? SelectedUserId { get; private set; }

        public RoleSwitchWindow(User currentUser)
        {
            InitializeComponent();
            LoadUsers(currentUser.Id);
        }

        private void LoadUsers(int currentUserId)
        {
            List<User> users = RoleSwitchService.GetSwitchableUsers(currentUserId);
            UsersGrid.ItemsSource = users;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Switch_Click(object sender, RoutedEventArgs e)
        {
            if (UsersGrid.SelectedItem is not User selectedUser)
            {
                MessageBox.Show("Select an account to switch into.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SelectedUserId = selectedUser.Id;
            DialogResult = true;
            Close();
        }
    }
}
