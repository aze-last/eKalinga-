using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class HolidaysViewModel : ObservableObject
    {
        private readonly AppDbContext _context;

        private ObservableCollection<Holiday> _holidays = new();
        private Holiday? _selectedHoliday;

        public ObservableCollection<Holiday> Holidays
        {
            get => _holidays;
            set => SetProperty(ref _holidays, value);
        }

        public Holiday? SelectedHoliday
        {
            get => _selectedHoliday;
            set => SetProperty(ref _selectedHoliday, value);
        }

        public ICommand AddHolidayCommand { get; }
        public ICommand EditHolidayCommand { get; }
        public ICommand DeleteHolidayCommand { get; }

        public HolidaysViewModel()
        {
            _context = new AppDbContext();

            AddHolidayCommand = new RelayCommand(_ => ExecuteAddHoliday());
            EditHolidayCommand = new RelayCommand(p => ExecuteEditHoliday(p));
            DeleteHolidayCommand = new RelayCommand(p => ExecuteDeleteHoliday(p));

            LoadHolidays();
        }

        private void LoadHolidays()
        {
            var list = _context.Holidays
                .OrderBy(h => h.HolidayDate)
                .ToList();

            Holidays = new ObservableCollection<Holiday>(list);
        }

        private void ExecuteAddHoliday()
        {
            var dialog = new Views.HolidayDialogWindow();
            dialog.ShowDialog();

            if (dialog.DataContext is HolidayDialogViewModel vm && vm.DialogResult)
                LoadHolidays();
        }

        private void ExecuteEditHoliday(object? parameter)
        {
            if (parameter is not Holiday holiday) return;

            var dialog = new Views.HolidayDialogWindow(holiday);
            dialog.ShowDialog();

            if (dialog.DataContext is HolidayDialogViewModel vm && vm.DialogResult)
                LoadHolidays();
        }

        private void ExecuteDeleteHoliday(object? parameter)
        {
            if (parameter is not Holiday holiday) return;

            var result = MessageBox.Show(
                $"Delete holiday '{holiday.Name}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            _context.Holidays.Remove(holiday);
            _context.SaveChanges();

            LoadHolidays();
        }
    }
}
