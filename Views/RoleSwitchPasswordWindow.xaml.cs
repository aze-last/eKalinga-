using AttendanceShiftingManagement.Models;
using System.Windows;

namespace AttendanceShiftingManagement.Views
{
    public partial class RoleSwitchPasswordWindow : Window
    {
        public string EnteredPassword { get; private set; } = string.Empty;

        public RoleSwitchPasswordWindow(User targetUser)
        {
            InitializeComponent();
            PromptText.Text = $"Enter the password for '{targetUser.Username}' before switching into the Admin account.";
            Loaded += (_, _) => PasswordInput.Focus();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            EnteredPassword = PasswordInput.Password;
            DialogResult = true;
            Close();
        }
    }
}
