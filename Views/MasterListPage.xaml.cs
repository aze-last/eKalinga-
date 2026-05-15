using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AttendanceShiftingManagement.Views
{
    public partial class MasterListPage : UserControl
    {
        private readonly MasterListViewModel _viewModel;

        public MasterListPage(User currentUser)
        {
            InitializeComponent();
            _viewModel = new MasterListViewModel(currentUser);
            DataContext = _viewModel;
        }

        private void MasterListPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Open filters on load as per Pattern B
            OpenFilterDialog();
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            OpenFilterDialog();
        }

        private void OpenFilterDialog()
        {
            _viewModel.IsFilterPanelOpen = true;
            try
            {
                var dialog = new Dialog.MasterListFilterDialog(_viewModel)
                {
                    Owner = Window.GetWindow(this)
                };
                dialog.ShowDialog();
            }
            finally
            {
                _viewModel.IsFilterPanelOpen = false;
            }
        }

        private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var grid = sender as DataGrid;
            if (grid?.SelectedItem == null) return;

            _viewModel.IsDetailPanelOpen = true;
            try
            {
                var dialog = new Dialog.MasterListDetailDialog(_viewModel)
                {
                    Owner = Window.GetWindow(this)
                };
                dialog.ShowDialog();
            }
            finally
            {
                _viewModel.IsDetailPanelOpen = false;
            }
        }
    }
}
