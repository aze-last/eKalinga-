using System.Windows;

namespace AttendanceShiftingManagement.Views.Dialog
{
    public partial class MasterListDetailDialog : Window
    {
        public MasterListDetailDialog(object viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
