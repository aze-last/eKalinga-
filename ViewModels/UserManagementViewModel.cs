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

        // Pagination Fields
        private int _currentPage = 1;
        private int _pageSize = 15;
        private int _totalUsers = 0;
        private int _totalPages = 1;

        // Create/Edit User Fields
        private bool _isCreateUserPanelOpen;
        private bool _isEditMode;
        private int _editingUserId;
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
                    OnPropertyChanged(nameof(CanEditUser));
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
                    CurrentPage = 1;
                    RefreshUsers();
                }
            }
        }

        public int CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        public int TotalPages
        {
            get => _totalPages;
            set => SetProperty(ref _totalPages, value);
        }

        public UserPermission EditingPermissions
        {
            get => _editingPermissions;
            set => SetProperty(ref _editingPermissions, value);
        }

        public bool CanManagePermissions => SelectedUser != null && SelectedUser.Role != UserRole.SuperAdmin;
        public bool CanEditUser => SelectedUser != null && SelectedUser.Role != UserRole.SuperAdmin;

        // Create/Edit User Properties
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

        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                if (SetProperty(ref _isEditMode, value))
                {
                    OnPropertyChanged(nameof(PanelTitle));
                    OnPropertyChanged(nameof(PasswordHint));
                }
            }
        }

        public string PanelTitle => IsEditMode ? "EDIT USER" : "CREATE NEW USER";
        public string PasswordHint => IsEditMode ? "New Password (leave blank to keep current)" : "Password";

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
        public ICommand OpenEditUserCommand { get; }
        public ICommand CloseCreateUserCommand { get; }
        public ICommand SaveNewUserCommand { get; }

        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand { get; }

        public UserManagementViewModel()
        {
            RefreshCommand = new RelayCommand(_ => { CurrentPage = 1; RefreshUsers(); });
            OpenPermissionsCommand = new RelayCommand(OpenPermissions, CanManagePermissionsUser);
            SavePermissionsCommand = new RelayCommand(_ => SavePermissions());
            ClosePermissionsCommand = new RelayCommand(_ => IsPermissionsPanelOpen = false);
            ToggleUserStatusCommand = new RelayCommand(ToggleUserStatus, CanManagePermissionsUser);
            
            OpenCreateUserCommand = new RelayCommand(_ => OpenCreateUser());
            OpenEditUserCommand = new RelayCommand(OpenEditUser, _ => CanEditUser);
            CloseCreateUserCommand = new RelayCommand(_ => IsCreateUserPanelOpen = false);
            SaveNewUserCommand = new RelayCommand(_ => SaveNewUser(), _ => CanSaveNewUser());

            PreviousPageCommand = new RelayCommand(_ => { if (CurrentPage > 1) { CurrentPage--; RefreshUsers(); } });
            NextPageCommand = new RelayCommand(_ => { if (CurrentPage < TotalPages) { CurrentPage++; RefreshUsers(); } });

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

            int totalCount = query.Count();
            _totalUsers = totalCount;
            TotalPages = (int)Math.Ceiling(totalCount / (double)_pageSize);
            if (TotalPages == 0) TotalPages = 1;
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;

            Users = new ObservableCollection<User>(query.OrderBy(u => u.Username)
                                                        .Skip((CurrentPage - 1) * _pageSize)
                                                        .Take(_pageSize)
                                                        .ToList());
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
            IsEditMode = false;
            _editingUserId = 0;
            NewUsername = string.Empty;
            NewEmail = string.Empty;
            NewPassword = string.Empty;
            NewRole = UserRole.Crew;
            IsCreateUserPanelOpen = true;
        }

        private void OpenEditUser(object? param)
        {
            var user = param as User ?? SelectedUser;
            if (user == null || user.Role == UserRole.SuperAdmin) return;

            IsEditMode = true;
            _editingUserId = user.Id;
            NewUsername = user.Username;
            NewEmail = user.Email;
            NewPassword = string.Empty; // Leave blank to not update
            NewRole = user.Role;
            IsCreateUserPanelOpen = true;
        }

        private bool CanSaveNewUser()
        {
            if (string.IsNullOrWhiteSpace(NewUsername) || string.IsNullOrWhiteSpace(NewEmail))
                return false;

            if (!NewEmail.Contains("@") || !NewEmail.Contains("."))
                return false;

            if (!IsEditMode && string.IsNullOrWhiteSpace(NewPassword))
                return false; // Password required for new users

            if (!IsEditMode && NewPassword.Length < 6)
                return false; // Simple length validation

            if (IsEditMode && !string.IsNullOrWhiteSpace(NewPassword) && NewPassword.Length < 6)
                return false;

            return true;
        }

        private void SaveNewUser()
        {
            if (!CanSaveNewUser())
            {
                System.Windows.MessageBox.Show("Please ensure all fields are filled correctly. Passwords must be at least 6 characters.", "Validation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            using var context = new AppDbContext();

            if (IsEditMode)
            {
                var existingUser = context.Users.Find(_editingUserId);
                if (existingUser == null) return;

                // Check duplicates (excluding current user)
                if (context.Users.Any(u => u.Id != _editingUserId && (u.Username == NewUsername || u.Email == NewEmail)))
                {
                    System.Windows.MessageBox.Show("Username or Email already exists for another user.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }

                existingUser.Username = NewUsername;
                existingUser.Email = NewEmail;
                existingUser.Role = NewRole;
                existingUser.UpdatedAt = DateTime.Now;

                if (!string.IsNullOrWhiteSpace(NewPassword))
                {
                    existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(NewPassword);
                }

                context.SaveChanges();

                var auditService = new AuditService(context);
                auditService.LogActivity(
                    userId: CurrentUser?.Id ?? 0,
                    action: "Edited User",
                    entity: "User",
                    entityId: existingUser.Id,
                    details: $"Updated user {existingUser.Username}"
                );
            }
            else
            {
                // Check duplicates for new user
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
            }

            IsCreateUserPanelOpen = false;
            RefreshUsers();
        }
    }
}