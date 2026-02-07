using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Views;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class EmployeesViewModel : ObservableObject
    {
        private readonly AppDbContext _context;
        private readonly User _currentUser;
        private ObservableCollection<Employee> _employees;
        private Employee? _selectedEmployee;

        public Visibility CrudVisibility => _currentUser.Role == UserRole.Admin ? Visibility.Visible : Visibility.Collapsed;

        public ObservableCollection<Employee> Employees
        {
            get => _employees;
            set => SetProperty(ref _employees, value);
        }

        public Employee? SelectedEmployee
        {
            get => _selectedEmployee;
            set => SetProperty(ref _selectedEmployee, value);
        }

        public ICommand AddEmployeeCommand { get; }
        public ICommand EditEmployeeCommand { get; }
        public ICommand DeleteEmployeeCommand { get; }

        public EmployeesViewModel(User user)
        {
            _currentUser = user;
            _context = new AppDbContext();
            _employees = new ObservableCollection<Employee>();

            AddEmployeeCommand = new RelayCommand(ExecuteAddEmployee);
            EditEmployeeCommand = new RelayCommand(ExecuteEditEmployee);
            DeleteEmployeeCommand = new RelayCommand(ExecuteDeleteEmployee);

            LoadEmployees();
        }

        private void LoadEmployees()
        {
            var employees = _context.Employees
                .Include(e => e.Position)
                .Include(e => e.User)
                .ToList();
            Employees = new ObservableCollection<Employee>(employees);
        }

        private void ExecuteAddEmployee(object? parameter)
        {
            var dialog = new Views.EmployeeDialogWindow();
            dialog.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is MainWindow);
            dialog.ShowDialog();

            if (dialog.DataContext is EmployeeDialogViewModel vm && vm.DialogResult)
            {
                LoadEmployees();
            }
        }

        private void ExecuteEditEmployee(object? parameter)
        {
            if (parameter is Employee employee)
            {
                var dialog = new Views.EmployeeDialogWindow(employee);
                dialog.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is MainWindow);
                dialog.ShowDialog();

                if (dialog.DataContext is EmployeeDialogViewModel vm && vm.DialogResult)
                {
                    LoadEmployees();
                }
            }
        }

        private void ExecuteDeleteEmployee(object? parameter)
        {
            if (parameter is Employee employee)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete employee '{employee.FullName}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    _context.Employees.Remove(employee);
                    _context.SaveChanges();
                    LoadEmployees();
                    MessageBox.Show("Employee deleted successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
    }
}