using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AttendanceShiftingManagement.ViewModels
{
    public class ProfileSettingsViewModel : ObservableObject
    {
        private readonly AppDbContext _context;
        private readonly int _userId;
        private UserProfile _profile = new UserProfile();
        private UserPreference _preferences = new UserPreference();
        private User? _user;
        private Employee? _employee;

        public bool IsManager { get; }
        public bool CanEditContactInfo => IsManager;

        public ObservableCollection<SelectablePosition> Positions { get; } = new();
        public ObservableCollection<string> ShiftBlocks { get; } = new() { "Any", "AM", "PM", "Mid", "Night" };
        public ObservableCollection<string> DefaultViews { get; } = new() { "Dashboard", "Weekly Schedule", "Shift Planning", "Attendance Logs", "Employee List" };
        public ObservableCollection<string> ReportFormats { get; } = new() { "CSV", "PDF" };

        private ImageSource? _photoImage;
        public ImageSource? PhotoImage
        {
            get => _photoImage;
            set => SetProperty(ref _photoImage, value);
        }

        private string _photoPath = string.Empty;
        public string PhotoPath
        {
            get => _photoPath;
            set => SetProperty(ref _photoPath, value);
        }

        private string _fullName = string.Empty;
        public string FullName
        {
            get => _fullName;
            set => SetProperty(ref _fullName, value);
        }

        private string _nickname = string.Empty;
        public string Nickname
        {
            get => _nickname;
            set => SetProperty(ref _nickname, value);
        }

        private string _email = string.Empty;
        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        private string _phone = string.Empty;
        public string Phone
        {
            get => _phone;
            set => SetProperty(ref _phone, value);
        }

        private string _address = string.Empty;
        public string Address
        {
            get => _address;
            set => SetProperty(ref _address, value);
        }

        private string _emergencyContactName = string.Empty;
        public string EmergencyContactName
        {
            get => _emergencyContactName;
            set => SetProperty(ref _emergencyContactName, value);
        }

        private string _emergencyContactPhone = string.Empty;
        public string EmergencyContactPhone
        {
            get => _emergencyContactPhone;
            set => SetProperty(ref _emergencyContactPhone, value);
        }

        private string _selectedShiftBlock = "Any";
        public string SelectedShiftBlock
        {
            get => _selectedShiftBlock;
            set => SetProperty(ref _selectedShiftBlock, value);
        }

        public bool IsMondayOff { get => _isMondayOff; set => SetProperty(ref _isMondayOff, value); }
        public bool IsTuesdayOff { get => _isTuesdayOff; set => SetProperty(ref _isTuesdayOff, value); }
        public bool IsWednesdayOff { get => _isWednesdayOff; set => SetProperty(ref _isWednesdayOff, value); }
        public bool IsThursdayOff { get => _isThursdayOff; set => SetProperty(ref _isThursdayOff, value); }
        public bool IsFridayOff { get => _isFridayOff; set => SetProperty(ref _isFridayOff, value); }
        public bool IsSaturdayOff { get => _isSaturdayOff; set => SetProperty(ref _isSaturdayOff, value); }
        public bool IsSundayOff { get => _isSundayOff; set => SetProperty(ref _isSundayOff, value); }

        private bool _isMondayOff;
        private bool _isTuesdayOff;
        private bool _isWednesdayOff;
        private bool _isThursdayOff;
        private bool _isFridayOff;
        private bool _isSaturdayOff;
        private bool _isSundayOff;

        private bool _notifyLeave = true;
        public bool NotifyLeave { get => _notifyLeave; set => SetProperty(ref _notifyLeave, value); }
        private bool _notifyShift = true;
        public bool NotifyShift { get => _notifyShift; set => SetProperty(ref _notifyShift, value); }
        private bool _notifyAnnouncement = true;
        public bool NotifyAnnouncement { get => _notifyAnnouncement; set => SetProperty(ref _notifyAnnouncement, value); }

        private bool _notifyInApp = true;
        public bool NotifyInApp { get => _notifyInApp; set => SetProperty(ref _notifyInApp, value); }
        private bool _notifyEmail;
        public bool NotifyEmail { get => _notifyEmail; set => SetProperty(ref _notifyEmail, value); }

        private string _defaultView = "Dashboard";
        public string DefaultView
        {
            get => _defaultView;
            set => SetProperty(ref _defaultView, value);
        }

        private string _reportFormat = "CSV";
        public string ReportFormat
        {
            get => _reportFormat;
            set => SetProperty(ref _reportFormat, value);
        }

        private string _approvalSignature = string.Empty;
        public string ApprovalSignature
        {
            get => _approvalSignature;
            set => SetProperty(ref _approvalSignature, value);
        }

        private bool _autoNotifyOnApproval = true;
        public bool AutoNotifyOnApproval
        {
            get => _autoNotifyOnApproval;
            set => SetProperty(ref _autoNotifyOnApproval, value);
        }

        private string _currentPassword = string.Empty;
        public string CurrentPassword
        {
            get => _currentPassword;
            set => SetProperty(ref _currentPassword, value);
        }

        private string _newPassword = string.Empty;
        public string NewPassword
        {
            get => _newPassword;
            set => SetProperty(ref _newPassword, value);
        }

        private string _confirmPassword = string.Empty;
        public string ConfirmPassword
        {
            get => _confirmPassword;
            set => SetProperty(ref _confirmPassword, value);
        }

        public ICommand BrowsePhotoCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ChangePasswordCommand { get; }
        public event Action? ProfileUpdated;

        public ProfileSettingsViewModel(User user)
        {
            _context = new AppDbContext();
            _userId = user.Id;
            IsManager = user.Role == UserRole.Manager;

            BrowsePhotoCommand = new RelayCommand(_ => BrowsePhoto());
            SaveCommand = new RelayCommand(_ => SaveProfile());
            CancelCommand = new RelayCommand(_ => LoadProfile());
            ChangePasswordCommand = new RelayCommand(_ => ChangePassword());

            LoadProfile();
        }

        private void LoadProfile()
        {
            _user = _context.Users.Include(u => u.Employee).FirstOrDefault(u => u.Id == _userId);
            _employee = _user?.Employee;

            _profile = _context.UserProfiles.FirstOrDefault(p => p.UserId == _userId) ?? new UserProfile
            {
                UserId = _userId,
                FullName = _employee?.FullName ?? string.Empty,
                Phone = string.Empty,
                Address = string.Empty,
                Nickname = string.Empty
            };

            _preferences = _context.UserPreferences.FirstOrDefault(p => p.UserId == _userId) ?? new UserPreference
            {
                UserId = _userId
            };

            Positions.Clear();
            foreach (var pos in _context.Positions.OrderBy(p => p.Name))
            {
                Positions.Add(new SelectablePosition(pos.Id, pos.Name));
            }

            FullName = string.IsNullOrWhiteSpace(_profile.FullName) ? _employee?.FullName ?? string.Empty : _profile.FullName;
            Nickname = _profile.Nickname;
            Email = _user?.Email ?? string.Empty;
            Phone = _profile.Phone;
            Address = _profile.Address;
            EmergencyContactName = _profile.EmergencyContactName;
            EmergencyContactPhone = _profile.EmergencyContactPhone;

            PhotoPath = _profile.PhotoPath;
            PhotoImage = BuildImage(PhotoPath);

            SelectedShiftBlock = _preferences.PreferredShiftBlock;
            ParseDaysOff(_preferences.PreferredDaysOff);
            ApplySelectedPositions(_preferences.PreferredPositions);
            ApplyNotificationPrefs(_preferences.NotificationTypes, _preferences.NotificationChannels);

            DefaultView = _preferences.DefaultView;
            ReportFormat = _preferences.ReportFormat;
            ApprovalSignature = _preferences.ApprovalSignature;
            AutoNotifyOnApproval = _preferences.AutoNotifyOnApproval;

            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
        }

        private void SaveProfile()
        {
            if (_user == null)
            {
                MessageBox.Show("Unable to load profile.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _profile.UserId = _userId;
            _profile.FullName = FullName.Trim();
            _profile.Nickname = Nickname.Trim();
            _profile.Phone = Phone.Trim();
            _profile.Address = Address.Trim();
            _profile.EmergencyContactName = EmergencyContactName.Trim();
            _profile.EmergencyContactPhone = EmergencyContactPhone.Trim();
            _profile.PhotoPath = PhotoPath.Trim();
            _profile.UpdatedAt = DateTime.Now;

            if (_profile.Id == 0)
            {
                _context.UserProfiles.Add(_profile);
            }
            else
            {
                _context.UserProfiles.Update(_profile);
            }

            if (IsManager)
            {
                _user.Email = Email.Trim();
                _context.Users.Update(_user);
            }

            if (_employee != null && !string.IsNullOrWhiteSpace(FullName))
            {
                _employee.FullName = FullName.Trim();
                _context.Employees.Update(_employee);
            }

            _preferences.UserId = _userId;
            _preferences.PreferredShiftBlock = SelectedShiftBlock;
            _preferences.PreferredDaysOff = BuildDaysOff();
            _preferences.PreferredPositions = BuildPositions();
            _preferences.NotificationTypes = BuildNotificationTypes();
            _preferences.NotificationChannels = BuildNotificationChannels();
            _preferences.DefaultView = DefaultView;
            _preferences.ReportFormat = ReportFormat;
            _preferences.ApprovalSignature = ApprovalSignature.Trim();
            _preferences.AutoNotifyOnApproval = AutoNotifyOnApproval;
            _preferences.UpdatedAt = DateTime.Now;

            if (_preferences.Id == 0)
            {
                _context.UserPreferences.Add(_preferences);
            }
            else
            {
                _context.UserPreferences.Update(_preferences);
            }

            _context.SaveChanges();
            MessageBox.Show("Profile updated successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            ProfileUpdated?.Invoke();
        }

        private void ChangePassword()
        {
            if (_user == null)
            {
                MessageBox.Show("Unable to load account.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(CurrentPassword) || string.IsNullOrWhiteSpace(NewPassword))
            {
                MessageBox.Show("Please fill out all password fields.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (NewPassword != ConfirmPassword)
            {
                MessageBox.Show("New password and confirmation do not match.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!BCrypt.Net.BCrypt.Verify(CurrentPassword, _user.PasswordHash))
            {
                MessageBox.Show("Current password is incorrect.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(NewPassword);
            _user.UpdatedAt = DateTime.Now;
            _context.Users.Update(_user);
            _context.SaveChanges();

            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
            MessageBox.Show("Password updated successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BrowsePhoto()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp",
                Title = "Select Profile Photo"
            };

            if (dialog.ShowDialog() != true)
                return;

            var targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "Profiles");
            Directory.CreateDirectory(targetDir);

            var fileName = $"profile_{_userId}{Path.GetExtension(dialog.FileName)}";
            var targetPath = Path.Combine(targetDir, fileName);

            File.Copy(dialog.FileName, targetPath, true);
            PhotoPath = targetPath;
            PhotoImage = BuildImage(targetPath);
        }

        private ImageSource? BuildImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            using var stream = File.OpenRead(path);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private void ParseDaysOff(string days)
        {
            var tokens = (days ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            IsMondayOff = tokens.Contains("Mon");
            IsTuesdayOff = tokens.Contains("Tue");
            IsWednesdayOff = tokens.Contains("Wed");
            IsThursdayOff = tokens.Contains("Thu");
            IsFridayOff = tokens.Contains("Fri");
            IsSaturdayOff = tokens.Contains("Sat");
            IsSundayOff = tokens.Contains("Sun");
        }

        private string BuildDaysOff()
        {
            var list = new[]
            {
                (IsMondayOff, "Mon"),
                (IsTuesdayOff, "Tue"),
                (IsWednesdayOff, "Wed"),
                (IsThursdayOff, "Thu"),
                (IsFridayOff, "Fri"),
                (IsSaturdayOff, "Sat"),
                (IsSundayOff, "Sun")
            }.Where(x => x.Item1).Select(x => x.Item2);

            return string.Join(",", list);
        }

        private void ApplySelectedPositions(string data)
        {
            var ids = (data ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var pos in Positions)
            {
                pos.IsSelected = ids.Contains(pos.Id.ToString());
            }
        }

        private string BuildPositions()
        {
            var ids = Positions.Where(p => p.IsSelected).Select(p => p.Id.ToString());
            return string.Join(",", ids);
        }

        private void ApplyNotificationPrefs(string types, string channels)
        {
            var typeTokens = (types ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var channelTokens = (channels ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            NotifyLeave = typeTokens.Contains("Leave");
            NotifyShift = typeTokens.Contains("Shift");
            NotifyAnnouncement = typeTokens.Contains("Announcement");

            NotifyInApp = channelTokens.Contains("InApp");
            NotifyEmail = channelTokens.Contains("Email");
        }

        private string BuildNotificationTypes()
        {
            var list = new[]
            {
                (NotifyLeave, "Leave"),
                (NotifyShift, "Shift"),
                (NotifyAnnouncement, "Announcement")
            }.Where(x => x.Item1).Select(x => x.Item2);

            return string.Join(",", list);
        }

        private string BuildNotificationChannels()
        {
            var list = new[]
            {
                (NotifyInApp, "InApp"),
                (NotifyEmail, "Email")
            }.Where(x => x.Item1).Select(x => x.Item2);

            return string.Join(",", list);
        }
    }

    public class SelectablePosition : ObservableObject
    {
        public int Id { get; }
        public string Name { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public SelectablePosition(int id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
