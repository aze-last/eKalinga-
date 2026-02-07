using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class LeaveApprovalViewModel : ObservableObject
    {
        private readonly AppDbContext _context;
        private readonly LeaveService _leaveService;
        private readonly NotificationService _notificationService;
        private readonly int _managerUserId;

        private List<LeaveRequest> _pendingRequests = new();
        private List<LeaveRequest> _allRequests = new();
        private LeaveRequest? _selectedRequest;
        private string _rejectionReason = string.Empty;

        public List<LeaveRequest> PendingRequests
        {
            get => _pendingRequests;
            set => SetProperty(ref _pendingRequests, value);
        }

        public List<LeaveRequest> AllRequests
        {
            get => _allRequests;
            set => SetProperty(ref _allRequests, value);
        }

        public LeaveRequest? SelectedRequest
        {
            get => _selectedRequest;
            set => SetProperty(ref _selectedRequest, value);
        }

        public string RejectionReason
        {
            get => _rejectionReason;
            set => SetProperty(ref _rejectionReason, value);
        }

        public ICommand ApproveCommand { get; }
        public ICommand RejectCommand { get; }
        public ICommand RefreshCommand { get; }

        public LeaveApprovalViewModel(int managerUserId)
        {
            _context = new AppDbContext();
            _leaveService = new LeaveService(_context);
            _notificationService = new NotificationService(_context);
            _managerUserId = managerUserId;

            ApproveCommand = new RelayCommand(param => ExecuteApprove(param));
            RejectCommand = new RelayCommand(param => ExecuteReject(param));
            RefreshCommand = new RelayCommand(_ => LoadData());

            LoadData();
        }

        private void LoadData()
        {
            // Load pending requests
            PendingRequests = _leaveService.GetPendingLeaveRequests();

            // Load all requests (last 30 days)
            var startDate = DateTime.Today.AddDays(-30);
            AllRequests = _leaveService.GetAllLeaveRequests(startDate);
        }

        private void ExecuteApprove(object? param)
        {
            if (param is not LeaveRequest request)
                return;

            var result = System.Windows.MessageBox.Show(
                $"Approve {request.Type} leave for {request.Employee.FullName}?\n\n" +
                $"Dates: {request.StartDate:MMM dd} - {request.EndDate:MMM dd} ({request.TotalDays} days)\n" +
                $"Reason: {request.Reason}",
                "Confirm Approval",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    _leaveService.ApproveLeaveRequest(request.Id, _managerUserId);

                    // Send notification to employee
                    var employee = _context.Employees
                        .Include(e => e.User)
                        .FirstOrDefault(e => e.Id == request.EmployeeId);

                    if (employee?.User != null)
                    {
                        _notificationService.CreateNotification(
                            employee.User.Id,
                            NotificationType.LeaveApproved,
                            "Leave Request Approved",
                            $"Your {request.Type} leave request from {request.StartDate:MMM dd} to {request.EndDate:MMM dd} has been approved.");
                    }

                    LoadData();

                    System.Windows.MessageBox.Show("Leave request approved successfully!", "Success",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(ex.Message, "Error",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteReject(object? param)
        {
            if (param is not LeaveRequest request)
                return;

            // Show rejection reason dialog
            var dialog = new Views.RejectionReasonDialog();
            if (dialog.ShowDialog() == true)
            {
                var reason = dialog.RejectionReason;

                if (string.IsNullOrWhiteSpace(reason))
                {
                    System.Windows.MessageBox.Show("Please provide a rejection reason.", "Rejection Reason Required",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    _leaveService.RejectLeaveRequest(request.Id, _managerUserId, reason);

                    // Send notification to employee
                    var employee = _context.Employees
                        .Include(e => e.User)
                        .FirstOrDefault(e => e.Id == request.EmployeeId);

                    if (employee?.User != null)
                    {
                        _notificationService.CreateNotification(
                            employee.User.Id,
                            NotificationType.LeaveRejected,
                            "Leave Request Rejected",
                            $"Your {request.Type} leave request from {request.StartDate:MMM dd} to {request.EndDate:MMM dd} has been rejected. Reason: {reason}");
                    }

                    LoadData();

                    System.Windows.MessageBox.Show("Leave request rejected.", "Success",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(ex.Message, "Error",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }
    }
}
