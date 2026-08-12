using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using MaterialDesignThemes.Wpf;
using System;
using System.Windows;

namespace AttendanceShiftingManagement.Views
{
    public partial class CashForWorkEventWindow : Window
    {
        public CashForWorkEventWindow(
            string eventTitle,
            string eventLocation,
            DateTime eventDate,
            string eventStartTime,
            string eventEndTime,
            string eventNotes,
            string windowTitle = "New Cash-for-Work Event",
            string submitButtonText = "CREATE EVENT")
        {
            InitializeComponent();
            WindowBrandingService.ApplyWindowIcon(this);

            Title = windowTitle;
            WindowHeadingTextBlock.Text = windowTitle;
            SubmitButton.Content = submitButtonText;

            TitleTextBox.Text = eventTitle;
            LocationTextBox.Text = eventLocation;
            EventDatePicker.SelectedDate = eventDate;
            StartTimeTextBox.Text = string.IsNullOrWhiteSpace(eventStartTime) ? "07:00" : eventStartTime;
            EndTimeTextBox.Text = string.IsNullOrWhiteSpace(eventEndTime) ? "12:00" : eventEndTime;
            NotesTextBox.Text = eventNotes;
        }

        public string EventTitle { get; private set; } = string.Empty;

        public string EventLocation { get; private set; } = string.Empty;

        public DateTime EventDate { get; private set; } = DateTime.Today;

        public string EventStartTime { get; private set; } = string.Empty;

        public string EventEndTime { get; private set; } = string.Empty;

        public string EventNotes { get; private set; } = string.Empty;

        public bool IsOpenAttendance { get; private set; } = true;

        public CashForWorkBenefitType BenefitType { get; private set; } = CashForWorkBenefitType.None;

        public decimal UnitAmount { get; private set; } = 0m;

        public string? BenefitDescription { get; private set; }

        public void SetIsSeminarEvent(bool isSeminar)
        {
            OpenAttendanceCheckBox.Visibility = isSeminar ? Visibility.Visible : Visibility.Collapsed;
            HasBenefitCheckBox.Visibility = isSeminar ? Visibility.Visible : Visibility.Collapsed;
        }

        private void HasBenefitCheckBox_Click(object sender, RoutedEventArgs e)
        {
            SeminarBenefitControlsGrid.Visibility = HasBenefitCheckBox.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void SeminarBenefitTypeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (SeminarBenefitDetailTextBox == null) return;
            var isCash = SeminarBenefitTypeComboBox.SelectedIndex == 0;
            materialDesign:HintAssist.SetHint(SeminarBenefitDetailTextBox, isCash ? "Unit Amount (PHP)" : "Goods Detail / Pack");
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            var eventTitle = TitleTextBox.Text?.Trim() ?? string.Empty;
            var eventLocation = LocationTextBox.Text?.Trim() ?? string.Empty;
            var startTime = StartTimeTextBox.Text?.Trim() ?? string.Empty;
            var endTime = EndTimeTextBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(eventTitle) || string.IsNullOrWhiteSpace(eventLocation))
            {
                MessageBox.Show(
                    "Provide the event title and location first.",
                    "Missing Event Details",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!TimeSpan.TryParse(startTime, out _) || !TimeSpan.TryParse(endTime, out _))
            {
                MessageBox.Show(
                    "Use HH:mm format for start and end time.",
                    "Invalid Time",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            EventTitle = eventTitle;
            EventLocation = eventLocation;
            EventDate = EventDatePicker.SelectedDate ?? DateTime.Today;
            EventStartTime = startTime;
            EventEndTime = endTime;
            EventNotes = NotesTextBox.Text?.Trim() ?? string.Empty;
            IsOpenAttendance = OpenAttendanceCheckBox.IsChecked == true;

            if (HasBenefitCheckBox.IsChecked == true)
            {
                var isCash = SeminarBenefitTypeComboBox.SelectedIndex == 0;
                var detail = SeminarBenefitDetailTextBox.Text?.Trim() ?? string.Empty;

                if (isCash)
                {
                    BenefitType = CashForWorkBenefitType.Cash;
                    if (decimal.TryParse(detail, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out var amt))
                    {
                        UnitAmount = amt;
                    }
                    else if (decimal.TryParse(detail, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var amtInv))
                    {
                        UnitAmount = amtInv;
                    }
                }
                else
                {
                    BenefitType = CashForWorkBenefitType.Goods;
                    BenefitDescription = detail;
                }
            }
            else
            {
                BenefitType = CashForWorkBenefitType.None;
                UnitAmount = 0m;
                BenefitDescription = null;
            }

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
