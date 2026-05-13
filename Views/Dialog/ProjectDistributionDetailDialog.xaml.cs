using System.Windows;

namespace AttendanceShiftingManagement.Views.Dialog
{
    public partial class ProjectDistributionDetailDialog : Window
    {
        public ProjectDistributionDetailDialog()
        {
            InitializeComponent();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
