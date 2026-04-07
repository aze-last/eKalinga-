using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using System;
using System.Windows;

namespace AttendanceShiftingManagement.Views
{
    public partial class CashForWorkAttendanceWindow : Window
    {
        public CashForWorkAttendanceWindow(
            string fullName,
            string beneficiaryId,
            string civilRegistryId,
            DateTime attendanceDate,
            CashForWorkAttendanceStatus status,
            AttendanceCaptureSource source)
        {
            InitializeComponent();
            WindowBrandingService.ApplyWindowIcon(this);

            ParticipantNameTextBlock.Text = fullName;
            BeneficiaryIdTextBlock.Text = $"Beneficiary ID: {beneficiaryId}";
            CivilRegistryIdTextBlock.Text = $"Civil Registry ID: {civilRegistryId}";

            AttendanceDatePicker.SelectedDate = attendanceDate;
            StatusComboBox.ItemsSource = Enum.GetValues(typeof(CashForWorkAttendanceStatus));
            StatusComboBox.SelectedItem = status;
            SourceComboBox.ItemsSource = Enum.GetValues(typeof(AttendanceCaptureSource));
            SourceComboBox.SelectedItem = source;
        }

        public DateTime AttendanceDate { get; private set; } = DateTime.Today;

        public CashForWorkAttendanceStatus Status { get; private set; } = CashForWorkAttendanceStatus.Present;

        public AttendanceCaptureSource Source { get; private set; } = AttendanceCaptureSource.Manual;

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            AttendanceDate = AttendanceDatePicker.SelectedDate ?? DateTime.Today;
            Status = StatusComboBox.SelectedItem is CashForWorkAttendanceStatus status
                ? status
                : CashForWorkAttendanceStatus.Present;
            Source = SourceComboBox.SelectedItem is AttendanceCaptureSource source
                ? source
                : AttendanceCaptureSource.Manual;

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
