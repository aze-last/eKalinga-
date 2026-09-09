using AttendanceShiftingManagement.Services;
using AttendanceShiftingManagement.ViewModels;

namespace AttendanceShiftingManagement.Tests;

public sealed class ConnectionSettingsViewModelTests
{
    [Fact]
    public void SelectionOnlyMode_OnlyLanRemainsEditableBeforeSignIn()
    {
        var viewModel = new ConnectionSettingsViewModel(selectionOnly: true, initialSettings: BuildEditableSettings());

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
        var viewModel = new ConnectionSettingsViewModel(selectionOnly: true, initialSettings: BuildEditableSettings());
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
    public void AdminMode_LanAndRemotePresetsAreEditable()
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
        Assert.True(viewModel.IsRemoteSelected);
        Assert.True(viewModel.CanEditSelectedPresetCredentials);
        Assert.True(viewModel.ShowCredentialEditor);
        Assert.False(viewModel.ShowLanCredentialEditor);
        Assert.False(viewModel.ShowFixedPresetNotice);
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

    [Fact]
    public void LanDatabases_InitializedFromSettings_ContainsDefaultLan()
    {
        var settings = BuildEditableSettings();
        var viewModel = new ConnectionSettingsViewModel(
            selectionOnly: false,
            initialSettings: settings);

        Assert.NotEmpty(viewModel.LanDatabases);
        var defaultLan = viewModel.LanDatabases.FirstOrDefault(l => l.Key == "Lan");
        Assert.NotNull(defaultLan);
        Assert.True(defaultLan.IsDefault);
        Assert.True(defaultLan.IsEnabled);
    }

    [Fact]
    public void AddLanDatabaseCommand_CreatesNewLanPreset_AndSelectsIt()
    {
        var settings = BuildEditableSettings();
        var viewModel = new ConnectionSettingsViewModel(
            selectionOnly: false,
            initialSettings: settings);

        var initialCount = viewModel.LanDatabases.Count;
        Assert.True(viewModel.AddLanDatabaseCommand.CanExecute(null));
        viewModel.AddLanDatabaseCommand.Execute(null);

        Assert.Equal(initialCount + 1, viewModel.LanDatabases.Count);
        Assert.StartsWith("Lan_", viewModel.SelectedPresetKey);
        Assert.True(viewModel.IsLanSelected);
        Assert.True(viewModel.CanEditSelectedPresetCredentials);

        var addedItem = viewModel.LanDatabases.Last();
        Assert.False(addedItem.IsDefault);
        Assert.True(addedItem.IsEnabled);
        Assert.True(addedItem.IsSelected);
    }

    [Fact]
    public void RemoveLanDatabaseCommand_RemovesCustomPreset_ProtectsDefaultLan()
    {
        var settings = BuildEditableSettings();
        var viewModel = new ConnectionSettingsViewModel(
            selectionOnly: false,
            initialSettings: settings);

        viewModel.AddLanDatabaseCommand.Execute(null);
        var customItem = viewModel.LanDatabases.Last();
        Assert.False(customItem.IsDefault);

        viewModel.RemoveLanDatabaseCommand.Execute(customItem);
        Assert.DoesNotContain(customItem, viewModel.LanDatabases);
        Assert.Equal("Lan", viewModel.SelectedPresetKey);

        var defaultLan = viewModel.LanDatabases.First(l => l.Key == "Lan");
        viewModel.RemoveLanDatabaseCommand.Execute(defaultLan);
        Assert.Contains(defaultLan, viewModel.LanDatabases);
    }

    [Fact]
    public void SaveCommand_PersistsAllLanDatabases_AndEnabledStatus()
    {
        ConnectionSettingsModel? savedModel = null;
        var settings = BuildEditableSettings();
        var viewModel = new ConnectionSettingsViewModel(
            selectionOnly: false,
            requireOtpOnSave: false,
            initialSettings: settings,
            saveSettings: m => savedModel = m);

        viewModel.AddLanDatabaseCommand.Execute(null);
        var customItem = viewModel.LanDatabases.Last();
        viewModel.Server = "192.168.1.150";
        viewModel.Database = "ams_crs_mirror";
        customItem.IsEnabled = false;

        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(savedModel);
        var lanPresets = savedModel.GetLanPresets().ToList();
        Assert.Equal(2, lanPresets.Count);

        var customSaved = lanPresets.FirstOrDefault(p => p.Key == customItem.Key);
        Assert.NotNull(customSaved.Value);
        Assert.Equal("192.168.1.150", customSaved.Value.Server);
        Assert.Equal("ams_crs_mirror", customSaved.Value.Database);
        Assert.False(customSaved.Value.IsEnabled);
    }

    [Fact]
    public void LanDatabaseItemViewModel_StatusMethods_UpdateProperties()
    {
        var item = new LanDatabaseItemViewModel
        {
            Key = "Lan_1",
            DisplayName = "Test DB"
        };

        Assert.Equal("Untested", item.ConnectionStatusText);
        Assert.False(item.IsTesting);

        item.SetTestingStatus();
        Assert.True(item.IsTesting);
        Assert.Equal("Testing...", item.ConnectionStatusText);

        item.SetSuccessStatus("Connected (OK)");
        Assert.False(item.IsTesting);
        Assert.Equal("Connected (OK)", item.ConnectionStatusText);

        item.SetFailedStatus("Timeout");
        Assert.False(item.IsTesting);
        Assert.Equal("Timeout", item.ConnectionStatusText);

        item.ResetStatus();
        Assert.False(item.IsTesting);
        Assert.Equal("Untested", item.ConnectionStatusText);
    }

    [Fact]
    public void TestSpecificLanDatabaseCommand_WithIncompleteConfiguration_SetsFailedStatus()
    {
        var settings = BuildEditableSettings();
        var viewModel = new ConnectionSettingsViewModel(
            selectionOnly: false,
            initialSettings: settings);

        var item = new LanDatabaseItemViewModel
        {
            Key = "Lan_Test",
            DisplayName = "Incomplete DB",
            Server = "",
            Database = ""
        };

        viewModel.LanDatabases.Add(item);
        viewModel.TestSpecificLanDatabaseCommand.Execute(item);

        Assert.Contains("Missing", item.ConnectionStatusText);
        Assert.Contains("missing", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
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
