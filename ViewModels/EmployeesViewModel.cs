using AttendanceShiftingManagement.Data;
using AttendanceShiftingManagement.Helpers;
using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Views;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace AttendanceShiftingManagement.ViewModels
{
    public class EmployeesViewModel : ObservableObject
    {
        private readonly AppDbContext _context;
        private readonly User _currentUser;
        private ObservableCollection<Employee> _employees;
        private Employee? _selectedEmployee;
        private ICollectionView _employeesView;
        private ObservableCollection<Position> _positions;
        private ObservableCollection<PositionFilterItem> _positionFilters;
        private ObservableCollection<PositionCount> _positionCounts;
        private string _selectedPositionName;
        private string _searchText = string.Empty;

        public Visibility CrudVisibility => _currentUser.Role == UserRole.Admin ? Visibility.Visible : Visibility.Collapsed;

        public ObservableCollection<Employee> Employees
        {
            get => _employees;
            set => SetProperty(ref _employees, value);
        }

        public ICollectionView EmployeesView
        {
            get => _employeesView;
            private set => SetProperty(ref _employeesView, value);
        }

        public ObservableCollection<Position> Positions
        {
            get => _positions;
            private set => SetProperty(ref _positions, value);
        }

        public ObservableCollection<PositionFilterItem> PositionFilters
        {
            get => _positionFilters;
            private set => SetProperty(ref _positionFilters, value);
        }

        public ObservableCollection<PositionCount> PositionCounts
        {
            get => _positionCounts;
            private set => SetProperty(ref _positionCounts, value);
        }

        public string SelectedPositionName
        {
            get => _selectedPositionName;
            set
            {
                if (SetProperty(ref _selectedPositionName, value))
                {
                    ApplyFilter();
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplyFilter();
                }
            }
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
            _positions = new ObservableCollection<Position>();
            _positionFilters = new ObservableCollection<PositionFilterItem>();
            _positionCounts = new ObservableCollection<PositionCount>();
            _employeesView = CollectionViewSource.GetDefaultView(_employees);
            _selectedPositionName = "All Positions";

            AddEmployeeCommand = new RelayCommand(ExecuteAddEmployee);
            EditEmployeeCommand = new RelayCommand(ExecuteEditEmployee);
            DeleteEmployeeCommand = new RelayCommand(ExecuteDeleteEmployee);

            LoadPositions();
            LoadEmployees();
            BuildPositionFilters();
            UpdatePositionCounts();
            ApplyFilter();
        }

        private void LoadPositions()
        {
            var positions = _context.Positions
                .OrderBy(p => p.Name)
                .ToList();
            Positions = new ObservableCollection<Position>(positions);
        }

        private void LoadEmployees()
        {
            // Clear tracking to ensure fresh data from DB, or disposal/recreation would be better if cheap.
            _context.ChangeTracker.Clear();

            var employees = _context.Employees
                .Include(e => e.Position)
                .Include(e => e.User)
                .ToList();
            Employees = new ObservableCollection<Employee>(employees);
            EmployeesView = CollectionViewSource.GetDefaultView(Employees);
            ApplyFilter();
            UpdatePositionCounts();
        }

        private void ApplyFilter()
        {
            if (EmployeesView == null)
            {
                return;
            }

            EmployeesView.Filter = item =>
            {
                if (item is not Employee employee)
                {
                    return false;
                }

                if (SelectedPositionName != "All Positions" && employee.Position.Name != SelectedPositionName)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    return true;
                }

                return employee.FullName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || employee.Position.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || employee.Position.Area.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            };

            EmployeesView.Refresh();
        }

        private void BuildPositionFilters()
        {
            var items = new ObservableCollection<PositionFilterItem>();
            items.Add(new PositionFilterItem("All Positions"));

            foreach (var name in Positions
                .Select(p => p.Name)
                .Distinct()
                .OrderBy(n => n))
            {
                items.Add(new PositionFilterItem(name));
            }

            PositionFilters = items;
            if (string.IsNullOrWhiteSpace(SelectedPositionName))
            {
                SelectedPositionName = "All Positions";
            }
        }

        private void UpdatePositionCounts()
        {
            var items = Employees
                .GroupBy(e => e.Position.Name)
                .OrderBy(g => g.Key)
                .Select(g => new PositionCount(g.Key, g.Count()))
                .ToList();

            PositionCounts = new ObservableCollection<PositionCount>(items);
        }

        private void ExecuteAddEmployee(object? parameter)
        {
            var dialog = new Views.EmployeeDialogWindow();
            dialog.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is MainWindow);
            dialog.ShowDialog();

            if (dialog.DataContext is EmployeeDialogViewModel vm && vm.DialogResult)
            {
                LoadPositions();
                BuildPositionFilters();
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
                    LoadPositions();
                    BuildPositionFilters();
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
                    LoadPositions();
                    BuildPositionFilters();
                    LoadEmployees();
                    MessageBox.Show("Employee deleted successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        public sealed class PositionFilterItem
        {
            public PositionFilterItem(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }

        public sealed class PositionCount
        {
            public PositionCount(string name, int count)
            {
                Name = name;
                Count = count;
            }

            public string Name { get; }
            public int Count { get; }
        }
    }
}
