using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace AttendanceShiftingManagement.Views
{
    public enum SettingsWindowSection
    {
        SystemUserManagement = 0,
        SystemProfile = 1,
        MyAccount = 2,
        Security = 3,
        CrsConnection = 4,
        RemoteSnapshot = 4,
        DatabaseBackup = 5,
        AppDatabase = 6,
        GgmsBudgetSource = 7,
        Updates = 8,
        FeatureRules = 9
    }

    public partial class SettingsWindow : Window
    {
        private readonly SettingsWindowSection _initialSection;
        private readonly bool _checkForUpdatesOnOpen;
        private ScrollViewer? _tabsScroll;
        private RepeatButton? _tabsNavLeft;
        private RepeatButton? _tabsNavRight;

        private SettingsToolsViewModel ViewModel => (SettingsToolsViewModel)DataContext;

        public SettingsWindow(
            User? currentUser = null,
            SettingsWindowSection initialSection = SettingsWindowSection.SystemProfile,
            bool checkForUpdatesOnOpen = false)
        {
            InitializeComponent();
            _initialSection = initialSection;
            _checkForUpdatesOnOpen = checkForUpdatesOnOpen;
            DataContext = new SettingsToolsViewModel(currentUser);
            ViewModel.AdvancedLoadTablesRequested += OpenAdvancedLoadTables;
            Loaded += SettingsWindow_Loaded;
            WindowBrandingService.ApplyWindowIcon(this);

            if (UserManagementTab.DataContext is UserManagementViewModel userVm)
            {
                userVm.CurrentUser = currentUser;
            }
        }

        private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= SettingsWindow_Loaded;
            SelectInitialSection();
            HookupTabsNavigation();
            ScrollSelectedTabIntoView();

            if (_initialSection != SettingsWindowSection.Updates || !_checkForUpdatesOnOpen)
            {
                return;
            }

            await Dispatcher.InvokeAsync(
                () =>
                {
                    if (ViewModel.CheckForUpdatesCommand.CanExecute(null))
                    {
                        ViewModel.CheckForUpdatesCommand.Execute(null);
                    }
                },
                DispatcherPriority.Background);
        }

        private void SelectInitialSection()
        {
            if (SettingsTabs == null)
            {
                return;
            }

            SettingsTabs.SelectedIndex = (int)_initialSection;
        }

        private void HookupTabsNavigation()
        {
            _tabsScroll = SettingsTabs.Template.FindName("TabsScroll", SettingsTabs) as ScrollViewer;
            _tabsNavLeft = SettingsTabs.Template.FindName("TabsNavLeft", SettingsTabs) as RepeatButton;
            _tabsNavRight = SettingsTabs.Template.FindName("TabsNavRight", SettingsTabs) as RepeatButton;
            UpdateTabsNavButtons();
        }

        private void SettingsTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, SettingsTabs))
            {
                return;
            }

            ScrollSelectedTabIntoView();
        }

        private void ScrollSelectedTabIntoView()
        {
            var selectedHeader = SettingsTabs.ItemContainerGenerator
                .ContainerFromItem(SettingsTabs.SelectedItem) as TabItem;

            selectedHeader?.BringIntoView();
            UpdateTabsNavButtons();
        }

        private void TabsScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            UpdateTabsNavButtons();
        }

        private void TabsNavLeft_Click(object sender, RoutedEventArgs e)
        {
            _tabsScroll?.LineLeft();
        }

        private void TabsNavRight_Click(object sender, RoutedEventArgs e)
        {
            _tabsScroll?.LineRight();
        }

        private void UpdateTabsNavButtons()
        {
            if (_tabsScroll == null || _tabsNavLeft == null || _tabsNavRight == null)
            {
                return;
            }

            _tabsNavLeft.IsEnabled = _tabsScroll.HorizontalOffset > 1;
            _tabsNavRight.IsEnabled = _tabsScroll.HorizontalOffset < _tabsScroll.ScrollableWidth - 1;
        }

        private void OpenAdvancedLoadTables()
        {
            var window = new LoadTablesWindow
            {
                Owner = this
            };

            window.ShowDialog();
            ViewModel.RefreshPreviewCommand.Execute(null);
        }

        private void OpenAppDatabaseSettings_Click(object sender, RoutedEventArgs e)
        {
            var window = new ConnectionSettingsWindow(selectionOnly: false, requireOtpOnSave: true)
            {
                Owner = this
            };

            window.ShowDialog();
            ViewModel.RefreshPreviewCommand.Execute(null);
        }

        private void CurrentPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                ViewModel.CurrentPassword = passwordBox.Password;
            }
        }

        private void ProtectedSettingsUnlockPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                ViewModel.SensitiveSettingsUnlockPassword = passwordBox.Password;
            }
        }

        private void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                ViewModel.NewPassword = passwordBox.Password;
            }
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                ViewModel.ConfirmPassword = passwordBox.Password;
            }
        }

        private async void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.HandleChangePasswordAsync();
            if (!ViewModel.LastPasswordChangeSucceeded)
            {
                return;
            }

            if (FindName("CurrentPasswordBox") is PasswordBox currentPasswordBox)
            {
                currentPasswordBox.Clear();
            }

            if (FindName("NewPasswordBox") is PasswordBox newPasswordBox)
            {
                newPasswordBox.Clear();
            }

            if (FindName("ConfirmPasswordBox") is PasswordBox confirmPasswordBox)
            {
                confirmPasswordBox.Clear();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }

            base.OnClosed(e);
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
