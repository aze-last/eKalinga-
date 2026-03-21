using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class LeaveRequestViewModel : ObservableObject
    {
        private readonly AppDbContext _context;
        private readonly int _employeeId;
        private readonly NotificationService _notificationService;
        private readonly LeaveService _leaveService;

        private LeaveType _selectedLeaveType = LeaveType.Vacation;
        private DateTime _startDate = DateTime.Today;
        private DateTime _endDate = DateTime.Today;
        private string _reason = string.Empty;

        public ObservableCollection<LeaveRequest> MyRequests { get; } = new();
        public ObservableCollection<LeaveBalance> Balances { get; } = new();

        public Array LeaveTypes => Enum.GetValues(typeof(LeaveType));

        public LeaveType SelectedLeaveType
        {
            get => _selectedLeaveType;
            set => SetProperty(ref _selectedLeaveType, value);
        }

        public DateTime StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        public DateTime EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }

        public string Reason
        {
            get => _reason;
            set => SetProperty(ref _reason, value);
        }

        public ICommand SubmitCommand { get; }

        public LeaveRequestViewModel(int employeeId)
        {
            _employeeId = employeeId;
            _context = new AppDbContext();
            _notificationService = new NotificationService(_context);
            _leaveService = new LeaveService(_context);

            SubmitCommand = new RelayCommand(_ => ExecuteSubmit());

            LoadBalances();
            LoadRequests();
        }

        private void LoadRequests()
        {
            MyRequests.Clear();
            var requests = _context.LeaveRequests
                .Include(lr => lr.Employee)
                .Where(lr => lr.EmployeeId == _employeeId)
                .OrderByDescending(lr => lr.CreatedAt)
                .ToList();

            foreach (var r in requests)
            {
                MyRequests.Add(r);
            }
        }

        private void LoadBalances()
        {
            Balances.Clear();
            int year = DateTime.Today.Year;
            var balance = _context.LeaveBalances
                .FirstOrDefault(b => b.EmployeeId == _employeeId && b.Year == year);

            if (balance == null)
            {
                balance = new LeaveBalance
                {
                    EmployeeId = _employeeId,
                    Year = year
                };
                _context.LeaveBalances.Add(balance);
                _context.SaveChanges();
            }

            Balances.Add(balance);
        }

        private void ExecuteSubmit()
        {
            if (EndDate.Date < StartDate.Date)
            {
                MessageBox.Show("End date must be on or after start date.", "Invalid Dates",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(Reason))
            {
                MessageBox.Show("Please provide a reason for your leave request.", "Missing Reason",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _leaveService.SubmitLeaveRequest(
                    _employeeId,
                    SelectedLeaveType,
                    StartDate.Date,
                    EndDate.Date,
                    Reason.Trim());

                var employee = _context.Employees.FirstOrDefault(e => e.Id == _employeeId);
                if (employee != null)
                {
                    var managerIds = _context.Users
                        .Where(u => u.Role == UserRole.Manager)
                        .Select(u => u.Id)
                        .ToList();

                    if (managerIds.Count > 0)
                    {
                        _notificationService.CreateForUsers(
                            managerIds,
                            NotificationType.General,
                            "New Leave Request",
                            $"{employee.FullName} requested {SelectedLeaveType} leave ({StartDate:MMM dd} - {EndDate:MMM dd}).");
                    }
                }

                Reason = string.Empty;
                StartDate = DateTime.Today;
                EndDate = DateTime.Today;

                LoadBalances();
                LoadRequests();

                MessageBox.Show("Leave request submitted.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Submit Leave",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
