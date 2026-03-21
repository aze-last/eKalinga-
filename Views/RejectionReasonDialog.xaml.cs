using System.Windows;

namespace AttendanceShiftingManagement.Views
{
    public partial class RejectionReasonDialog : Window
    {
        public string RejectionReason { get; private set; } = string.Empty;

        public RejectionReasonDialog()
        {
            InitializeComponent();
        }

        private void Reject_Click(object sender, RoutedEventArgs e)
        {
            RejectionReason = ReasonTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
