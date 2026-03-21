using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class UserDialogViewModel : ObservableObject
    {
        private readonly AppDbContext _context;
        private readonly User? _existingUser;

        private string _dialogTitle = "Add New User";
        private string _username = string.Empty;
        private string _email = string.Empty;
        private string _password = string.Empty;
        private UserRole _selectedRole = UserRole.Crew;
        private bool _isActive = true;

        public string DialogTitle
        {
            get => _dialogTitle;
            set => SetProperty(ref _dialogTitle, value);
        }

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public ObservableCollection<UserRole> Roles { get; set; }

        public UserRole SelectedRole
        {
            get => _selectedRole;
            set => SetProperty(ref _selectedRole, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public ICommand SaveCommand { get; }

        public bool DialogResult { get; private set; }

        public UserDialogViewModel(User? user = null)
        {
            _context = new AppDbContext();
            _existingUser = user;

            Roles = new ObservableCollection<UserRole>
            {
                UserRole.Admin,
                UserRole.Manager,
                UserRole.HRStaff,
                UserRole.Crew
            };

            SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);

            if (_existingUser != null)
            {
                DialogTitle = "Edit User";
                Username = _existingUser.Username;
                Email = _existingUser.Email;
                SelectedRole = _existingUser.Role;
                IsActive = _existingUser.IsActive;
            }
        }

        private bool CanExecuteSave(object? parameter)
        {
            return !string.IsNullOrWhiteSpace(Username) &&
                   !string.IsNullOrWhiteSpace(Email) &&
                   (_existingUser != null || !string.IsNullOrWhiteSpace(Password));
        }

        private void ExecuteSave(object? parameter)
        {
            try
            {
                if (_existingUser != null)
                {
                    // Edit existing user
                    _existingUser.Username = Username;
                    _existingUser.Email = Email;
                    _existingUser.Role = SelectedRole;
                    _existingUser.IsActive = IsActive;

                    if (!string.IsNullOrWhiteSpace(Password))
                    {
                        _existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);
                    }

                    _existingUser.UpdatedAt = DateTime.Now;
                }
                else
                {
                    // Add new user
                    var newUser = new User
                    {
                        Username = Username,
                        Email = Email,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                        Role = SelectedRole,
                        IsActive = IsActive,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    _context.Users.Add(newUser);
                }

                _context.SaveChanges();
                DialogResult = true;

                MessageBox.Show("User saved successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                // Close the window
                Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.DataContext == this)?.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving user: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
