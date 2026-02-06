using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Views;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class UsersViewModel : ObservableObject
    {
        private readonly AppDbContext _context;
        private ObservableCollection<User> _users;
        private User? _selectedUser;

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

        public UsersViewModel()
        {
            _context = new AppDbContext();
            _users = new ObservableCollection<User>();

            AddUserCommand = new RelayCommand(ExecuteAddUser);
            EditUserCommand = new RelayCommand(ExecuteEditUser);
            DeleteUserCommand = new RelayCommand(ExecuteDeleteUser);

            LoadUsers();
        }

        private void LoadUsers()
        {
            var users = _context.Users.ToList();
            Users = new ObservableCollection<User>(users);
        }

        private void ExecuteAddUser(object? parameter)
        {
            var dialog = new Views.UserDialogWindow();
            dialog.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is MainWindow);
            dialog.ShowDialog();

            if (dialog.DataContext is UserDialogViewModel vm && vm.DialogResult)
            {
                LoadUsers();
            }
        }

        private void ExecuteEditUser(object? parameter)
        {
            if (parameter is User user)
            {
                var dialog = new Views.UserDialogWindow(user);
                dialog.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is MainWindow);
                dialog.ShowDialog();

                if (dialog.DataContext is UserDialogViewModel vm && vm.DialogResult)
                {
                    LoadUsers();
                }
            }
        }

        private void ExecuteDeleteUser(object? parameter)
        {
            if (parameter is User user)
            {
                var result = MessageBox.Show($"Are you sure you want to delete user '{user.Username}'?",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var userToDelete = _context.Users.Find(user.Id);
                        if (userToDelete != null)
                        {
                            _context.Users.Remove(userToDelete);
                            _context.SaveChanges();
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