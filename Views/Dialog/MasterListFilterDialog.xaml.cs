using AttendanceShiftingManagement.ViewModels;
using System.Windows;

namespace AttendanceShiftingManagement.Views.Dialog
{
    public partial class MasterListFilterDialog : Window
    {
        private readonly MasterListViewModel _viewModel;

        public MasterListFilterDialog(MasterListViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
