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
    public class LeaveApprovalViewModel : ObservableObject
    {
        private readonly AppDbContext _context;
        private readonly int _managerUserId;
        private readonly NotificationService _notificationService;
        private readonly LeaveService _leaveService;

        private LeaveRequest? _selectedRequest;
        private string _rejectionReason = string.Empty;

        public ObservableCollection<LeaveRequest> PendingRequests { get; } = new();

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

        public LeaveApprovalViewModel(int managerUserId)
        {
            _managerUserId = managerUserId;
            _context = new AppDbContext();
            _notificationService = new NotificationService(_context);
            _leaveService = new LeaveService(_context);

            ApproveCommand = new RelayCommand(_ => ExecuteApprove(), _ => SelectedRequest != null);
            RejectCommand = new RelayCommand(_ => ExecuteReject(), _ => SelectedRequest != null);

            LoadPending();
        }

        private void LoadPending()
        {
            PendingRequests.Clear();
            var requests = _context.LeaveRequests
                .Include(lr => lr.Employee)
                .Where(lr => lr.Status == LeaveStatus.Pending)
                .OrderBy(lr => lr.CreatedAt)
                .ToList();

            foreach (var r in requests)
            {
                PendingRequests.Add(r);
            }
        }

        private void ExecuteApprove()
        {
            if (SelectedRequest == null) return;

            try
            {
                _leaveService.ApproveLeaveRequest(SelectedRequest.Id, _managerUserId);
                NotifyEmployee(SelectedRequest, true);
                LoadPending();
                MessageBox.Show("Leave request approved.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Approve Leave",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ExecuteReject()
        {
            if (SelectedRequest == null) return;

            if (string.IsNullOrWhiteSpace(RejectionReason))
            {
                MessageBox.Show("Please enter a rejection reason.", "Missing Reason",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _leaveService.RejectLeaveRequest(SelectedRequest.Id, _managerUserId, RejectionReason.Trim());
                NotifyEmployee(SelectedRequest, false);
                RejectionReason = string.Empty;
                LoadPending();
                MessageBox.Show("Leave request rejected.", "Updated", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Reject Leave",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void NotifyEmployee(LeaveRequest request, bool approved)
        {
            var employee = _context.Employees.FirstOrDefault(e => e.Id == request.EmployeeId);
            if (employee == null) return;

            _notificationService.Create(
                employee.UserId,
                approved ? NotificationType.LeaveApproved : NotificationType.LeaveRejected,
                approved ? "Leave Approved" : "Leave Rejected",
                approved
                    ? $"Your {request.Type} leave ({request.StartDate:MMM dd} - {request.EndDate:MMM dd}) was approved."
                    : $"Your {request.Type} leave ({request.StartDate:MMM dd} - {request.EndDate:MMM dd}) was rejected.");
        }

    }
}
