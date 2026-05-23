using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class UserManagementViewModel : ObservableObject
    {
        private ObservableCollection<User> _users = new();
        private User? _selectedUser;
        private bool _isPermissionsPanelOpen;
        private UserPermission _editingPermissions = new();
        private string _searchText = string.Empty;

        // Create User Fields
        private bool _isCreateUserPanelOpen;
        private string _newUsername = string.Empty;
        private string _newEmail = string.Empty;
        private string _newPassword = string.Empty;
        private UserRole _newRole = UserRole.Crew;

        public User? CurrentUser { get; set; }

        public ObservableCollection<User> Users
        {
            get => _users;
            set => SetProperty(ref _users, value);
        }

        public User? SelectedUser
        {
            get => _selectedUser;
            set
            {
                if (SetProperty(ref _selectedUser, value))
                {
                    OnPropertyChanged(nameof(CanManagePermissions));
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    RefreshUsers();
                }
            }
        }

        public UserPermission EditingPermissions
        {
            get => _editingPermissions;
            set => SetProperty(ref _editingPermissions, value);
        }

        public bool CanManagePermissions => SelectedUser != null && SelectedUser.Role != UserRole.SuperAdmin;

        // Create User Properties
        public bool IsCreateUserPanelOpen
        {
            get => _isCreateUserPanelOpen;
            set 
            {
                if (SetProperty(ref _isCreateUserPanelOpen, value))
                {
                    OnPropertyChanged(nameof(IsOverlayOpen));
                }
            }
        }

        public bool IsPermissionsPanelOpen
        {
            get => _isPermissionsPanelOpen;
            set 
            {
                if (SetProperty(ref _isPermissionsPanelOpen, value))
                {
                    OnPropertyChanged(nameof(IsOverlayOpen));
                }
            }
        }

        public bool IsOverlayOpen => IsPermissionsPanelOpen || IsCreateUserPanelOpen;

        public string NewUsername
        {
            get => _newUsername;
            set => SetProperty(ref _newUsername, value);
        }

        public string NewEmail
        {
            get => _newEmail;
            set => SetProperty(ref _newEmail, value);
        }

        public string NewPassword
        {
            get => _newPassword;
            set => SetProperty(ref _newPassword, value);
        }

        public UserRole NewRole
        {
            get => _newRole;
            set => SetProperty(ref _newRole, value);
        }

        public Array AvailableRoles => Enum.GetValues(typeof(UserRole));

        public ICommand RefreshCommand { get; }
        public ICommand OpenPermissionsCommand { get; }
        public ICommand SavePermissionsCommand { get; }
        public ICommand ClosePermissionsCommand { get; }
        public ICommand ToggleUserStatusCommand { get; }
        
        public ICommand OpenCreateUserCommand { get; }
        public ICommand CloseCreateUserCommand { get; }
        public ICommand SaveNewUserCommand { get; }

        public UserManagementViewModel()
        {
            RefreshCommand = new RelayCommand(_ => RefreshUsers());
            OpenPermissionsCommand = new RelayCommand(OpenPermissions, CanManagePermissionsUser);
            SavePermissionsCommand = new RelayCommand(_ => SavePermissions());
            ClosePermissionsCommand = new RelayCommand(_ => IsPermissionsPanelOpen = false);
            ToggleUserStatusCommand = new RelayCommand(ToggleUserStatus, CanManagePermissionsUser);
            
            OpenCreateUserCommand = new RelayCommand(_ => OpenCreateUser());
            CloseCreateUserCommand = new RelayCommand(_ => IsCreateUserPanelOpen = false);
            SaveNewUserCommand = new RelayCommand(_ => SaveNewUser(), _ => CanSaveNewUser());

            RefreshUsers();
        }

        private bool CanManagePermissionsUser(object? param)
        {
            var user = param as User ?? SelectedUser;
            return user != null && user.Role != UserRole.SuperAdmin;
        }

        public void RefreshUsers()
        {
            using var context = new AppDbContext();
            var query = context.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var term = SearchText.Trim().ToLower();
                query = query.Where(u => u.Username.ToLower().Contains(term) || u.Email.ToLower().Contains(term));
            }

            Users = new ObservableCollection<User>(query.OrderBy(u => u.Username).ToList());
        }

        private void OpenPermissions(object? param)
        {
            var user = param as User ?? SelectedUser;
            if (user == null || user.Role == UserRole.SuperAdmin) return;

            SelectedUser = user;

            using var context = new AppDbContext();
            var permissions = context.UserPermissions
                .FirstOrDefault(p => p.UserId == SelectedUser.Id);

            if (permissions == null)
            {
                EditingPermissions = new UserPermission { UserId = SelectedUser.Id };
            }
            else
            {
                // Create a clone for editing
                EditingPermissions = new UserPermission
                {
                    Id = permissions.Id,
                    UserId = permissions.UserId,
                    CanAccessDashboard = permissions.CanAccessDashboard,
                    CanAccessMasterList = permissions.CanAccessMasterList,
                    CanAccessAssistanceCases = permissions.CanAccessAssistanceCases,
                    CanAccessBudget = permissions.CanAccessBudget,
                    CanAccessDistribution = permissions.CanAccessDistribution,
                    CanAccessCashForWork = permissions.CanAccessCashForWork,
                    CanAccessBorrowing = permissions.CanAccessBorrowing,
                    CanAccessReports = permissions.CanAccessReports,
                    CanAccessGgmsTransactions = permissions.CanAccessGgmsTransactions
                };
            }

            IsPermissionsPanelOpen = true;
        }

        private void SavePermissions()
        {
            using var context = new AppDbContext();
            
            var existing = context.UserPermissions.FirstOrDefault(p => p.UserId == EditingPermissions.UserId);
            
            if (existing == null)
            {
                EditingPermissions.UpdatedAt = DateTime.Now;
                context.UserPermissions.Add(EditingPermissions);
            }
            else
            {
                existing.CanAccessDashboard = EditingPermissions.CanAccessDashboard;
                existing.CanAccessMasterList = EditingPermissions.CanAccessMasterList;
                existing.CanAccessAssistanceCases = EditingPermissions.CanAccessAssistanceCases;
                existing.CanAccessBudget = EditingPermissions.CanAccessBudget;
                existing.CanAccessDistribution = EditingPermissions.CanAccessDistribution;
                existing.CanAccessCashForWork = EditingPermissions.CanAccessCashForWork;
                existing.CanAccessBorrowing = EditingPermissions.CanAccessBorrowing;
                existing.CanAccessReports = EditingPermissions.CanAccessReports;
                existing.CanAccessGgmsTransactions = EditingPermissions.CanAccessGgmsTransactions;
                existing.UpdatedAt = DateTime.Now;
                context.UserPermissions.Update(existing);
            }

            context.SaveChanges();

            if (CurrentUser != null)
            {
                var auditService = new AuditService(context);
                auditService.LogActivity(
                    CurrentUser.Id,
                    "PermissionsUpdated",
                    "UserPermission",
                    EditingPermissions.UserId,
                    $"User '{CurrentUser.Username}' updated permissions for user ID {EditingPermissions.UserId}.");
            }

            IsPermissionsPanelOpen = false;
            
            // Reload if current user was changed (optional, usually takes effect on next login as per prompt)
        }

        private void ToggleUserStatus(object? param)
        {
            var userTarget = param as User ?? SelectedUser;
            if (userTarget == null || userTarget.Role == UserRole.SuperAdmin) return;

            using var context = new AppDbContext();
            var user = context.Users.Find(userTarget.Id);
            if (user != null)
            {
                user.IsActive = !user.IsActive;
                user.UpdatedAt = DateTime.Now;

                if (CurrentUser != null)
                {
                    var auditService = new AuditService(context);
                    string action = user.IsActive ? "UserActivated" : "UserDeactivated";
                    auditService.LogActivity(
                        CurrentUser.Id,
                        action,
                        "User",
                        user.Id,
                        $"User '{CurrentUser.Username}' {(user.IsActive ? "activated" : "deactivated")} user '{user.Username}'.");
                }

                context.SaveChanges();
                RefreshUsers();
            }
        }

        private void OpenCreateUser()
        {
            NewUsername = string.Empty;
            NewEmail = string.Empty;
            NewPassword = string.Empty;
            NewRole = UserRole.Crew;
            IsCreateUserPanelOpen = true;
        }

        private bool CanSaveNewUser()
        {
            return !string.IsNullOrWhiteSpace(NewUsername) &&
                   !string.IsNullOrWhiteSpace(NewEmail) &&
                   !string.IsNullOrWhiteSpace(NewPassword);
        }

        private void SaveNewUser()
        {
            if (!CanSaveNewUser()) return;

            using var context = new AppDbContext();

            // Check duplicates
            if (context.Users.Any(u => u.Username == NewUsername || u.Email == NewEmail))
            {
                System.Windows.MessageBox.Show("Username or Email already exists.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            var newUser = new User
            {
                Username = NewUsername,
                Email = NewEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(NewPassword),
                Role = NewRole,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            context.Users.Add(newUser);
            context.SaveChanges();

            // Add default blank permissions
            var newPermissions = new UserPermission
            {
                UserId = newUser.Id,
                UpdatedAt = DateTime.Now
            };
            context.UserPermissions.Add(newPermissions);
            context.SaveChanges();

            var auditService = new AuditService(context);
            auditService.LogActivity(
                userId: CurrentUser?.Id ?? 0,
                action: "Created User",
                entity: "User",
                entityId: newUser.Id,
                details: $"Created new user {newUser.Username} with role {newUser.Role}"
            );

            IsCreateUserPanelOpen = false;
            RefreshUsers();
        }
    }
}
