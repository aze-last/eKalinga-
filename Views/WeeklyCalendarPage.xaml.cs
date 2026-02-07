using System.Windows;
using System.Windows.Controls;
using AttendanceShiftingManagement.ViewModels;
using System.Linq;

namespace AttendanceShiftingManagement.Views
{
    public partial class WeeklyCalendarPage : UserControl
    {
        public WeeklyCalendarPage()
        {
            InitializeComponent();
            DataContextChanged += WeeklyCalendarPage_DataContextChanged;
        }

        private void WeeklyCalendarPage_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is WeeklyCalendarViewModel oldVm)
            {
                oldVm.ScrollToEmployeeRequested -= HandleScrollToEmployeeRequested;
            }

            if (e.NewValue is WeeklyCalendarViewModel newVm)
            {
                newVm.ScrollToEmployeeRequested += HandleScrollToEmployeeRequested;
            }
        }

        private void HandleScrollToEmployeeRequested(string employeeName)
        {
            if (string.IsNullOrWhiteSpace(employeeName))
            {
                return;
            }

            var vm = DataContext as WeeklyCalendarViewModel;
            if (vm == null)
            {
                return;
            }

            var target = vm.EmployeeWeeklySchedules.FirstOrDefault(s => s.EmployeeName == employeeName);
            if (target == null)
            {
                return;
            }

            ScheduleItemsControl.UpdateLayout();
            var container = ScheduleItemsControl.ItemContainerGenerator.ContainerFromItem(target) as FrameworkElement;
            container?.BringIntoView();
        }
    }
}
