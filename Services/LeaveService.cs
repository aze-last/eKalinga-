using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AttendanceShiftingManagement.Services
{
    public class LeaveService
    {
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;

        public LeaveService(AppDbContext context)
        {
            _context = context;
            _auditService = new AuditService(_context);
        }

        /// <summary>
        /// Submit a new leave request
        /// </summary>
        public LeaveRequest SubmitLeaveRequest(int employeeId, LeaveType type, DateTime startDate, DateTime endDate, string reason)
        {
            // Validate dates
            if (startDate > endDate)
                throw new Exception("Start date must be before or equal to end date.");

            if (startDate < DateTime.Today)
                throw new Exception("Cannot request leave for past dates.");

            // Calculate total days
            int totalDays = (endDate.Date - startDate.Date).Days + 1;

            // Check leave balance
            var balance = GetOrCreateLeaveBalance(employeeId, startDate.Year);

            if (type == LeaveType.Vacation)
            {
                if (balance.RemainingVacationDays < totalDays)
                    throw new Exception($"Insufficient vacation days. Available: {balance.RemainingVacationDays}, Requested: {totalDays}");
            }
            else if (type == LeaveType.Sick)
            {
                if (balance.RemainingSickDays < totalDays)
                    throw new Exception($"Insufficient sick days. Available: {balance.RemainingSickDays}, Requested: {totalDays}");
            }

            // Check for overlapping leave requests
            var overlapping = _context.LeaveRequests
                .Any(lr =>
                    lr.EmployeeId == employeeId &&
                    lr.Status != LeaveStatus.Rejected &&
                    lr.Status != LeaveStatus.Cancelled &&
                    lr.StartDate <= endDate &&
                    lr.EndDate >= startDate);

            if (overlapping)
                throw new Exception("You already have a leave request for this date range.");

            // Create leave request
            var leaveRequest = new LeaveRequest
            {
                EmployeeId = employeeId,
                Type = type,
                StartDate = startDate.Date,
                EndDate = endDate.Date,
                Reason = reason,
                Status = LeaveStatus.Pending,
                CreatedAt = DateTime.Now
            };

            _context.LeaveRequests.Add(leaveRequest);
            _context.SaveChanges();

            var userId = _context.Employees
                .Where(e => e.Id == employeeId)
                .Select(e => (int?)e.UserId)
                .FirstOrDefault();

            _auditService.LogActivity(
                userId,
                "LeaveSubmitted",
                "LeaveRequest",
                leaveRequest.Id,
                $"Submitted {type} leave from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}.");

            return leaveRequest;
        }

        /// <summary>
        /// Approve a leave request
        /// </summary>
        public void ApproveLeaveRequest(int requestId, int approvedByUserId)
        {
            var request = _context.LeaveRequests
                .Include(lr => lr.Employee)
                .FirstOrDefault(lr => lr.Id == requestId);

            if (request == null)
                throw new Exception("Leave request not found.");

            if (request.Status != LeaveStatus.Pending)
                throw new Exception("Only pending requests can be approved.");

            // Update leave balance
            var balance = GetOrCreateLeaveBalance(request.EmployeeId, request.StartDate.Year);
            int totalDays = request.TotalDays;

            if (request.Type == LeaveType.Vacation)
            {
                balance.UsedVacationDays += totalDays;
            }
            else if (request.Type == LeaveType.Sick)
            {
                balance.UsedSickDays += totalDays;
            }

            // Update request status
            request.Status = LeaveStatus.Approved;
            request.ApprovedBy = approvedByUserId;
            request.ApprovedAt = DateTime.Now;

            _context.SaveChanges();

            _auditService.LogActivity(
                approvedByUserId,
                "LeaveApproved",
                "LeaveRequest",
                request.Id,
                $"Approved {request.Type} leave for employee {request.EmployeeId} ({request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}).");
        }

        /// <summary>
        /// Reject a leave request
        /// </summary>
        public void RejectLeaveRequest(int requestId, int rejectedByUserId, string rejectionReason)
        {
            var request = _context.LeaveRequests.FirstOrDefault(lr => lr.Id == requestId);

            if (request == null)
                throw new Exception("Leave request not found.");

            if (request.Status != LeaveStatus.Pending)
                throw new Exception("Only pending requests can be rejected.");

            request.Status = LeaveStatus.Rejected;
            request.ApprovedBy = rejectedByUserId;
            request.ApprovedAt = DateTime.Now;
            request.RejectionReason = rejectionReason;

            _context.SaveChanges();

            _auditService.LogActivity(
                rejectedByUserId,
                "LeaveRejected",
                "LeaveRequest",
                request.Id,
                $"Rejected {request.Type} leave for employee {request.EmployeeId}. Reason: {rejectionReason}");
        }

        /// <summary>
        /// Cancel a leave request (by employee)
        /// </summary>
        public void CancelLeaveRequest(int requestId, int employeeId)
        {
            var request = _context.LeaveRequests
                .FirstOrDefault(lr => lr.Id == requestId && lr.EmployeeId == employeeId);

            if (request == null)
                throw new Exception("Leave request not found.");

            if (request.Status == LeaveStatus.Cancelled || request.Status == LeaveStatus.Rejected)
                throw new Exception("This leave request can no longer be cancelled.");

            if (request.Status == LeaveStatus.Approved)
            {
                // Restore leave balance
                var balance = GetOrCreateLeaveBalance(employeeId, request.StartDate.Year);
                int totalDays = request.TotalDays;

                if (request.Type == LeaveType.Vacation)
                {
                    balance.UsedVacationDays = Math.Max(0, balance.UsedVacationDays - totalDays);
                }
                else if (request.Type == LeaveType.Sick)
                {
                    balance.UsedSickDays = Math.Max(0, balance.UsedSickDays - totalDays);
                }
            }

            request.Status = LeaveStatus.Cancelled;
            _context.SaveChanges();

            var userId = _context.Employees
                .Where(e => e.Id == employeeId)
                .Select(e => (int?)e.UserId)
                .FirstOrDefault();

            _auditService.LogActivity(
                userId,
                "LeaveCancelled",
                "LeaveRequest",
                request.Id,
                $"Cancelled leave request ({request.Type}) for {request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}.");
        }

        /// <summary>
        /// Get pending leave requests (for manager approval)
        /// </summary>
        public List<LeaveRequest> GetPendingLeaveRequests()
        {
            return _context.LeaveRequests
                .Include(lr => lr.Employee)
                    .ThenInclude(e => e.Position)
                .Where(lr => lr.Status == LeaveStatus.Pending)
                .OrderBy(lr => lr.CreatedAt)
                .ToList();
        }

        /// <summary>
        /// Get employee's leave requests
        /// </summary>
        public List<LeaveRequest> GetEmployeeLeaveRequests(int employeeId)
        {
            return _context.LeaveRequests
                .Include(lr => lr.ApprovedByUser)
                .Where(lr => lr.EmployeeId == employeeId)
                .OrderByDescending(lr => lr.CreatedAt)
                .ToList();
        }

        /// <summary>
        /// Get leave balance for an employee
        /// </summary>
        public LeaveBalance GetLeaveBalance(int employeeId, int year)
        {
            return GetOrCreateLeaveBalance(employeeId, year);
        }

        /// <summary>
        /// Get or create leave balance for an employee
        /// </summary>
        private LeaveBalance GetOrCreateLeaveBalance(int employeeId, int year)
        {
            var balance = _context.LeaveBalances
                .FirstOrDefault(lb => lb.EmployeeId == employeeId && lb.Year == year);

            if (balance == null)
            {
                balance = new LeaveBalance
                {
                    EmployeeId = employeeId,
                    Year = year,
                    VacationDays = 15,
                    SickDays = 10,
                    UsedVacationDays = 0,
                    UsedSickDays = 0
                };

                _context.LeaveBalances.Add(balance);
                _context.SaveChanges();
            }

            return balance;
        }

        /// <summary>
        /// Get all leave requests (for manager/admin)
        /// </summary>
        public List<LeaveRequest> GetAllLeaveRequests(DateTime? startDate = null, DateTime? endDate = null, LeaveStatus? status = null)
        {
            var query = _context.LeaveRequests
                .Include(lr => lr.Employee)
                    .ThenInclude(e => e.Position)
                .Include(lr => lr.ApprovedByUser)
                .AsQueryable();

            if (startDate.HasValue)
                query = query.Where(lr => lr.StartDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(lr => lr.EndDate <= endDate.Value);

            if (status.HasValue)
                query = query.Where(lr => lr.Status == status.Value);

            return query.OrderByDescending(lr => lr.CreatedAt).ToList();
        }
    }
}
