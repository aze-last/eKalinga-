using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.ViewModels;

namespace AttendanceShiftingManagement.Tests;

public sealed class ConnectionSettingsViewModelTests
{
    [Fact]
    public void SelectionOnlyMode_OnlyLanRemainsEditableBeforeSignIn()
    {
        var viewModel = new ConnectionSettingsViewModel(selectionOnly: true);

        Assert.True(viewModel.IsSelectionOnly);
        Assert.False(viewModel.ShowCredentialEditor);
        Assert.False(viewModel.ShowLanCredentialEditor);
        Assert.True(viewModel.ShowFixedPresetNotice);
        Assert.False(viewModel.CanEditSelectedPresetCredentials);
        Assert.True(viewModel.TestConnectionCommand.CanExecute(null));

        viewModel.SelectLanPresetCommand.Execute(null);

        Assert.True(viewModel.IsLanSelected);
        Assert.True(viewModel.ShowCredentialEditor);
        Assert.True(viewModel.ShowLanCredentialEditor);
        Assert.False(viewModel.ShowFixedPresetNotice);
        Assert.True(viewModel.CanEditSelectedPresetCredentials);

        viewModel.SelectRemotePresetCommand.Execute(null);

        Assert.True(viewModel.IsRemoteSelected);
        Assert.False(viewModel.ShowCredentialEditor);
        Assert.False(viewModel.ShowLanCredentialEditor);
        Assert.True(viewModel.ShowFixedPresetNotice);
        Assert.False(viewModel.CanEditSelectedPresetCredentials);
    }

    [Fact]
    public void SelectionOnlyMode_RaisesShowCredentialEditorChange_WhenLanIsSelected()
    {
        var viewModel = new ConnectionSettingsViewModel(selectionOnly: true);
        var changedProperties = new List<string>();

        viewModel.PropertyChanged += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.PropertyName))
            {
                changedProperties.Add(args.PropertyName);
            }
        };

        viewModel.SelectLanPresetCommand.Execute(null);

        Assert.Contains(nameof(ConnectionSettingsViewModel.ShowCredentialEditor), changedProperties);
        Assert.Contains(nameof(ConnectionSettingsViewModel.ShowLanCredentialEditor), changedProperties);
    }

    [Fact]
    public void AdminMode_OnlyLanPresetIsEditable()
    {
        var viewModel = new ConnectionSettingsViewModel(selectionOnly: false);

        viewModel.SelectLocalPresetCommand.Execute(null);
        Assert.False(viewModel.CanEditSelectedPresetCredentials);
        Assert.False(viewModel.ShowCredentialEditor);
        Assert.False(viewModel.ShowLanCredentialEditor);
        Assert.True(viewModel.ShowFixedPresetNotice);

        viewModel.SelectLanPresetCommand.Execute(null);
        Assert.True(viewModel.IsLanSelected);
        Assert.True(viewModel.CanEditSelectedPresetCredentials);
        Assert.True(viewModel.ShowCredentialEditor);
        Assert.True(viewModel.ShowLanCredentialEditor);
        Assert.False(viewModel.ShowFixedPresetNotice);

        viewModel.SelectRemotePresetCommand.Execute(null);
        Assert.False(viewModel.CanEditSelectedPresetCredentials);
        Assert.False(viewModel.ShowCredentialEditor);
        Assert.False(viewModel.ShowLanCredentialEditor);
        Assert.True(viewModel.ShowFixedPresetNotice);
    }

    [Fact]
    public void AdminMode_AllPresetsStillAllowTesting()
    {
        var viewModel = new ConnectionSettingsViewModel(selectionOnly: false);

        viewModel.SelectLocalPresetCommand.Execute(null);
        Assert.True(viewModel.TestConnectionCommand.CanExecute(null));

        viewModel.SelectRemotePresetCommand.Execute(null);
        Assert.True(viewModel.TestConnectionCommand.CanExecute(null));
    }

    [Fact]
    public void AdminMode_WhenOtpIsRequired_SaveWithoutChanges_DoesNotOpenOtpPanel()
    {
        var saveCallCount = 0;
        bool? closeResult = null;
        var viewModel = new ConnectionSettingsViewModel(
            selectionOnly: false,
            requireOtpOnSave: true,
            initialSettings: BuildEditableSettings(),
            saveSettings: _ => saveCallCount++);

        viewModel.CloseRequested += result => closeResult = result;

        viewModel.SaveCommand.Execute(null);

        Assert.False(viewModel.ShowSaveOtpPanel);
        Assert.Equal(1, saveCallCount);
        Assert.True(closeResult);
    }

    [Fact]
    public void AdminMode_WhenOtpIsRequired_SaveWithChanges_OpensOtpPanelBeforeSaving()
    {
        var saveCallCount = 0;
        bool? closeResult = null;
        var viewModel = new ConnectionSettingsViewModel(
            selectionOnly: false,
            requireOtpOnSave: true,
            initialSettings: BuildEditableSettings(),
            saveSettings: _ => saveCallCount++);

        viewModel.CloseRequested += result => closeResult = result;
        viewModel.SelectLanPresetCommand.Execute(null);
        viewModel.Server = "10.0.0.55";

        viewModel.SaveCommand.Execute(null);

        Assert.True(viewModel.ShowSaveOtpPanel);
        Assert.Equal(0, saveCallCount);
        Assert.Null(closeResult);
    }

    private static ConnectionSettingsModel BuildEditableSettings()
    {
        return new ConnectionSettingsModel
        {
            SelectedPreset = "Local",
            Presets = new Dictionary<string, DatabaseConnectionPreset>(StringComparer.OrdinalIgnoreCase)
            {
                ["Local"] = new DatabaseConnectionPreset
                {
                    DisplayName = "Local",
                    Server = "127.0.0.1",
                    Port = 3306,
                    Database = "local_db",
                    Username = "root",
                    Password = "local"
                },
                ["Lan"] = new DatabaseConnectionPreset
                {
                    DisplayName = "Network (LAN)",
                    Server = "192.168.1.10",
                    Port = 3306,
                    Database = "lan_db",
                    Username = "lan_user",
                    Password = "lan_pass"
                },
                ["Remote"] = new DatabaseConnectionPreset
                {
                    DisplayName = "Remote",
                    Server = "db.example.com",
                    Port = 3306,
                    Database = "remote_db",
                    Username = "remote_user",
                    Password = "remote_pass"
                }
            }
        };
    }
}
