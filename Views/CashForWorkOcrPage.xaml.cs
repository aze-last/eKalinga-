using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace AttendanceShiftingManagement.Views
{
    public partial class CashForWorkOcrPage : UserControl
    {
        private CashForWorkOcrViewModel ViewModel => (CashForWorkOcrViewModel)DataContext;

        public CashForWorkOcrPage(User currentUser)
        {
            InitializeComponent();
            DataContext = new CashForWorkOcrViewModel(currentUser);
        }

        private void CreateEvent_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CashForWorkEventWindow(
                ViewModel.EventTitle,
                ViewModel.EventLocation,
                ViewModel.EventDate,
                ViewModel.EventStartTime,
                ViewModel.EventEndTime,
                ViewModel.EventNotes)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            ViewModel.EventTitle = dialog.EventTitle;
            ViewModel.EventLocation = dialog.EventLocation;
            ViewModel.EventDate = dialog.EventDate;
            ViewModel.EventStartTime = dialog.EventStartTime;
            ViewModel.EventEndTime = dialog.EventEndTime;
            ViewModel.EventNotes = dialog.EventNotes;
            ViewModel.CreateEventCommand.Execute(null);
        }

        private void EditEvent_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedEvent == null)
            {
                MessageBox.Show("Select an event first.", "No Event Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedEvent = ViewModel.SelectedEvent;
            var dialog = new CashForWorkEventWindow(
                selectedEvent.Title,
                selectedEvent.Location,
                selectedEvent.EventDate,
                selectedEvent.StartTime.ToString(@"hh\:mm"),
                selectedEvent.EndTime.ToString(@"hh\:mm"),
                selectedEvent.Notes ?? string.Empty,
                "Edit Cash-for-Work Event",
                "SAVE CHANGES")
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                ViewModel.UpdateSelectedEvent(
                    dialog.EventTitle,
                    dialog.EventLocation,
                    dialog.EventDate,
                    dialog.EventStartTime,
                    dialog.EventEndTime,
                    dialog.EventNotes);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Update Event", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeleteEvent_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedEvent == null)
            {
                MessageBox.Show("Select an event first.", "No Event Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedEvent = ViewModel.SelectedEvent;
            var confirmDelete = MessageBox.Show(
                $"Delete the event '{selectedEvent.Title}' and all of its attendance records?",
                "Delete Event",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmDelete != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                ViewModel.DeleteSelectedEvent();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Delete Event", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void EditAttendance_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedAttendanceRow == null)
            {
                MessageBox.Show("Select an attendance record first.", "No Attendance Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedAttendance = ViewModel.SelectedAttendanceRow;
            var dialog = new CashForWorkAttendanceWindow(
                selectedAttendance.FullName,
                selectedAttendance.BeneficiaryId,
                selectedAttendance.CivilRegistryId,
                selectedAttendance.AttendanceDate,
                selectedAttendance.StatusValue,
                selectedAttendance.SourceValue)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                ViewModel.UpdateSelectedAttendance(
                    dialog.AttendanceDate,
                    dialog.Status,
                    dialog.Source);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Update Attendance", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeleteAttendance_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedAttendanceRow == null)
            {
                MessageBox.Show("Select an attendance record first.", "No Attendance Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedAttendance = ViewModel.SelectedAttendanceRow;
            var confirmDelete = MessageBox.Show(
                $"Delete the attendance record for '{selectedAttendance.FullName}'?",
                "Delete Attendance",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmDelete != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                ViewModel.DeleteSelectedAttendance();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Unable to Delete Attendance", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
