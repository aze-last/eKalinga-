using AttendanceShiftingManagement.Models;
using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AttendanceShiftingManagement.Views
{
    public enum SettingsWindowSection
    {
        SystemProfile = 0,
        DatabaseBackup = 1,
        AppDatabase = 2,
        GgmsBudgetSource = 3,
        Updates = 4,
        FeatureRules = 5
    }

    public partial class SettingsWindow : Window
    {
        private readonly SettingsWindowSection _initialSection;
        private readonly bool _checkForUpdatesOnOpen;

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
        }

        private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= SettingsWindow_Loaded;
            SelectInitialSection();

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
