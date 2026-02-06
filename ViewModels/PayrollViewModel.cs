using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.Models;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class PayrollViewModel : ObservableObject
    {
        private readonly PayrollService _payrollService;

        public DateTime StartDate { get; set; } = DateTime.Today.AddDays(-7);
        public DateTime EndDate { get; set; } = DateTime.Today;

        public ObservableCollection<PayrollResult> PayrollList { get; set; } = new();

        public ICommand GeneratePayrollCommand { get; }

        public PayrollViewModel()
        {
            _payrollService = new PayrollService();
            GeneratePayrollCommand = new RelayCommand(_ => Generate());
        }

        private void Generate()
        {
            PayrollList.Clear();

            var results = _payrollService.Generate(StartDate, EndDate);
            foreach (var r in results)
                PayrollList.Add(r);
        }
    }
}
