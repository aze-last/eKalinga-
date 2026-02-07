using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class LeaveRequestViewModel : ObservableObject
    {
        private readonly AppDbContext _context;
        private readonly LeaveService _leaveService;
        private readonly int _employeeId;

        private LeaveType _selectedLeaveType = LeaveType.Vacation;
        private DateTime _startDate = DateTime.Today;
        private DateTime _endDate = DateTime.Today;
        private string _reason = string.Empty;
        private string _vacationBalance = "0";
        private string _sickBalance = "0";

        public LeaveType SelectedLeaveType
        {
            get => _selectedLeaveType;
            set => SetProperty(ref _selectedLeaveType, value);
        }

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty(ref _startDate, value))
                {
                    if (_endDate < _startDate)
                        EndDate = _startDate;
                }
            }
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

        public string VacationBalance
        {
            get => _vacationBalance;
            set => SetProperty(ref _vacationBalance, value);
        }

        public string SickBalance
        {
            get => _sickBalance;
            set => SetProperty(ref _sickBalance, value);
        }

        public ObservableCollection<LeaveRequest> MyLeaveRequests { get; }
        public ObservableCollection<LeaveType> LeaveTypes { get; }

        public ICommand SubmitLeaveCommand { get; }
        public ICommand CancelLeaveCommand { get; }
        public ICommand RefreshCommand { get; }

        public LeaveRequestViewModel(int employeeId)
        {
            _context = new AppDbContext();
            _leaveService = new LeaveService(_context);
            _employeeId = employeeId;

            MyLeaveRequests = new ObservableCollection<LeaveRequest>();
            LeaveTypes = new ObservableCollection<LeaveType>
            {
                LeaveType.Vacation,
                LeaveType.Sick,
                LeaveType.Emergency,
                LeaveType.Personal
            };

            SubmitLeaveCommand = new RelayCommand(_ => ExecuteSubmitLeave(), _ => CanSubmitLeave());
            CancelLeaveCommand = new RelayCommand(param => ExecuteCancelLeave(param));
            RefreshCommand = new RelayCommand(_ => LoadData());

            LoadData();
        }

        private void LoadData()
        {
            // Load leave balance
            var balance = _leaveService.GetLeaveBalance(_employeeId, DateTime.Today.Year);
            VacationBalance = $"{balance.RemainingVacationDays:F1} days";
            SickBalance = $"{balance.RemainingSickDays:F1} days";

            // Load leave requests
            MyLeaveRequests.Clear();
            var requests = _leaveService.GetEmployeeLeaveRequests(_employeeId);
            foreach (var request in requests)
            {
                MyLeaveRequests.Add(request);
            }
        }

        private bool CanSubmitLeave()
        {
            return !string.IsNullOrWhiteSpace(Reason) && StartDate >= DateTime.Today;
        }

        private void ExecuteSubmitLeave()
        {
            try
            {
                _leaveService.SubmitLeaveRequest(_employeeId, SelectedLeaveType, StartDate, EndDate, Reason);

                System.Windows.MessageBox.Show("Leave request submitted successfully!", "Success",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                // Reset form
                Reason = string.Empty;
                StartDate = DateTime.Today;
                EndDate = DateTime.Today;

                LoadData();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ExecuteCancelLeave(object? param)
        {
            if (param is not LeaveRequest request)
                return;

            if (request.Status != LeaveStatus.Pending && request.Status != LeaveStatus.Approved)
            {
                System.Windows.MessageBox.Show("Only pending or approved requests can be cancelled.", "Cannot Cancel",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var result = System.Windows.MessageBox.Show($"Cancel this {request.Type} leave request?", "Confirm Cancel",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    _leaveService.CancelLeaveRequest(request.Id, _employeeId);
                    LoadData();

                    System.Windows.MessageBox.Show("Leave request cancelled successfully!", "Success",
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
