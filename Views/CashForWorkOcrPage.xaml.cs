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
    }
}
