using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class PayrollViewModel : ObservableObject
    {
        private readonly PayrollService _payrollService;
        private DateTime _startDate = DateTime.Now.AddDays(-14);
        private DateTime _endDate = DateTime.Now;
        private ObservableCollection<PayrollItem> _payrollItems;
        private string _payrollSummary = string.Empty;

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

        public ObservableCollection<PayrollItem> PayrollItems
        {
            get => _payrollItems;
            set => SetProperty(ref _payrollItems, value);
        }

        public string PayrollSummary
        {
            get => _payrollSummary;
            set => SetProperty(ref _payrollSummary, value);
        }

        public ICommand GeneratePayrollCommand { get; }
        public ICommand SavePayrollCommand { get; }

        public PayrollViewModel()
        {
            _payrollService = new PayrollService();
            _payrollItems = new ObservableCollection<PayrollItem>();

            GeneratePayrollCommand = new RelayCommand(ExecuteGeneratePayroll, CanExecuteGeneratePayroll);
            SavePayrollCommand = new RelayCommand(ExecuteSavePayroll, CanExecuteSavePayroll);
        }

        private bool CanExecuteGeneratePayroll(object? parameter)
        {
            return StartDate <= EndDate;
        }

        private void ExecuteGeneratePayroll(object? parameter)
        {
            try
            {
                var items = _payrollService.GeneratePayroll(StartDate, EndDate);
                PayrollItems = new ObservableCollection<PayrollItem>(items);

                if (items.Count > 0)
                {
                    var totalPay = items.Sum(i => i.TotalPay);
                    var totalHours = items.Sum(i => i.TotalHours);
                    PayrollSummary = $"{items.Count} employees | {totalHours:N2} total hours | ₱{totalPay:N2} total pay";
                }
                else
                {
                    PayrollSummary = "No attendance records found for this period";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating payroll: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExecuteSavePayroll(object? parameter)
        {
            return PayrollItems != null && PayrollItems.Count > 0;
        }

        private void ExecuteSavePayroll(object? parameter)
        {
            try
            {
                var result = MessageBox.Show(
                    $"Save payroll for period {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}?\n\n" +
                    $"Total: ₱{PayrollItems.Sum(i => i.TotalPay):N2} for {PayrollItems.Count} employees",
                    "Confirm Save",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // TODO: Get current logged-in user ID (for now use 1 = admin)
                    int currentUserId = 1;

                    _payrollService.SavePayroll(PayrollItems.ToList(), StartDate, EndDate, currentUserId);

                    MessageBox.Show("Payroll saved successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving payroll: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}