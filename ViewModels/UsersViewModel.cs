using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Views;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class UsersViewModel : ObservableObject
    {
        private readonly AppDbContext _context;
        private readonly User _currentUser;
        private readonly Services.AuditService _auditService;

        private ObservableCollection<User> _users;
        private User? _selectedUser;

        public bool CanManageUsers => _currentUser.Role == UserRole.Admin;

        public ObservableCollection<User> Users
        {
            get => _users;
            set => SetProperty(ref _users, value);
        }

        public User? SelectedUser
        {
            get => _selectedUser;
            set => SetProperty(ref _selectedUser, value);
        }

        public ICommand AddUserCommand { get; }
        public ICommand EditUserCommand { get; }
        public ICommand DeleteUserCommand { get; }

        public UsersViewModel(User currentUser)
        {
            _currentUser = currentUser;
            _context = new AppDbContext();
            _auditService = new Services.AuditService(_context);
            _users = new ObservableCollection<User>();

            AddUserCommand = new RelayCommand(ExecuteAddUser);
            EditUserCommand = new RelayCommand(ExecuteEditUser);
            DeleteUserCommand = new RelayCommand(ExecuteDeleteUser);

            LoadUsers();
        }

        private void LoadUsers()
        {
            _context.ChangeTracker.Clear();
            var users = _context.Users
                .OrderBy(u => u.Id)
                .ToList();
            Users = new ObservableCollection<User>(users);
        }

        private bool EnsureAdminAccess()
        {
            if (CanManageUsers)
            {
                return true;
            }

            MessageBox.Show("Only Admin can manage user accounts.", "Access Denied",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private void ExecuteAddUser(object? parameter)
        {
            if (!EnsureAdminAccess())
            {
                return;
            }

            var dialog = new UserDialogWindow();
            dialog.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is MainWindow);
            dialog.ShowDialog();

            if (dialog.DataContext is UserDialogViewModel vm && vm.DialogResult)
            {
                _context.ChangeTracker.Clear();
                var created = _context.Users.FirstOrDefault(u => u.Username == vm.Username || u.Email == vm.Email);
                if (created != null)
                {
                    _auditService.LogActivity(
                        _currentUser.Id,
                        "UserCreated",
                        "User",
                        created.Id,
                        $"Created user '{created.Username}' ({created.Role}).");
                }

                LoadUsers();
            }
        }

        private void ExecuteEditUser(object? parameter)
        {
            if (!EnsureAdminAccess())
            {
                return;
            }

            if (parameter is User user)
            {
                var dialog = new UserDialogWindow(user);
                dialog.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is MainWindow);
                dialog.ShowDialog();

                if (dialog.DataContext is UserDialogViewModel vm && vm.DialogResult)
                {
                    _auditService.LogActivity(
                        _currentUser.Id,
                        "UserUpdated",
                        "User",
                        user.Id,
                        $"Updated user '{vm.Username}' ({vm.SelectedRole}).");

                    LoadUsers();
                }
            }
        }

        private void ExecuteDeleteUser(object? parameter)
        {
            if (!EnsureAdminAccess())
            {
                return;
            }

            if (parameter is User user)
            {
                if (user.Id == _currentUser.Id)
                {
                    MessageBox.Show("You cannot delete your own account while logged in.", "Blocked",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (user.Role == UserRole.Admin)
                {
                    int activeAdminCount = _context.Users.Count(u => u.Role == UserRole.Admin && u.IsActive);
                    if (activeAdminCount <= 1)
                    {
                        MessageBox.Show("At least one active Admin account must remain.", "Blocked",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                var result = MessageBox.Show($"Are you sure you want to delete user '{user.Username}'?",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var userToDelete = _context.Users.Find(user.Id);
                        if (userToDelete != null)
                        {
                            string deletedUsername = userToDelete.Username;
                            _context.Users.Remove(userToDelete);
                            _context.SaveChanges();

                            _auditService.LogActivity(
                                _currentUser.Id,
                                "UserDeleted",
                                "User",
                                user.Id,
                                $"Deleted user '{deletedUsername}'.");

                            LoadUsers();
                            MessageBox.Show("User deleted successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error deleting user: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}
