using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class EmployeeDialogViewModel : ObservableObject
    {
        private readonly AppDbContext _context;
        private readonly Employee? _existingEmployee;

        private string _dialogTitle = "Add New Employee";
        private string _fullName = string.Empty;
        private decimal _hourlyRate;
        private DateTime _dateHired = DateTime.Now;
        private Position? _selectedPosition;
        private User? _selectedUser;
        private EmployeeStatus _selectedStatus = EmployeeStatus.Active;

        public string DialogTitle
        {
            get => _dialogTitle;
            set => SetProperty(ref _dialogTitle, value);
        }

        public string FullName
        {
            get => _fullName;
            set => SetProperty(ref _fullName, value);
        }

        public decimal HourlyRate
        {
            get => _hourlyRate;
            set => SetProperty(ref _hourlyRate, value);
        }

        public DateTime DateHired
        {
            get => _dateHired;
            set => SetProperty(ref _dateHired, value);
        }

        public ObservableCollection<Position> Positions { get; set; } = new();
        public ObservableCollection<User> AvailableUsers { get; set; } = new();
        public ObservableCollection<EmployeeStatus> Statuses { get; set; } = new();

        public Position? SelectedPosition
        {
            get => _selectedPosition;
            set => SetProperty(ref _selectedPosition, value);
        }

        public User? SelectedUser
        {
            get => _selectedUser;
            set => SetProperty(ref _selectedUser, value);
        }

        public EmployeeStatus SelectedStatus
        {
            get => _selectedStatus;
            set => SetProperty(ref _selectedStatus, value);
        }

        public bool IsNewEmployee => _existingEmployee == null;

        public ICommand SaveCommand { get; }
        public bool DialogResult { get; private set; }

        public EmployeeDialogViewModel(Employee? employee = null)
        {
            _context = new AppDbContext();
            _existingEmployee = employee;

            Statuses = new ObservableCollection<EmployeeStatus>
            {
                EmployeeStatus.Active,
                EmployeeStatus.Inactive
            };

            LoadData();

            SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);

            if (_existingEmployee != null)
            {
                DialogTitle = "Edit Employee";
                FullName = _existingEmployee.FullName;
                HourlyRate = _existingEmployee.HourlyRate;
                DateHired = _existingEmployee.DateHired;
                SelectedStatus = _existingEmployee.Status;
                SelectedPosition = Positions.FirstOrDefault(p => p.Id == _existingEmployee.PositionId);
                SelectedUser = AvailableUsers.FirstOrDefault(u => u.Id == _existingEmployee.UserId);
            }
        }

        private void LoadData()
        {
            var positions = _context.Positions.ToList();
            foreach (var pos in positions) Positions.Add(pos);

            var users = _context.Users
                .Include(u => u.Employee)
                .Where(u => u.Employee == null || (_existingEmployee != null && u.Id == _existingEmployee.UserId))
                .ToList();
            foreach (var user in users) AvailableUsers.Add(user);
        }

        private bool CanExecuteSave(object? parameter)
        {
            return !string.IsNullOrWhiteSpace(FullName) &&
                   SelectedPosition != null &&
                   (SelectedUser != null || !IsNewEmployee) &&
                   HourlyRate > 0;
        }

        private void ExecuteSave(object? parameter)
        {
            try
            {
                if (_existingEmployee != null)
                {
                    _existingEmployee.FullName = FullName;
                    _existingEmployee.PositionId = SelectedPosition!.Id;
                    _existingEmployee.HourlyRate = HourlyRate;
                    _existingEmployee.DateHired = DateHired;
                    _existingEmployee.Status = SelectedStatus;
                }
                else
                {
                    var newEmployee = new Employee
                    {
                        FullName = FullName,
                        UserId = SelectedUser!.Id,
                        PositionId = SelectedPosition!.Id,
                        HourlyRate = HourlyRate,
                        DateHired = DateHired,
                        Status = SelectedStatus
                    };
                    _context.Employees.Add(newEmployee);
                }

                _context.SaveChanges();
                DialogResult = true;

                MessageBox.Show("Employee saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.DataContext == this)?.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving employee: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
