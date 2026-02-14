using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class PayrollViewModel : ObservableObject
    {
        private readonly PayrollService _payrollService;
        private readonly ReportExportService _reportExportService;
        private readonly int _generatedByUserId;
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
        public ICommand ExportPayrollCsvCommand { get; }

        public PayrollViewModel(int generatedByUserId = 1)
        {
            _payrollService = new PayrollService(new Data.AppDbContext());
            _reportExportService = new ReportExportService();
            _generatedByUserId = generatedByUserId;
            _payrollItems = new ObservableCollection<PayrollItem>();

            GeneratePayrollCommand = new RelayCommand(ExecuteGeneratePayroll, CanExecuteGeneratePayroll);
            SavePayrollCommand = new RelayCommand(ExecuteSavePayroll, CanExecuteSavePayroll);
            ExportPayrollCsvCommand = new RelayCommand(ExecuteExportPayrollCsv, CanExecuteSavePayroll);
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
                    var grossPay = items.Sum(i => i.GrossPay);
                    var deductions = items.Sum(i => i.DeductionAmount);
                    var netPay = items.Sum(i => i.NetPay);
                    var totalHours = items.Sum(i => i.TotalHours);
                    PayrollSummary = $"{items.Count} employees | {totalHours:N2} total hours | Gross ₱{grossPay:N2} | Deductions ₱{deductions:N2} | Net ₱{netPay:N2}";
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
                    $"Net total: ₱{PayrollItems.Sum(i => i.NetPay):N2} for {PayrollItems.Count} employees",
                    "Confirm Save",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _payrollService.SavePayroll(PayrollItems.ToList(), StartDate, EndDate, _generatedByUserId);

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

        private void ExecuteExportPayrollCsv(object? parameter)
        {
            try
            {
                if (PayrollItems == null || PayrollItems.Count == 0)
                {
                    MessageBox.Show("Generate payroll first before exporting.", "No Data",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv",
                    FileName = $"asms_payroll_{StartDate:yyyyMMdd}_{EndDate:yyyyMMdd}.csv"
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                _reportExportService.ExportPayrollCsv(PayrollItems, dialog.FileName, StartDate, EndDate);

                MessageBox.Show("Payroll CSV exported successfully.", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting payroll CSV: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
