using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class HolidayDialogViewModel : ObservableObject
    {
        private readonly AppDbContext _context;
        private readonly Holiday? _existingHoliday;
        private readonly Window _window;

        private string _dialogTitle = "Add New Holiday";
        private DateTime _holidayDate = DateTime.Today;
        private string _holidayName = string.Empty;
        private bool _isDoublePay = true;

        public string DialogTitle
        {
            get => _dialogTitle;
            set => SetProperty(ref _dialogTitle, value);
        }

        public DateTime HolidayDate
        {
            get => _holidayDate;
            set
            {
                if (SetProperty(ref _holidayDate, value))
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            }
        }

        public string HolidayName
        {
            get => _holidayName;
            set
            {
                if (SetProperty(ref _holidayName, value))
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            }
        }

        public bool IsDoublePay
        {
            get => _isDoublePay;
            set => SetProperty(ref _isDoublePay, value);
        }

        public ICommand SaveCommand { get; }
        public bool DialogResult { get; private set; }

        // Add
        public HolidayDialogViewModel(Window window)
        {
            _window = window;
            _context = new AppDbContext();
            SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);
        }

        // Edit
        public HolidayDialogViewModel(Window window, Holiday holiday)
        {
            _window = window;
            _context = new AppDbContext();
            _existingHoliday = holiday;

            SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);

            DialogTitle = "Edit Holiday";
            HolidayDate = _existingHoliday.HolidayDate;
            HolidayName = _existingHoliday.Name;
            IsDoublePay = _existingHoliday.IsDoublePay;
        }

        private bool CanExecuteSave(object? _)
        {
            return HolidayDate != default && !string.IsNullOrWhiteSpace(HolidayName);
        }

        private void ExecuteSave(object? _)
        {
            try
            {
                // prevent duplicates by date (except editing same record)
                var dateOnly = HolidayDate.Date;

                bool dateExists = _context.Holidays
                    .AsNoTracking()
                    .Any(h => h.HolidayDate.Date == dateOnly
                              && (_existingHoliday == null || h.Id != _existingHoliday.Id));

                if (dateExists)
                {
                    MessageBox.Show("Holiday date already exists.", "Validation",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (_existingHoliday != null)
                {
                    _existingHoliday.HolidayDate = dateOnly;
                    _existingHoliday.Name = HolidayName.Trim();
                    _existingHoliday.IsDoublePay = IsDoublePay;

                    _context.Holidays.Update(_existingHoliday);
                }
                else
                {
                    var newHoliday = new Holiday
                    {
                        HolidayDate = dateOnly,
                        Name = HolidayName.Trim(),
                        IsDoublePay = IsDoublePay
                    };

                    _context.Holidays.Add(newHoliday);
                }

                _context.SaveChanges();
                DialogResult = true;

                _window.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving holiday: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
