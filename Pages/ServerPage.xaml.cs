using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ASAServerManager.Server;
using ASAServerManager.SteamCMD;

namespace ASAServerManager.Pages
{
    public partial class ServerPage : UserControl
    {
        // =========================================================
        // MULTI-SERVER IDENTITY
        // =========================================================

        public string ServerDisplayName { get; }

        public string ServerId { get; }

        public bool IsServerRunning =>
            _asaServerManager != null &&
            _asaServerManager.IsRunning;

        // =========================================================
        // PATHS / MANAGERS
        // =========================================================

        private static readonly HashSet<ServerPage> OpenServerPages =
        new HashSet<ServerPage>();
        private readonly string _steamCmdDirectory;
        private readonly string _steamCmdPath;
        private readonly AsaServerManager _asaServerManager;
        private readonly SteamCmdDownloader _steamCmdDownloader;

        private bool _operationRunning;
        private bool _serverOperationRunning;
        private bool _loadingConfiguration;

        private const int AsaAppId = 2430930;

        // =========================================================
        // CONFIGURATION
        // =========================================================

        private class ServerConfiguration
        {
            public string ServerPath { get; set; } = "";
            public string ServerName { get; set; } = "ASA Server";
            public string Map { get; set; } = "TheIsland_WP";
            public string ServerPort { get; set; } = "7777";
            public string QueryPort { get; set; } = "27015";
            public string MaxPlayers { get; set; } = "70";
            public string Difficulty { get; set; } = "1.0";

           [JsonIgnore]
            public string ServerPassword { get; set; } = "";

            [JsonIgnore]
            public string AdminPassword { get; set; } = "";

            public bool ServerPasswordEnabled { get; set; }
            public bool AdminPasswordEnabled { get; set; }

            public bool PvE { get; set; }
            public bool Crossplay { get; set; } = true;

            public string Mods { get; set; } = "";

            public bool ClusterEnabled { get; set; }
            public string ClusterId { get; set; } = "ASACluster";
            public string ClusterDirectory { get; set; } = "";

            public bool WhitelistEnabled { get; set; }

                public string WhitelistIds { get; set; } = "";

                public string WhitelistMode { get; set; } = "Steam IDs";
                    }

        // =========================================================
        // CONSTRUCTOR
        // =========================================================
        

        public ServerPage(
            string serverDisplayName,
            string serverId)
        {
            InitializeComponent();
            OpenServerPages.Add(this);

            ServerDisplayName =
                string.IsNullOrWhiteSpace(serverDisplayName)
                    ? "ASA Server"
                    : serverDisplayName;

            ServerId =
                string.IsNullOrWhiteSpace(serverId)
                    ? Guid.NewGuid().ToString("N")
                    : serverId;

            // =====================================================
            // ASA SERVER MANAGER
            // =====================================================

            _asaServerManager =
                new AsaServerManager();

            _asaServerManager.OutputReceived +=
                AsaServer_OutputReceived;

            _asaServerManager.ErrorReceived +=
                AsaServer_ErrorReceived;

            _asaServerManager.ServerExited +=
                AsaServer_Exited;

            // =====================================================
            // STEAMCMD
            //
            // Each ServerPage gets its own SteamCMD directory.
            // =====================================================

            _steamCmdDirectory =
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "SteamCMD",
                    ServerId);

            _steamCmdPath =
                Path.Combine(
                    _steamCmdDirectory,
                    "steamcmd.exe");

            _steamCmdDownloader =
                new SteamCmdDownloader(
                    _steamCmdDirectory);

            // =====================================================
            // LOAD SERVER-SPECIFIC CONFIGURATION
            // =====================================================

            LoadConfiguration();
            UpdateSteamCmdStatus();
            UpdatePasswordControls();
            UpdateWhitelistControls();
            UpdateWhitelistSummary();
            UpdateServerStatus();
            UpdateServerButtons();
            UpdateLaunchOptions();

            UpdateRuntimeStatus(
                _asaServerManager.IsRunning
                    ? "● RUNNING"
                    : "● OFFLINE",
                _asaServerManager.IsRunning
                    ? Brushes.LightGreen
                    : Brushes.IndianRed);
        }

        // =========================================================
        // PASSWORD SHOW/HIDE HANDLERS
        // =========================================================

       private void ServerPasswordToggleCheckBox_Checked(
    object sender,
    RoutedEventArgs e)
{
    if (ServerPasswordBox == null ||
        ServerPasswordTextBox == null)
        return;

    ServerPasswordTextBox.Text =
        ServerPasswordBox.Password;

    ServerPasswordBox.Visibility =
        Visibility.Collapsed;

    ServerPasswordTextBox.Visibility =
        Visibility.Visible;

    ServerPasswordTextBox.Focus();

    UpdateLaunchOptions();
}

private void ServerPasswordToggleCheckBox_Unchecked(
    object sender,
    RoutedEventArgs e)
{
    if (ServerPasswordBox == null ||
        ServerPasswordTextBox == null)
        return;

    ServerPasswordBox.Password =
        ServerPasswordTextBox.Text;

    ServerPasswordTextBox.Visibility =
        Visibility.Collapsed;

    ServerPasswordBox.Visibility =
        Visibility.Visible;

    ServerPasswordBox.Focus();

    UpdateLaunchOptions();
}
        private void AdminPasswordToggleCheckBox_Checked(
    object sender,
    RoutedEventArgs e)
{
    if (AdminPasswordBox == null ||
        AdminPasswordTextBox == null)
        return;

    AdminPasswordTextBox.Text =
        AdminPasswordBox.Password;

    AdminPasswordBox.Visibility =
        Visibility.Collapsed;

    AdminPasswordTextBox.Visibility =
        Visibility.Visible;

    AdminPasswordTextBox.Focus();

    UpdateLaunchOptions();
}

private void AdminPasswordToggleCheckBox_Unchecked(
    object sender,
    RoutedEventArgs e)
{
    if (AdminPasswordBox == null ||
        AdminPasswordTextBox == null)
        return;

    AdminPasswordBox.Password =
        AdminPasswordTextBox.Text;

    AdminPasswordTextBox.Visibility =
        Visibility.Collapsed;

    AdminPasswordBox.Visibility =
        Visibility.Visible;

    AdminPasswordBox.Focus();

    UpdateLaunchOptions();
}

        // =========================================================
        // CONFIG PATH
        // =========================================================

        private string GetConfigDirectory()
        {
            string directory =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "ASA Server Manager");

            Directory.CreateDirectory(directory);

            return directory;
        }

        private string GetConfigPath()
        {
            return Path.Combine(
                GetConfigDirectory(),
                $"server_{ServerId}_config.json");
        }

        // =========================================================
        // ASA CONFIG DIRECTORY
        // =========================================================

        private string GetAsaConfigDirectory()
        {
            string serverDirectory =
                ServerPathTextBox?.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(serverDirectory))
                return "";

            return Path.Combine(
                serverDirectory,
                "ShooterGame",
                "Saved",
                "Config",
                "WindowsServer");
        }

        private string GetGameIniPath()
        {
            string directory =
                GetAsaConfigDirectory();

            if (string.IsNullOrWhiteSpace(directory))
                return "";

            return Path.Combine(
                directory,
                "Game.ini");
        }

        private string GetGameUserSettingsIniPath()
        {
            string directory =
                GetAsaConfigDirectory();

            if (string.IsNullOrWhiteSpace(directory))
                return "";

            return Path.Combine(
                directory,
                "GameUserSettings.ini");
        }
private void SavePasswordsToGameUserSettingsIni(
    string serverPassword,
    string adminPassword)
{
    try
    {
        string iniPath =
            GetGameUserSettingsIniPath();

        if (string.IsNullOrWhiteSpace(iniPath))
            return;

        string? directory =
            Path.GetDirectoryName(iniPath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        List<string> lines =
            File.Exists(iniPath)
                ? File.ReadAllLines(iniPath).ToList()
                : new List<string>();

        SetIniValue(
            lines,
            "ServerPassword",
            serverPassword ?? "");

        SetIniValue(
            lines,
            "ServerAdminPassword",
            adminPassword ?? "");

        File.WriteAllLines(
            iniPath,
            lines);
    }
    catch (Exception ex)
    {
        AppendConsole("");
        AppendConsole(
            "PASSWORD INI SAVE ERROR:");

        AppendConsole(
            ex.ToString());
    }
}

private void LoadPasswordsFromGameUserSettingsIni(
    ServerConfiguration config)
{
    if (config == null)
        return;

    try
    {
        if (string.IsNullOrWhiteSpace(config.ServerPath))
        {
            AppendConsole(
                "PASSWORD LOAD: Server path is empty.");

            return;
        }

        string iniPath =
            Path.Combine(
                config.ServerPath,
                "ShooterGame",
                "Saved",
                "Config",
                "WindowsServer",
                "GameUserSettings.ini");

        AppendConsole(
            "PASSWORD LOAD: Checking:");

        AppendConsole(
            iniPath);

        if (!File.Exists(iniPath))
        {
            AppendConsole(
                "PASSWORD LOAD: GameUserSettings.ini not found.");

            return;
        }

        string[] lines =
            File.ReadAllLines(iniPath);

        bool inServerSettings = false;

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            // -------------------------------------------------
            // SECTION
            // -------------------------------------------------

            if (line.StartsWith("[") &&
                line.EndsWith("]"))
            {
                inServerSettings =
                    string.Equals(
                        line,
                        "[ServerSettings]",
                        StringComparison.OrdinalIgnoreCase);

                continue;
            }

            if (!inServerSettings)
                continue;

            // -------------------------------------------------
            // KEY / VALUE
            // -------------------------------------------------

            int equalsIndex =
                line.IndexOf('=');

            if (equalsIndex <= 0)
                continue;

            string key =
                line.Substring(
                    0,
                    equalsIndex)
                .Trim();

            string value =
                line.Substring(
                    equalsIndex + 1)
                .Trim();

            // -------------------------------------------------
            // SERVER PASSWORD
            // -------------------------------------------------

            if (string.Equals(
                key,
                "ServerPassword",
                StringComparison.OrdinalIgnoreCase))
            {
                config.ServerPassword =
                    value;

                AppendConsole(
                    "PASSWORD LOAD: Server password found.");
            }

            // -------------------------------------------------
            // ADMIN PASSWORD
            // -------------------------------------------------

            else if (
                string.Equals(
                    key,
                    "ServerAdminPassword",
                    StringComparison.OrdinalIgnoreCase))
            {
                config.AdminPassword =
                    value;

                AppendConsole(
                    "PASSWORD LOAD: Admin password found.");
            }
        }

        if (string.IsNullOrWhiteSpace(
            config.ServerPassword))
        {
            AppendConsole(
                "PASSWORD LOAD: No ServerPassword found.");
        }

        if (string.IsNullOrWhiteSpace(
            config.AdminPassword))
        {
            AppendConsole(
                "PASSWORD LOAD: No ServerAdminPassword found.");
        }
    }
    catch (Exception ex)
    {
        AppendConsole(
            "PASSWORD LOAD ERROR:");

        AppendConsole(
            ex.ToString());
    }
}


private void SetIniValue(
    List<string> lines,
    string key,
    string value)
{
    string prefix = key + "=";

    for (int i = 0; i < lines.Count; i++)
    {
        string line = lines[i];

        if (line.TrimStart()
            .StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
        {
            lines[i] = prefix + value;
            return;
        }
    }

    lines.Add(prefix + value);
}

private string GetIniValue(
    List<string> lines,
    string key)
{
    string prefix = key + "=";

    foreach (string line in lines)
    {
        string trimmed = line.TrimStart();

        if (trimmed.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.Substring(
                prefix.Length);
        }
    }

    return "";
}

private void UpdateIniValue(
    List<string> lines,
    string section,
    string key,
    string value)
{
    int sectionStart = -1;
    int sectionEnd = lines.Count;

    for (int i = 0; i < lines.Count; i++)
    {
        string line =
            lines[i].Trim();

        if (line.StartsWith("[") &&
            line.EndsWith("]"))
        {
            string currentSection =
                line.Substring(
                    1,
                    line.Length - 2)
                .Trim();

            if (string.Equals(
                currentSection,
                section,
                StringComparison.OrdinalIgnoreCase))
            {
                sectionStart = i;
                continue;
            }

            if (sectionStart >= 0)
            {
                sectionEnd = i;
                break;
            }
        }
    }

    // =====================================================
    // CREATE SECTION IF IT DOES NOT EXIST
    // =====================================================

    if (sectionStart < 0)
    {
        if (lines.Count > 0 &&
            !string.IsNullOrWhiteSpace(
                lines[lines.Count - 1]))
        {
            lines.Add("");
        }

        lines.Add(
            "[" + section + "]");

        lines.Add(
            key + "=" + value);

        return;
    }

    // =====================================================
    // LOOK FOR EXISTING KEY
    // =====================================================

    for (int i = sectionStart + 1;
         i < sectionEnd;
         i++)
    {
        string trimmed =
            lines[i].Trim();

        if (trimmed.StartsWith(
            key + "=",
            StringComparison.OrdinalIgnoreCase))
        {
            lines[i] =
                key + "=" + value;

            return;
        }
    }

    // =====================================================
    // KEY DOES NOT EXIST - ADD IT
    // =====================================================

    lines.Insert(
        sectionEnd,
        key + "=" + value);
}

private string GetIniValue(
    List<string> lines,
    string section,
    string key)
{
    bool insideSection = false;

    foreach (string rawLine in lines)
    {
        string line =
            rawLine.Trim();

        if (line.StartsWith("[") &&
            line.EndsWith("]"))
        {
            string currentSection =
                line.Substring(
                    1,
                    line.Length - 2)
                .Trim();

            insideSection =
                string.Equals(
                    currentSection,
                    section,
                    StringComparison.OrdinalIgnoreCase);

            continue;
        }

        if (!insideSection)
            continue;

        if (line.StartsWith(
            key + "=",
            StringComparison.OrdinalIgnoreCase))
        {
            return line.Substring(
                key.Length + 1);
        }
    }

    return "";
}

        // =========================================================
        // WHITELIST PATH
        // =========================================================

        private string GetWhitelistDirectory()
        {
            string serverDirectory =
                ServerPathTextBox?.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(serverDirectory))
                return "";

            return Path.Combine(
                serverDirectory,
                "ShooterGame",
                "Binaries",
                "Win64");
        }

        private string GetWhitelistPath()
        {
            string directory =
                GetWhitelistDirectory();

            if (string.IsNullOrWhiteSpace(directory))
                return "";

            return Path.Combine(
                directory,
                "PlayersExclusiveJoinList.txt");
        }

        // =========================================================
        // CONFIGURATION FROM UI
        // =========================================================

        private ServerConfiguration GetConfigurationFromUI()
{
    string serverPassword = "";

    if (ServerPasswordBox != null)
    {
        serverPassword =
            ServerPasswordBox.Password;
    }

    if (ServerPasswordTextBox != null &&
        ServerPasswordTextBox.Visibility ==
        Visibility.Visible)
    {
        serverPassword =
            ServerPasswordTextBox.Text ?? "";
    }

    string adminPassword = "";

    if (AdminPasswordBox != null)
    {
        adminPassword =
            AdminPasswordBox.Password;
    }

    if (AdminPasswordTextBox != null &&
        AdminPasswordTextBox.Visibility ==
        Visibility.Visible)
    {
        adminPassword =
            AdminPasswordTextBox.Text ?? "";
    }

    return new ServerConfiguration
    {
        ServerPath =
            ServerPathTextBox?.Text?.Trim() ?? "",

        ServerName =
            string.IsNullOrWhiteSpace(
                ServerNameTextBox?.Text)
                ? "ASA Server"
                : ServerNameTextBox.Text.Trim(),

        Map =
            GetComboBoxValue(
                MapComboBox,
                "TheIsland_WP"),

        ServerPort =
            string.IsNullOrWhiteSpace(
                ServerPortTextBox?.Text)
                ? "7777"
                : ServerPortTextBox.Text.Trim(),

        QueryPort =
            string.IsNullOrWhiteSpace(
                QueryPortTextBox?.Text)
                ? "27015"
                : QueryPortTextBox.Text.Trim(),

        MaxPlayers =
            string.IsNullOrWhiteSpace(
                MaxPlayersTextBox?.Text)
                ? "70"
                : MaxPlayersTextBox.Text.Trim(),

        Difficulty =
            GetComboBoxValue(
                DifficultyComboBox,
                "1.0"),

        ServerPassword =
            serverPassword,

        ServerPasswordEnabled =
            ServerPasswordToggleCheckBox?.IsChecked == true,

        AdminPassword =
            adminPassword,

        AdminPasswordEnabled =
            AdminPasswordToggleCheckBox?.IsChecked == true,

        PvE =
            PvECheckBox?.IsChecked == true,

        Crossplay =
            CrossplayCheckBox?.IsChecked == true,

        Mods =
            ModsTextBox?.Text ?? "",

        ClusterEnabled =
            ClusterEnabledCheckBox?.IsChecked == true,

        ClusterId =
            string.IsNullOrWhiteSpace(
                ClusterIdTextBox?.Text)
                ? "ASACluster"
                : ClusterIdTextBox.Text.Trim(),

        ClusterDirectory =
            ClusterDirectoryTextBox?.Text?.Trim() ?? "",

        WhitelistEnabled =
            WhitelistEnabledCheckBox?.IsChecked == true,

        WhitelistIds =
            WhitelistTextBox?.Text ?? ""
    };
}

        // =========================================================
        // APPLY CONFIGURATION
        // =========================================================

        private void ApplyConfiguration(
    ServerConfiguration config)
{
    if (config == null)
        return;

    _loadingConfiguration = true;

    try
    {
        // =====================================================
        // SERVER SETTINGS
        // =====================================================

        if (ServerPathTextBox != null)
        {
            ServerPathTextBox.Text =
                config.ServerPath ?? "";
        }

        if (ServerNameTextBox != null)
        {
            ServerNameTextBox.Text =
                string.IsNullOrWhiteSpace(
                    config.ServerName)
                    ? "ASA Server"
                    : config.ServerName;
        }

        if (ServerPortTextBox != null)
        {
            ServerPortTextBox.Text =
                string.IsNullOrWhiteSpace(
                    config.ServerPort)
                    ? "7777"
                    : config.ServerPort;
        }

        if (QueryPortTextBox != null)
        {
            QueryPortTextBox.Text =
                string.IsNullOrWhiteSpace(
                    config.QueryPort)
                    ? "27015"
                    : config.QueryPort;
        }

        if (MaxPlayersTextBox != null)
        {
            MaxPlayersTextBox.Text =
                string.IsNullOrWhiteSpace(
                    config.MaxPlayers)
                    ? "70"
                    : config.MaxPlayers;
        }

        // =====================================================
        // PASSWORDS
        // =====================================================

        if (ServerPasswordBox != null)
        {
            ServerPasswordBox.Password =
                config.ServerPassword ?? "";
        }

        if (ServerPasswordTextBox != null)
        {
            ServerPasswordTextBox.Text =
                config.ServerPassword ?? "";
        }

        if (AdminPasswordBox != null)
        {
            AdminPasswordBox.Password =
                config.AdminPassword ?? "";
        }

        if (AdminPasswordTextBox != null)
        {
            AdminPasswordTextBox.Text =
                config.AdminPassword ?? "";
        }

        if (ServerPasswordToggleCheckBox != null)
        {
            ServerPasswordToggleCheckBox.IsChecked =
                config.ServerPasswordEnabled;
        }

        if (AdminPasswordToggleCheckBox != null)
        {
            AdminPasswordToggleCheckBox.IsChecked =
                config.AdminPasswordEnabled;
        }

        // =====================================================
        // OTHER SETTINGS
        // =====================================================

        if (PvECheckBox != null)
        {
            PvECheckBox.IsChecked =
                config.PvE;
        }

        if (CrossplayCheckBox != null)
        {
            CrossplayCheckBox.IsChecked =
                config.Crossplay;
        }

        if (ModsTextBox != null)
        {
            ModsTextBox.Text =
                config.Mods ?? "";
        }

        // =====================================================
        // CLUSTER
        // =====================================================

        if (ClusterEnabledCheckBox != null)
        {
            ClusterEnabledCheckBox.IsChecked =
                config.ClusterEnabled;
        }

        if (ClusterIdTextBox != null)
        {
            ClusterIdTextBox.Text =
                string.IsNullOrWhiteSpace(
                    config.ClusterId)
                    ? "ASACluster"
                    : config.ClusterId;
        }

        if (ClusterDirectoryTextBox != null)
        {
            ClusterDirectoryTextBox.Text =
                config.ClusterDirectory ?? "";
        }

        // =====================================================
        // WHITELIST
        // =====================================================

        if (WhitelistEnabledCheckBox != null)
        {
            WhitelistEnabledCheckBox.IsChecked =
                config.WhitelistEnabled;
        }

        if (WhitelistTextBox != null)
        {
            WhitelistTextBox.Text =
                config.WhitelistIds ?? "";
        }

        SetWhitelistMode(
            config.WhitelistMode);

        // =====================================================
        // COMBOBOXES
        // =====================================================

        SetComboBoxValue(
            MapComboBox,
            config.Map,
            "TheIsland_WP");

        SetComboBoxValue(
            DifficultyComboBox,
            config.Difficulty,
            "1.0");
    }
    finally
    {
        _loadingConfiguration = false;
    }

    // =====================================================
    // UPDATE UI AFTER EVERYTHING IS LOADED
    // =====================================================

    UpdatePasswordControls();
    UpdateWhitelistControls();
    UpdateWhitelistSummary();
    UpdateServerStatus();
    UpdateLaunchOptions();
}

        // =========================================================
        // COMBOBOX HELPERS
        // =========================================================

        private string GetComboBoxValue(
    ComboBox comboBox,
    string defaultValue)
{
    if (comboBox == null)
        return defaultValue;

    if (comboBox.SelectedItem is ComboBoxItem item)
    {
        string value =
            item.Content?.ToString() ?? "";

        if (!string.IsNullOrWhiteSpace(value))
            return value;
    }

    if (!string.IsNullOrWhiteSpace(comboBox.Text))
        return comboBox.Text.Trim();

    return defaultValue;
}


private void SetComboBoxValue(
    ComboBox comboBox,
    string value,
    string defaultValue)
{
    if (comboBox == null)
        return;

    string target =
        string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : value;

    for (int i = 0;
         i < comboBox.Items.Count;
         i++)
    {
        if (comboBox.Items[i] is ComboBoxItem item)
        {
            string itemValue =
                item.Content?.ToString() ?? "";

            if (string.Equals(
                itemValue,
                target,
                StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }
    }

    if (comboBox.Items.Count > 0)
    {
        comboBox.SelectedIndex = 0;
    }
}
        // =========================================================
        // SAVE CONFIGURATION
        // =========================================================

        private bool SaveConfiguration()
{
    try
    {
        ServerConfiguration config =
            GetConfigurationFromUI();

        if (string.IsNullOrWhiteSpace(
            config.ServerPath))
        {
            return false;
        }

        string configPath =
            GetConfigPath();

        string? directory =
            Path.GetDirectoryName(configPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json =
            JsonSerializer.Serialize(
                config,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        File.WriteAllText(
            configPath,
            json);

        // =====================================================
        // SAVE PASSWORDS TO GAMEUSERSETTINGS.INI
        // =====================================================

        SavePasswordsToGameUserSettingsIni(
            config.ServerPassword,
            config.AdminPassword);

        SaveWhitelistFile(config);

        return true;
    }
    catch (Exception ex)
    {
        AppendConsole("");

        AppendConsole(
            "CONFIG SAVE ERROR:");

        AppendConsole(
            ex.ToString());

        return false;
    }
}


        // =========================================================
        // LOAD CONFIGURATION
        // =========================================================

       private bool LoadConfiguration()
{
    try
    {
        string path = GetConfigPath();

        if (!File.Exists(path))
            return false;

        string json = File.ReadAllText(path);

        if (string.IsNullOrWhiteSpace(json))
            return false;

        ServerConfiguration? config =
            JsonSerializer.Deserialize<ServerConfiguration>(json);

        if (config == null)
            return false;

        // =====================================================
        // LOAD PASSWORDS FROM GAMEUSERSETTINGS.INI FIRST
        // =====================================================

        LoadPasswordsFromGameUserSettingsIni(config);

        // =====================================================
        // APPLY EVERYTHING TO UI
        // =====================================================

        ApplyConfiguration(config);

        AppendConsole(
            "Saved server configuration loaded.");

        AppendConsole(
            "Whitelist: " +
            (config.WhitelistEnabled
                ? "ENABLED"
                : "DISABLED"));

        if (config.WhitelistEnabled)
        {
            AppendConsole(
                "Whitelist entries: " +
                GetWhitelistIds().Count);
        }

        return true;
    }
    catch (JsonException ex)
    {
        AppendConsole(
            "CONFIGURATION JSON ERROR:");

        AppendConsole(ex.Message);

        return false;
    }
    catch (Exception ex)
    {
        AppendConsole(
            "CONFIG LOAD ERROR:");

        AppendConsole(ex.ToString());

        return false;
    }
}

        // =========================================================
        // SERVER PATH
        // =========================================================

        private void ServerPathTextBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            if (_loadingConfiguration)
                return;

            UpdateServerStatus();
            UpdateLaunchOptions();
        }

        // =========================================================
        // BROWSE SERVER FOLDER
        // =========================================================

        private void BrowseServerFolderButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            using var dialog =
                new System.Windows.Forms.FolderBrowserDialog();

            dialog.Description =
                "Select the ASA server installation folder";

            dialog.ShowNewFolderButton = true;

            if (dialog.ShowDialog() ==
                System.Windows.Forms.DialogResult.OK)
            {
                if (ServerPathTextBox != null)
                    ServerPathTextBox.Text =
                        dialog.SelectedPath;

                AppendConsole("");
                AppendConsole(
                    "Server folder selected:");

                AppendConsole(
                    dialog.SelectedPath);

                UpdateServerStatus();
                UpdateLaunchOptions();
            }
        }

        // =========================================================
        // PASSWORD SETTINGS
        // =========================================================

        private void PasswordToggleCheckBox_Changed(
    object sender,
    RoutedEventArgs e)
{
    if (_loadingConfiguration)
        return;

    UpdatePasswordControls();
    UpdateLaunchOptions();
}

        private void UpdatePasswordControls()
{
    // =====================================================
    // SERVER PASSWORD
    // =====================================================

    if (ServerPasswordBox != null)
    {
        ServerPasswordBox.IsEnabled = true;

        ServerPasswordBox.Visibility =
            ServerPasswordToggleCheckBox?.IsChecked == true
                ? Visibility.Collapsed
                : Visibility.Visible;
    }

    if (ServerPasswordTextBox != null)
    {
        ServerPasswordTextBox.IsEnabled = true;

        ServerPasswordTextBox.Visibility =
            ServerPasswordToggleCheckBox?.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    // =====================================================
    // ADMIN PASSWORD
    // =====================================================

    if (AdminPasswordBox != null)
    {
        AdminPasswordBox.IsEnabled = true;

        AdminPasswordBox.Visibility =
            AdminPasswordToggleCheckBox?.IsChecked == true
                ? Visibility.Collapsed
                : Visibility.Visible;
    }

    if (AdminPasswordTextBox != null)
    {
        AdminPasswordTextBox.IsEnabled = true;

        AdminPasswordTextBox.Visibility =
            AdminPasswordToggleCheckBox?.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
    }
}

        private void ServerPasswordBox_PasswordChanged(
            object sender,
            RoutedEventArgs e)
        {
            if (_loadingConfiguration)
                return;

            UpdateLaunchOptions();
        }

        private void AdminPasswordBox_PasswordChanged(
            object sender,
            RoutedEventArgs e)
        {
            if (_loadingConfiguration)
                return;

            UpdateLaunchOptions();
        }

        // =========================================================
        // OPTIONAL PASSWORD BUTTONS
        // =========================================================

        private void ServerPasswordToggleButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (ServerPasswordToggleCheckBox == null)
                return;

            ServerPasswordToggleCheckBox.IsChecked =
                !ServerPasswordToggleCheckBox.IsChecked;

            UpdatePasswordControls();
            UpdateLaunchOptions();

            if (sender is Button button)
            {
                button.Content =
                    ServerPasswordToggleCheckBox.IsChecked == true
                        ? "Hide"
                        : "Show";
            }
        }

        private void AdminPasswordToggleButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (AdminPasswordToggleCheckBox == null)
                return;

            AdminPasswordToggleCheckBox.IsChecked =
                !AdminPasswordToggleCheckBox.IsChecked;

            UpdatePasswordControls();
            UpdateLaunchOptions();

            if (sender is Button button)
            {
                button.Content =
                    AdminPasswordToggleCheckBox.IsChecked == true
                        ? "Hide"
                        : "Show";
            }
        }

        // =========================================================
        // WHITELIST
        // =========================================================

        private void WhitelistEnabledCheckBox_Changed(
    object sender,
    RoutedEventArgs e)
{
    if (_loadingConfiguration)
        return;

    UpdateWhitelistControls();
    UpdateWhitelistSummary();
    UpdateLaunchOptions();

    string serverPath =
        ServerPathTextBox?.Text?.Trim() ?? "";

    if (!string.IsNullOrWhiteSpace(serverPath))
    {
        SaveConfiguration();
    }
}

        private void WhitelistSettings_Changed(
            object sender,
            RoutedEventArgs e)
        {
            if (_loadingConfiguration)
                return;

            UpdateWhitelistControls();
            UpdateWhitelistSummary();
            UpdateLaunchOptions();
        }

        private void UpdateWhitelistControls()
        {
            bool enabled =
                WhitelistEnabledCheckBox?.IsChecked == true;

            if (WhitelistTextBox != null)
                WhitelistTextBox.IsEnabled =
                    enabled;
        }

        private List<string> GetWhitelistIds()
        {
            var ids = new List<string>();

            if (WhitelistTextBox == null)
                return ids;

            string text =
                WhitelistTextBox.Text ?? "";

            string[] lines =
                text.Split(
                    new[]
                    {
                        '\r',
                        '\n',
                        ',',
                        ';'
                    },
                    StringSplitOptions.RemoveEmptyEntries);

            foreach (string raw in lines)
            {
                string id =
                    raw.Trim();

                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (!ids.Contains(
                    id,
                    StringComparer.OrdinalIgnoreCase))
                {
                    ids.Add(id);
                }
            }

            return ids;
        }

        private void UpdateWhitelistSummary()
        {
            if (SummaryWhitelistText == null)
                return;

            bool enabled =
                WhitelistEnabledCheckBox?.IsChecked == true;

            if (!enabled)
            {
                SummaryWhitelistText.Text =
                    "Disabled";

                SummaryWhitelistText.Foreground =
                    Brushes.White;

                return;
            }

            int count =
                GetWhitelistIds().Count;

            SummaryWhitelistText.Text =
                $"Enabled: {count}";

            SummaryWhitelistText.Foreground =
                Brushes.LightGreen;
        }


private void SaveWhitelistFile(
    ServerConfiguration config)
{
    if (config == null)
        return;

    if (string.IsNullOrWhiteSpace(
        config.ServerPath))
        return;

    try
    {
        string directory =
            Path.Combine(
                config.ServerPath,
                "ShooterGame",
                "Binaries",
                "Win64");

        Directory.CreateDirectory(
            directory);

        string path =
            Path.Combine(
                directory,
                "PlayersExclusiveJoinList.txt");

        // =====================================================
        // NORMALIZE WHITELIST IDS
        //
        // Supports:
        //
        // 76561198048881688
        // 76561198290356555
        //
        // OR:
        //
        // 76561198048881688 76561198290356555
        //
        // OR:
        //
        // 76561198048881688,76561198290356555
        //
        // OR any combination of whitespace, commas,
        // semicolons, and newlines.
        // =====================================================

        string whitelistText =
            config.WhitelistIds ?? "";

        List<string> ids =
            whitelistText
                .Split(
                    new[]
                    {
                        ' ',
                        '\t',
                        '\r',
                        '\n',
                        ',',
                        ';'
                    },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        // =====================================================
        // WRITE EXACTLY ONE ID PER LINE
        // =====================================================

        File.WriteAllLines(
            path,
            ids);

        AppendConsole("");
        AppendConsole(
            "Whitelist file updated:");

        AppendConsole(path);

        AppendConsole(
            "Whitelist enabled: " +
            config.WhitelistEnabled);

        AppendConsole(
            "Whitelist entries: " +
            ids.Count);

        if (!config.WhitelistEnabled)
        {
            AppendConsole(
                "Whitelist mode is DISABLED.");
        }
        else
        {
            AppendConsole(
                "Whitelist mode is ENABLED.");
        }
    }
    catch (Exception ex)
    {
        AppendConsole(
            "WHITELIST FILE ERROR:");

        AppendConsole(
            ex.ToString());
    }
}
       private void WhitelistTextBox_TextChanged(
    object sender,
    TextChangedEventArgs e)
{
    if (_loadingConfiguration)
        return;

    UpdateWhitelistSummary();
    UpdateLaunchOptions();

    string serverPath =
        ServerPathTextBox?.Text?.Trim() ?? "";

    if (!string.IsNullOrWhiteSpace(serverPath))
    {
        SaveConfiguration();
    }
}
        private void ManageWhitelistButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            string serverDirectory =
                ServerPathTextBox?.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(serverDirectory))
            {
                MessageBox.Show(
                    "Please select the ASA server folder first.",
                    "Whitelist",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            try
            {
                string directory =
                    GetWhitelistDirectory();

                Directory.CreateDirectory(
                    directory);

                string path =
                    GetWhitelistPath();

                if (!File.Exists(path))
                    File.WriteAllText(path, "");

                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = "notepad.exe",
                        Arguments =
                            "\"" + path + "\"",
                        UseShellExecute = true
                    });

                AppendConsole("");
                AppendConsole(
                    "Opened ASA whitelist:");

                AppendConsole(path);
            }
            catch (Exception ex)
            {
                AppendConsole(
                    "WHITELIST ERROR:");

                AppendConsole(
                    ex.ToString());

                MessageBox.Show(
                    ex.Message,
                    "Whitelist Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // =========================================================
        // SAVE BUTTON
        // =========================================================

        private void SaveConfigButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                if (!SaveConfiguration())
                {
                    MessageBox.Show(
                        "Please select a server folder first.",
                        "Configuration",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                AppendConsole("");
                AppendConsole(
                    "========================================");
                AppendConsole(
                    "SERVER CONFIGURATION SAVED");
                AppendConsole(
                    "========================================");

                MessageBox.Show(
                    "Server configuration saved successfully.",
                    "Configuration Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Configuration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // =========================================================
        // LOAD BUTTON
        // =========================================================

        private void LoadConfigButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!LoadConfiguration())
            {
                MessageBox.Show(
                    "No valid saved configuration was found.",
                    "Configuration",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            MessageBox.Show(
                "Server configuration loaded successfully.",
                "Configuration Loaded",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // =========================================================
        // MOD MANAGEMENT
        // =========================================================

        private List<string> GetModIds()
        {
            var mods = new List<string>();

            if (ModsTextBox == null)
                return mods;

            string text =
                ModsTextBox.Text ?? "";

            string[] lines =
                text.Split(
                    new[]
                    {
                        '\r',
                        '\n'
                    },
                    StringSplitOptions.RemoveEmptyEntries);

            foreach (string rawLine in lines)
            {
                string line =
                    rawLine.Trim();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] commaSeparated =
                    line.Split(
                        ',',
                        StringSplitOptions.RemoveEmptyEntries);

                foreach (string part in commaSeparated)
                {
                    string mod =
                        part.Trim();

                    if (string.IsNullOrWhiteSpace(mod))
                        continue;

                    if (!mods.Contains(
                        mod,
                        StringComparer.OrdinalIgnoreCase))
                    {
                        mods.Add(mod);
                    }
                }
            }

            return mods;
        }

        private string BuildModsArgument()
        {
            List<string> mods =
                GetModIds();

            if (mods.Count == 0)
                return "";

            return "-mods=" +
                   string.Join(",", mods);
        }

        private void ModsTextBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            if (_loadingConfiguration)
                return;

            UpdateLaunchOptions();
        }

        // =========================================================
        // LAUNCH OPTION EVENTS
        // =========================================================

        private void LaunchOptionsTextBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            if (_loadingConfiguration)
                return;

            UpdateLaunchOptions();
        }

        private void LaunchOptionsComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_loadingConfiguration)
                return;

            UpdateLaunchOptions();
        }

        private void LaunchOptionsPasswordBox_PasswordChanged(
            object sender,
            RoutedEventArgs e)
        {
            if (_loadingConfiguration)
                return;

            UpdateLaunchOptions();
        }

        private void LaunchOptionsCheckBox_Changed(
            object sender,
            RoutedEventArgs e)
        {
            if (_loadingConfiguration)
                return;

            UpdateLaunchOptions();
        }

        private void ClusterSettings_Changed(
            object sender,
            RoutedEventArgs e)
        {
            if (_loadingConfiguration)
                return;

            UpdateLaunchOptions();
        }

        // =========================================================
        // BUILD SERVER ARGUMENTS
        // =========================================================

        private string BuildServerArguments()
        {
            ServerConfiguration config =
                GetConfigurationFromUI();

            string map =
                string.IsNullOrWhiteSpace(config.Map)
                    ? "TheIsland_WP"
                    : config.Map;

            string serverPort =
                string.IsNullOrWhiteSpace(config.ServerPort)
                    ? "7777"
                    : config.ServerPort;

            string queryPort =
                string.IsNullOrWhiteSpace(config.QueryPort)
                    ? "27015"
                    : config.QueryPort;

            string maxPlayers =
                string.IsNullOrWhiteSpace(config.MaxPlayers)
                    ? "70"
                    : config.MaxPlayers;

            string difficulty =
                string.IsNullOrWhiteSpace(config.Difficulty)
                    ? "1.0"
                    : config.Difficulty;

            string arguments =
                map +
                "?listen" +
                "?Port=" +
                serverPort +
                "?QueryPort=" +
                queryPort +
                "?MaxPlayers=" +
                maxPlayers +
                "?DifficultyOffset=" +
                difficulty;

            // =====================================================
            // SERVER NAME
            // =====================================================

            if (!string.IsNullOrWhiteSpace(
                config.ServerName))
            {
                arguments +=
                    "?SessionName=" +
                    Uri.EscapeDataString(
                        config.ServerName);
            }

            // =====================================================
            // SERVER PASSWORD
            // =====================================================

            if (config.ServerPasswordEnabled &&
                !string.IsNullOrWhiteSpace(
                    config.ServerPassword))
            {
                arguments +=
                    "?ServerPassword=" +
                    Uri.EscapeDataString(
                        config.ServerPassword);
            }

            // =====================================================
            // ADMIN PASSWORD
            // =====================================================

            if (config.AdminPasswordEnabled &&
                !string.IsNullOrWhiteSpace(
                    config.AdminPassword))
            {
                arguments +=
                    "?ServerAdminPassword=" +
                    Uri.EscapeDataString(
                        config.AdminPassword);
            }

            // =====================================================
            // MODS
            // =====================================================

            string mods =
                BuildModsArgument();

            if (!string.IsNullOrWhiteSpace(mods))
            {
                arguments +=
                    " " + mods;
            }

            // =====================================================
            // PVE
            // =====================================================

            if (config.PvE)
                arguments += " -pve";

            // =====================================================
            // CROSSPLAY
            // =====================================================

            if (config.Crossplay)
                arguments += " -crossplay";

            // =====================================================
            // CLUSTER
            // =====================================================

            if (config.ClusterEnabled)
            {
                string clusterId =
                    string.IsNullOrWhiteSpace(
                        config.ClusterId)
                        ? "ASACluster"
                        : config.ClusterId;

                arguments +=
                    " -clusterid=" +
                    clusterId;

                if (!string.IsNullOrWhiteSpace(
                    config.ClusterDirectory))
                {
                    arguments +=
                        " -ClusterDirOverride=\"" +
                        config.ClusterDirectory +
                        "\"";
                }
            }

            // =====================================================
            // WHITELIST
            // =====================================================
            

        

           if (config.WhitelistEnabled)
{
    SaveWhitelistFile(config);

    arguments +=
        " -exclusivejoin";
}

            return arguments.Trim();
        }

        // =========================================================
        // LIVE LAUNCH OPTIONS
        // =========================================================

        private void UpdateLaunchOptions()
        {
            if (LaunchOptionsTextBox == null)
                return;

            try
            {
                string arguments =
                    BuildServerArguments();

                LaunchOptionsTextBox.Text =
                    arguments;

                if (SummaryMapText != null)
                {
                    SummaryMapText.Text =
                        GetComboBoxValue(
                            MapComboBox,
                            "TheIsland_WP");
                }

                if (SummaryPlayersText != null)
                {
                    SummaryPlayersText.Text =
                        MaxPlayersTextBox?.Text?.Trim()
                        ?? "70";
                }

                if (SummaryModsText != null)
                {
                    SummaryModsText.Text =
                        GetModIds().Count.ToString();
                }

                if (SummaryClusterText != null)
                {
                    bool enabled =
                        ClusterEnabledCheckBox?.IsChecked == true;

                    SummaryClusterText.Text =
                        enabled
                            ? (
                                string.IsNullOrWhiteSpace(
                                    ClusterIdTextBox?.Text)
                                    ? "Enabled"
                                    : "Enabled: " +
                                      ClusterIdTextBox.Text.Trim()
                              )
                            : "Disabled";

                    SummaryClusterText.Foreground =
                        enabled
                            ? Brushes.LightGreen
                            : Brushes.White;
                }

                UpdateWhitelistSummary();
            }
            catch (Exception ex)
            {
                AppendConsole(
                    "LAUNCH OPTIONS PREVIEW ERROR:");

                AppendConsole(
                    ex.Message);
            }
        }

private void SetWhitelistMode(string mode)
{
    foreach (ComboBoxItem item in WhitelistModeComboBox.Items)
    {
        if (string.Equals(
                item.Content?.ToString(),
                mode,
                StringComparison.OrdinalIgnoreCase))
        {
            WhitelistModeComboBox.SelectedItem = item;
            return;
        }
    }

    // Default to Steam IDs
    WhitelistModeComboBox.SelectedIndex = 1;
}

        // =========================================================
        // STEAMCMD
        // =========================================================

        private async Task<int> RunSteamCmdAsync(
    string arguments,
    string workingDirectory)
{
    if (!File.Exists(_steamCmdPath))
    {
        throw new FileNotFoundException(
            "SteamCMD executable was not found.",
            _steamCmdPath);
    }

    if (!Directory.Exists(workingDirectory))
    {
        Directory.CreateDirectory(
            workingDirectory);
    }

    AppendConsole(
        "SteamCMD command:");

    AppendConsole(
        _steamCmdPath +
        " " +
        arguments);

    AppendConsole("");

    AppendConsole(
        "Starting SteamCMD with Windows ConPTY...");

    AppendConsole("");

    IntPtr inputRead = IntPtr.Zero;
    IntPtr inputWrite = IntPtr.Zero;

    IntPtr outputRead = IntPtr.Zero;
    IntPtr outputWrite = IntPtr.Zero;

    IntPtr pseudoConsole = IntPtr.Zero;

    IntPtr attributeList = IntPtr.Zero;

    IntPtr processHandle = IntPtr.Zero;
    IntPtr threadHandle = IntPtr.Zero;

    Task? outputTask = null;

    try
    {
        // =====================================================
        // CREATE INPUT PIPE
        // =====================================================

        if (!CreatePipe(
            out inputRead,
            out inputWrite,
            IntPtr.Zero,
            0))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Failed to create ConPTY input pipe.");
        }

        // Parent owns the write side.
        // Child/ConPTY owns the read side.
        SetHandleInformation(
            inputWrite,
            HANDLE_FLAG_INHERIT,
            0);

        // =====================================================
        // CREATE OUTPUT PIPE
        // =====================================================

        if (!CreatePipe(
            out outputRead,
            out outputWrite,
            IntPtr.Zero,
            0))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Failed to create ConPTY output pipe.");
        }

        // Parent owns the read side.
        // ConPTY owns the write side.
        SetHandleInformation(
            outputRead,
            HANDLE_FLAG_INHERIT,
            0);

        // =====================================================
        // CREATE PSEUDO CONSOLE
        // =====================================================

        COORD consoleSize =
            new COORD
            {
                X = 120,
                Y = 30
            };

        int result =
            CreatePseudoConsole(
                consoleSize,
                inputRead,
                outputWrite,
                0,
                out pseudoConsole);

        if (result != S_OK)
        {
            throw new Win32Exception(
                result,
                "Windows could not create a ConPTY pseudo console.");
        }

        // The pseudo console now owns these endpoints.
        CloseHandle(inputRead);
        inputRead = IntPtr.Zero;

        CloseHandle(outputWrite);
        outputWrite = IntPtr.Zero;

        // =====================================================
        // CREATE ATTRIBUTE LIST
        // =====================================================

        IntPtr attributeListSize =
            IntPtr.Zero;

        InitializeProcThreadAttributeList(
            IntPtr.Zero,
            1,
            0,
            ref attributeListSize);

        attributeList =
            Marshal.AllocHGlobal(
                attributeListSize);

        if (!InitializeProcThreadAttributeList(
            attributeList,
            1,
            0,
            ref attributeListSize))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Failed to initialize process attribute list.");
        }

        // =====================================================
        // ATTACH CONPTY
        // =====================================================

        IntPtr pseudoConsoleValue =
            pseudoConsole;

        if (!UpdateProcThreadAttribute(
            attributeList,
            0,
            PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
            pseudoConsoleValue,
            new IntPtr(IntPtr.Size),
            IntPtr.Zero,
            IntPtr.Zero))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Failed to attach ConPTY to SteamCMD.");
        }

        // =====================================================
        // STARTUP INFO
        // =====================================================

        STARTUPINFOEX startupInfo =
            new STARTUPINFOEX();

        startupInfo.StartupInfo.cb =
            Marshal.SizeOf<STARTUPINFOEX>();

        startupInfo.lpAttributeList =
            attributeList;

        // =====================================================
        // COMMAND LINE
        // =====================================================

        string commandLine =
            "\"" +
            _steamCmdPath +
            "\" " +
            arguments;

        StringBuilder commandLineBuilder =
            new StringBuilder(
                commandLine);

        // =====================================================
        // START STEAMCMD
        // =====================================================

        

        bool processCreated =
            CreateProcess(
                null,
                commandLineBuilder,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                EXTENDED_STARTUPINFO_PRESENT |
                CREATE_UNICODE_ENVIRONMENT,
                IntPtr.Zero,
                workingDirectory,
                ref startupInfo,
                out PROCESS_INFORMATION processInformation);

        if (!processCreated)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "SteamCMD process could not be started.");
        }

        processHandle =
            processInformation.hProcess;

        threadHandle =
            processInformation.hThread;

        AppendConsole(
            "SteamCMD PID: " +
            processInformation.dwProcessId);

        AppendConsole("");

        // =====================================================
        // READ CONPTY OUTPUT
        // =====================================================

        outputTask =
            ReadConPtyOutputAsync(
                outputRead);

        // IMPORTANT:
        //
        // Ownership of outputRead is now transferred to
        // ReadConPtyOutputAsync / SafeFileHandle.
        //
        // Do NOT close outputRead from this method anymore.
        outputRead = IntPtr.Zero;

        // =====================================================
        // WAIT FOR STEAMCMD PROCESS
        // =====================================================

        await Task.Run(() =>
        {
            WaitForSingleObject(
                processHandle,
                INFINITE);
        });

        // =====================================================
        // GET EXIT CODE
        // =====================================================

        uint exitCode = 0;

        if (!GetExitCodeProcess(
            processHandle,
            out exitCode))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Could not retrieve SteamCMD exit code.");
        }

        AppendConsole("");

        AppendConsole(
            "SteamCMD process exited.");

        AppendConsole(
            "SteamCMD exit code: " +
            exitCode);

        // =====================================================
        // VERY IMPORTANT
        //
        // SteamCMD is finished, so close the ConPTY NOW.
        //
        // This causes the ConPTY output pipe to receive EOF,
        // allowing ReadConPtyOutputAsync() to finish.
        // =====================================================

        if (pseudoConsole != IntPtr.Zero)
        {
            ClosePseudoConsole(
                pseudoConsole);

            pseudoConsole =
                IntPtr.Zero;
        }

        // =====================================================
        // WAIT FOR OUTPUT READER TO FINISH
        // =====================================================

        if (outputTask != null)
        {
            try
            {
                await outputTask;
            }
            catch (ObjectDisposedException)
            {
            }
            catch (IOException)
            {
            }
        }

        return unchecked((int)exitCode);
    }
    finally
    {
        // =====================================================
        // CLOSE PROCESS HANDLES
        // =====================================================

        if (threadHandle != IntPtr.Zero)
        {
            CloseHandle(
                threadHandle);

            threadHandle =
                IntPtr.Zero;
        }

        if (processHandle != IntPtr.Zero)
        {
            CloseHandle(
                processHandle);

            processHandle =
                IntPtr.Zero;
        }

        // =====================================================
        // CLOSE ATTRIBUTE LIST
        // =====================================================

        if (attributeList != IntPtr.Zero)
        {
            try
            {
                DeleteProcThreadAttributeList(
                    attributeList);
            }
            catch
            {
            }

            Marshal.FreeHGlobal(
                attributeList);

            attributeList =
                IntPtr.Zero;
        }

        // =====================================================
        // CLOSE REMAINING INPUT HANDLES
        // =====================================================

        if (inputRead != IntPtr.Zero)
        {
            CloseHandle(
                inputRead);

            inputRead =
                IntPtr.Zero;
        }

        if (inputWrite != IntPtr.Zero)
        {
            CloseHandle(
                inputWrite);

            inputWrite =
                IntPtr.Zero;
        }

        // =====================================================
        // CLOSE OUTPUT WRITE HANDLE
        // =====================================================

        if (outputWrite != IntPtr.Zero)
        {
            CloseHandle(
                outputWrite);

            outputWrite =
                IntPtr.Zero;
        }

        // =====================================================
        // OUTPUT READ HANDLE
        //
        // If outputRead was transferred to SafeFileHandle,
        // it is already owned by ReadConPtyOutputAsync.
        // =====================================================

        if (outputRead != IntPtr.Zero)
        {
            CloseHandle(
                outputRead);

            outputRead =
                IntPtr.Zero;
        }

        // =====================================================
        // SAFETY NET
        //
        // Normally the pseudo console was already closed
        // immediately after SteamCMD exited.
        // =====================================================

        if (pseudoConsole != IntPtr.Zero)
        {
            ClosePseudoConsole(
                pseudoConsole);

            pseudoConsole =
                IntPtr.Zero;
        }
    }
}

// =========================================================
// READ CONPTY OUTPUT
// =========================================================

private async Task ReadConPtyOutputAsync(
    IntPtr outputHandle)
{
    if (outputHandle == IntPtr.Zero)
        return;

    try
    {
        using SafeFileHandle safeHandle =
            new SafeFileHandle(
                outputHandle,
                ownsHandle: true);

        outputHandle =
            IntPtr.Zero;

        using FileStream stream =
            new FileStream(
                safeHandle,
                FileAccess.Read,
                4096,
                isAsync: false);

        byte[] buffer =
            new byte[4096];

        StringBuilder currentLine =
            new StringBuilder();

        while (true)
        {
            int bytesRead =
                await stream.ReadAsync(
                    buffer,
                    0,
                    buffer.Length);

            if (bytesRead <= 0)
                break;

            string text =
                Encoding.UTF8.GetString(
                    buffer,
                    0,
                    bytesRead);

            for (int i = 0;
                 i < text.Length;
                 i++)
            {
                char character =
                    text[i];

                // =================================================
                // NEW LINE
                // =================================================

                if (character == '\n')
                {
                    FlushSteamCmdLine(
                        currentLine);

                    continue;
                }

                // =================================================
                // CARRIAGE RETURN
                //
                // SteamCMD uses this to update the same line.
                // We treat it as a live progress update.
                // =================================================

                if (character == '\r')
                {
                    FlushSteamCmdLine(
                        currentLine);

                    continue;
                }

                currentLine.Append(
                    character);
            }

            // =====================================================
            // FLUSH PARTIAL OUTPUT
            //
            // This is important for SteamCMD progress which can
            // arrive without a newline.
            // =====================================================

            if (currentLine.Length > 0)
            {
                string liveText =
                    CleanSteamCmdOutput(
                        currentLine.ToString());

                currentLine.Clear();

                if (!string.IsNullOrWhiteSpace(
                    liveText))
                {
                    AppendConsoleLive(
                        "[SteamCMD] " +
                        liveText);
                }
            }

            // Allow WPF to process UI messages.
            await Task.Yield();
        }

        FlushSteamCmdLine(
            currentLine);
    }
    catch (ObjectDisposedException)
    {
    }
    catch (IOException)
    {
    }
    catch (Exception ex)
    {
        AppendConsole(
            "[SteamCMD OUTPUT ERROR] " +
            ex.Message);
    }
}


// =========================================================
// FLUSH STEAMCMD LINE
// =========================================================

private void FlushSteamCmdLine(
    StringBuilder line)
{
    if (line == null ||
        line.Length == 0)
    {
        return;
    }

    string text =
        CleanSteamCmdOutput(
            line.ToString());

    line.Clear();

    if (string.IsNullOrWhiteSpace(text))
        return;

    AppendConsole(
        "[SteamCMD] " +
        text);
}


// =========================================================
// CLEAN STEAMCMD TERMINAL OUTPUT
// =========================================================

private string CleanSteamCmdOutput(
    string text)
{
    if (string.IsNullOrEmpty(text))
        return "";

    StringBuilder result =
        new StringBuilder();

    bool insideAnsiEscape =
        false;

    for (int i = 0;
         i < text.Length;
         i++)
    {
        char c =
            text[i];

        // ANSI escape sequence begins.
        if (c == '\x1B')
        {
            insideAnsiEscape = true;
            continue;
        }

        if (insideAnsiEscape)
        {
            // Most ANSI sequences terminate with a letter.
            if ((c >= '@' && c <= '~'))
            {
                insideAnsiEscape = false;
            }

            continue;
        }

        // Ignore other control characters.
        if (char.IsControl(c) &&
            c != '\t')
        {
            continue;
        }

        result.Append(c);
    }

    return result.ToString();
}


// =========================================================
// LIVE CONSOLE APPEND
// =========================================================

private async void StartClusterButton_Click(
    object sender,
    RoutedEventArgs e)
{
    if (!ClusterEnabledCheckBox.IsChecked.GetValueOrDefault())
    {
        MessageBox.Show(
            "Cluster is not enabled for this server.",
            "Cluster",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return;
    }

    string clusterId =
        ClusterIdTextBox?.Text?.Trim() ?? "";

    if (string.IsNullOrWhiteSpace(clusterId))
    {
        MessageBox.Show(
            "Please enter a Cluster ID first.",
            "Cluster",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return;
    }

    List<ServerPage> clusterServers =
        OpenServerPages
            .Where(page =>
                page != null &&
                page.ClusterEnabledCheckBox != null &&
                page.ClusterEnabledCheckBox.IsChecked == true &&
                !string.IsNullOrWhiteSpace(
                    page.ClusterIdTextBox?.Text) &&
                string.Equals(
                    page.ClusterIdTextBox.Text.Trim(),
                    clusterId,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

    AppendConsole("");
    AppendConsole("========================================");
    AppendConsole("CLUSTER START REQUEST");
    AppendConsole("========================================");

    AppendConsole(
        "Registered Server Pages: " +
        OpenServerPages.Count);

    AppendConsole(
        "Matching Cluster Servers: " +
        clusterServers.Count);

    foreach (ServerPage page in OpenServerPages)
    {
        AppendConsole(
            "SERVER: " +
            page.ServerDisplayName +
            " | Enabled: " +
            page.ClusterEnabledCheckBox.IsChecked +
            " | Cluster ID: [" +
            page.ClusterIdTextBox.Text.Trim() +
            "]");
    }

    AppendConsole("");

    if (clusterServers.Count == 0)
    {
        MessageBox.Show(
            "No enabled servers were found for cluster '" +
            clusterId +
            "'.",
            "Cluster",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return;
    }

    StartClusterButton.IsEnabled = false;
    StopClusterButton.IsEnabled = false;

    try
    {
        AppendConsole(
            "Starting " +
            clusterServers.Count +
            " server(s)...");

        AppendConsole("");

        List<Task> startTasks =
            new List<Task>();

        foreach (ServerPage server in clusterServers)
        {
            AppendConsole(
                "Starting: " +
                server.ServerDisplayName);

            startTasks.Add(
                server.StartServerForClusterAsync());
        }

        await Task.WhenAll(startTasks);

        AppendConsole("");
        AppendConsole("========================================");
        AppendConsole("CLUSTER START OPERATION COMPLETED");
        AppendConsole("========================================");
        AppendConsole("");

        StopClusterButton.IsEnabled = true;
    }
    catch (Exception ex)
    {
        AppendConsole("");
        AppendConsole(
            "CLUSTER START ERROR:");

        AppendConsole(
            ex.ToString());

        MessageBox.Show(
            ex.Message,
            "Cluster Start Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
    finally
    {
        StartClusterButton.IsEnabled = true;
    }
}

private async void StopClusterButton_Click(
    object sender,
    RoutedEventArgs e)
{
    string clusterId =
        ClusterIdTextBox?.Text?.Trim() ?? "";

    if (string.IsNullOrWhiteSpace(clusterId))
    {
        MessageBox.Show(
            "Please enter a Cluster ID first.",
            "Cluster",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return;
    }

    List<ServerPage> clusterServers =
        OpenServerPages
            .Where(page =>
                page != null &&
                page.ClusterEnabledCheckBox != null &&
                page.ClusterEnabledCheckBox.IsChecked == true &&
                !string.IsNullOrWhiteSpace(
                    page.ClusterIdTextBox?.Text) &&
                string.Equals(
                    page.ClusterIdTextBox.Text.Trim(),
                    clusterId,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

    if (clusterServers.Count == 0)
    {
        MessageBox.Show(
            "No enabled servers were found for this cluster.",
            "Cluster",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return;
    }

    AppendConsole("");
    AppendConsole("========================================");
    AppendConsole("STOPPING CLUSTER");
    AppendConsole("========================================");
    AppendConsole("");
    AppendConsole(
        "Cluster ID: " + clusterId);
    AppendConsole(
        "Servers found: " + clusterServers.Count);
    AppendConsole("");

    StopClusterButton.IsEnabled = false;
    StartClusterButton.IsEnabled = false;

    try
    {
        List<Task> stopTasks =
            new List<Task>();

        foreach (ServerPage server in clusterServers)
        {
            stopTasks.Add(
                server.StopServerForClusterAsync());
        }

        await Task.WhenAll(stopTasks);

        AppendConsole("");
        AppendConsole(
            "========================================");
        AppendConsole(
            "CLUSTER STOPPED");
        AppendConsole(
            "========================================");
        AppendConsole("");
    }
    catch (Exception ex)
    {
        AppendConsole("");
        AppendConsole(
            "CLUSTER STOP ERROR:");

        AppendConsole(
            ex.ToString());

        MessageBox.Show(
            ex.Message,
            "Cluster Stop Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
    finally
    {
        StartClusterButton.IsEnabled = true;
        StopClusterButton.IsEnabled = false;
    }
}

// =========================================================
// CONPTY NATIVE DEFINITIONS
// =========================================================

private const int S_OK = 0;

private const uint INFINITE =
    0xFFFFFFFF;

private const uint CREATE_UNICODE_ENVIRONMENT =
    0x00000400;

private const uint EXTENDED_STARTUPINFO_PRESENT =
    0x00080000;

private const uint HANDLE_FLAG_INHERIT =
    0x00000001;

private const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE =
    0x00020016;


// =========================================================
// COORD
// =========================================================

[StructLayout(
    LayoutKind.Sequential)]
private struct COORD
{
    public short X;
    public short Y;
}


// =========================================================
// STARTUPINFO
// =========================================================

[StructLayout(
    LayoutKind.Sequential,
    CharSet = CharSet.Unicode)]
private struct STARTUPINFO
{
    public int cb;

    public string? lpReserved;

    public string? lpDesktop;

    public string? lpTitle;

    public int dwX;

    public int dwY;

    public int dwXSize;

    public int dwYSize;

    public int dwXCountChars;

    public int dwYCountChars;

    public int dwFillAttribute;

    public int dwFlags;

    public short wShowWindow;

    public short cbReserved2;

    public IntPtr lpReserved2;

    public IntPtr hStdInput;

    public IntPtr hStdOutput;

    public IntPtr hStdError;
}


// =========================================================
// STARTUPINFOEX
// =========================================================

[StructLayout(
    LayoutKind.Sequential)]
private struct STARTUPINFOEX
{
    public STARTUPINFO StartupInfo;

    public IntPtr lpAttributeList;
}


// =========================================================
// PROCESS INFORMATION
// =========================================================

[StructLayout(
    LayoutKind.Sequential)]
private struct PROCESS_INFORMATION
{
    public IntPtr hProcess;

    public IntPtr hThread;

    public uint dwProcessId;

    public uint dwThreadId;
}


// =========================================================
// CREATE PIPE
// =========================================================

[DllImport(
    "kernel32.dll",
    SetLastError = true)]
private static extern bool CreatePipe(
    out IntPtr hReadPipe,
    out IntPtr hWritePipe,
    IntPtr lpPipeAttributes,
    int nSize);


// =========================================================
// SET HANDLE INFORMATION
// =========================================================

[DllImport(
    "kernel32.dll",
    SetLastError = true)]
private static extern bool SetHandleInformation(
    IntPtr hObject,
    uint dwMask,
    uint dwFlags);


// =========================================================
// CREATE PSEUDO CONSOLE
// =========================================================

[DllImport(
    "kernel32.dll",
    SetLastError = true)]
private static extern int CreatePseudoConsole(
    COORD size,
    IntPtr hInput,
    IntPtr hOutput,
    uint dwFlags,
    out IntPtr phPC);


// =========================================================
// CLOSE PSEUDO CONSOLE
// =========================================================

[DllImport(
    "kernel32.dll",
    SetLastError = true)]
private static extern void ClosePseudoConsole(
    IntPtr hPC);


// =========================================================
// ATTRIBUTE LIST - INITIALIZE
// =========================================================

[DllImport(
    "kernel32.dll",
    SetLastError = true)]
private static extern bool InitializeProcThreadAttributeList(
    IntPtr lpAttributeList,
    int dwAttributeCount,
    int dwFlags,
    ref IntPtr lpSize);


// =========================================================
// ATTRIBUTE LIST - UPDATE
// =========================================================

[DllImport(
    "kernel32.dll",
    SetLastError = true)]
private static extern bool UpdateProcThreadAttribute(
    IntPtr lpAttributeList,
    uint dwFlags,
    int attribute,
    IntPtr lpValue,
    IntPtr cbSize,
    IntPtr lpPreviousValue,
    IntPtr lpReturnSize);


// =========================================================
// ATTRIBUTE LIST - DELETE
// =========================================================

[DllImport(
    "kernel32.dll")]
private static extern void DeleteProcThreadAttributeList(
    IntPtr lpAttributeList);


// =========================================================
// CREATE PROCESS
// =========================================================

[DllImport(
    "kernel32.dll",
    SetLastError = true,
    CharSet = CharSet.Unicode)]
private static extern bool CreateProcess(
    string? lpApplicationName,
    StringBuilder lpCommandLine,
    IntPtr lpProcessAttributes,
    IntPtr lpThreadAttributes,
    bool bInheritHandles,
    uint dwCreationFlags,
    IntPtr lpEnvironment,
    string? lpCurrentDirectory,
    ref STARTUPINFOEX lpStartupInfo,
    out PROCESS_INFORMATION lpProcessInformation);


// =========================================================
// WAIT FOR PROCESS
// =========================================================

[DllImport(
    "kernel32.dll",
    SetLastError = true)]
private static extern uint WaitForSingleObject(
    IntPtr hHandle,
    uint dwMilliseconds);


// =========================================================
// GET EXIT CODE
// =========================================================

[DllImport(
    "kernel32.dll",
    SetLastError = true)]
private static extern bool GetExitCodeProcess(
    IntPtr hProcess,
    out uint lpExitCode);


// =========================================================
// CLOSE HANDLE
// =========================================================

[DllImport(
    "kernel32.dll",
    SetLastError = true)]
private static extern bool CloseHandle(
    IntPtr hObject);
        // =========================================================
        // INSTALL SERVER
        // =========================================================

        private async void InstallServerButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_operationRunning)
                return;

            string installDirectory =
                ServerPathTextBox?.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(
                installDirectory))
            {
                MessageBox.Show(
                    "Please select an installation folder first.",
                    "Installation Folder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            SaveConfiguration();

            if (!File.Exists(_steamCmdPath))
            {
                MessageBox.Show(
                    "SteamCMD could not be found.\n\n" +
                    _steamCmdPath +
                    "\n\nUse the SteamCMD setup button first.",
                    "SteamCMD Not Found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            try
            {
                Directory.CreateDirectory(
                    installDirectory);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Folder Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            SetOperationRunning(
                true,
                "Installing...");

            ConsoleTextBox?.Clear();

            AppendConsole(
                "========================================");

            AppendConsole(
                "ASA SERVER INSTALLATION");

            AppendConsole(
                "========================================");

            AppendConsole("");

            AppendConsole(
                "Server: " +
                ServerDisplayName);

            AppendConsole("");

            AppendConsole(
                "Installation directory:");

            AppendConsole(
                installDirectory);

            AppendConsole("");

            AppendConsole(
                "ASA App ID: " +
                AsaAppId);

            AppendConsole("");

            try
            {
                string arguments =
                    "+login anonymous " +
                    "+force_install_dir \"" +
                    installDirectory +
                    "\" " +
                    "+app_update " +
                    AsaAppId +
                    " validate " +
                    "+quit";

                AppendConsole(
                    "Starting SteamCMD...");

                AppendConsole("");

                int exitCode =
                    await RunSteamCmdAsync(
                        arguments,
                        _steamCmdDirectory);

                AppendConsole("");

                if (exitCode != 0)
                {
                    AppendConsole(
                        "ASA installation failed.");

                    MessageBox.Show(
                        "SteamCMD returned exit code " +
                        exitCode +
                        ".",
                        "Installation Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return;
                }

                AppendConsole(
                    "SteamCMD completed successfully.");

                AppendConsole("");
                AppendConsole(
                    "Verifying ASA server files...");

                bool installed =
                    FindAsaServerExecutable(
                        installDirectory);

                if (installed)
                {
                    AppendConsole(
                        "ASA server executable found.");

                    AppendConsole("");
                    AppendConsole(
                        "ASA SERVER INSTALLATION COMPLETE.");

                    UpdateServerStatus();
                    UpdateServerButtons();
                    UpdateLaunchOptions();

                    MessageBox.Show(
                        "ARK: Survival Ascended dedicated server installation completed successfully.",
                        "Installation Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    AppendConsole(
                        "ASA server executable was not found.");

                    UpdateServerStatus();
                    UpdateServerButtons();

                    MessageBox.Show(
                        "SteamCMD completed, but the ASA server executable could not be found.",
                        "Verification Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                AppendConsole("");
                AppendConsole(
                    "INSTALLATION ERROR:");

                AppendConsole(
                    ex.ToString());

                MessageBox.Show(
                    ex.Message,
                    "Installation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetOperationRunning(
                    false);

                UpdateServerStatus();
                UpdateServerButtons();
                UpdateLaunchOptions();
            }
        }

    private async void UpdateServerButton_Click(
    object sender,
    RoutedEventArgs e)
{
    if (_operationRunning)
        return;

    string installDirectory =
        ServerPathTextBox?.Text?.Trim() ?? "";

    if (string.IsNullOrWhiteSpace(installDirectory))
    {
        MessageBox.Show(
            "Please select the ASA server folder first.",
            "Server Folder",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return;
    }

    if (!Directory.Exists(installDirectory))
    {
        MessageBox.Show(
            "The selected server folder does not exist.",
            "Server Folder",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return;
    }

    if (!File.Exists(_steamCmdPath))
    {
        MessageBox.Show(
            "SteamCMD could not be found.",
            "SteamCMD Not Found",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        return;
    }

    SetOperationRunning(
        true,
        "Updating...");

    ConsoleTextBox?.Clear();

    AppendConsole(
        "========================================");

    AppendConsole(
        "UPDATING ASA SERVER");

    AppendConsole(
        "========================================");

    AppendConsole("");

    AppendConsole(
        "Server: " +
        ServerDisplayName);

    AppendConsole("");

    AppendConsole(
        "Installation directory:");

    AppendConsole(
        installDirectory);

    AppendConsole("");

    AppendConsole(
        "ASA App ID: " +
        AsaAppId);

    AppendConsole("");

    try
    {
        string arguments =
            "+force_install_dir \"" +
            installDirectory +
            "\" " +
            "+login anonymous " +
            "+app_update " +
            AsaAppId +
            " " +
            "+quit";

        AppendConsole(
            "Starting SteamCMD update...");

        AppendConsole("");

        int exitCode =
            await RunSteamCmdAsync(
                arguments,
                _steamCmdDirectory);

        AppendConsole("");

        if (exitCode == 0)
        {
            AppendConsole(
                "SteamCMD update completed successfully.");

            AppendConsole("");

            AppendConsole(
                "ASA SERVER UPDATE COMPLETE.");

            UpdateServerStatus();
            UpdateServerButtons();
            UpdateLaunchOptions();

            MessageBox.Show(
                "ARK: Survival Ascended server update completed successfully.",
                "Update Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        else
        {
            AppendConsole(
                "ASA server update failed.");

            MessageBox.Show(
                "SteamCMD returned exit code " +
                exitCode +
                ".",
                "Update Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
    catch (Exception ex)
    {
        AppendConsole(
            "UPDATE ERROR:");

        AppendConsole(
            ex.ToString());

        MessageBox.Show(
            ex.Message,
            "Update Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
    finally
    {
        SetOperationRunning(
            false);

        UpdateServerStatus();
        UpdateServerButtons();
        UpdateLaunchOptions();
    }
}

        // =========================================================
        // VERIFY SERVER
        // =========================================================

        private async void VerifyServerButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_operationRunning)
                return;

            string installDirectory =
                ServerPathTextBox?.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(
                installDirectory))
            {
                MessageBox.Show(
                    "Please select the ASA server folder first.",
                    "Server Folder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (!Directory.Exists(
                installDirectory))
            {
                MessageBox.Show(
                    "The selected server folder does not exist.",
                    "Server Folder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (!File.Exists(_steamCmdPath))
            {
                MessageBox.Show(
                    "SteamCMD could not be found.",
                    "SteamCMD Not Found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            SetOperationRunning(
                true,
                "Verifying...");

            ConsoleTextBox?.Clear();

            AppendConsole(
                "========================================");

            AppendConsole(
                "VERIFYING ASA SERVER FILES");

            AppendConsole(
                "========================================");

            AppendConsole("");

            try
            {
                string arguments =
                    "+force_install_dir \"" +
                    installDirectory +
                    "\" " +
                    "+login anonymous " +
                    "+app_update " +
                    AsaAppId +
                    " validate " +
                    "+quit";

                AppendConsole(
                    "Starting SteamCMD verification...");

                AppendConsole("");

                int exitCode =
                    await RunSteamCmdAsync(
                        arguments,
                        _steamCmdDirectory);

                AppendConsole("");

                if (exitCode == 0)
                {
                    AppendConsole(
                        "SteamCMD file verification completed.");

                    UpdateServerStatus();
                    UpdateServerButtons();

                    MessageBox.Show(
                        "Server file verification completed.",
                        "Verification Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    AppendConsole(
                        "SteamCMD verification failed.");

                    MessageBox.Show(
                        "SteamCMD returned exit code " +
                        exitCode +
                        ".",
                        "Verification Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                AppendConsole(
                    "VERIFICATION ERROR:");

                AppendConsole(
                    ex.ToString());

                MessageBox.Show(
                    ex.Message,
                    "Verification Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetOperationRunning(
                    false);

                UpdateServerStatus();
                UpdateServerButtons();
                UpdateLaunchOptions();
            }
        }

        // =========================================================
        // START SERVER
        // =========================================================

        private void StartServerButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_serverOperationRunning ||
                _asaServerManager.IsRunning)
            {
                return;
            }

            string serverDirectory =
                ServerPathTextBox?.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(
                serverDirectory))
            {
                MessageBox.Show(
                    "Please select the ASA server folder first.",
                    "Server Folder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            SaveConfiguration();

            if (!FindAsaServerExecutable(
                serverDirectory))
            {
                MessageBox.Show(
                    "The ASA server executable could not be found.",
                    "Server Not Installed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                UpdateServerStatus();
                return;
            }

            try
            {
                _serverOperationRunning = true;

                UpdateRuntimeStatus(
                    "● STARTING",
                    Brushes.Gold);

                UpdateServerButtons();

                AppendConsole("");
                AppendConsole(
                    "========================================");

                AppendConsole(
                    "STARTING ASA SERVER");

                AppendConsole(
                    "========================================");

                AppendConsole("");

                AppendConsole(
                    "Server: " +
                    ServerDisplayName);

                AppendConsole("");

                string arguments =
    LaunchOptionsTextBox?.Text?.Trim() ?? "";

if (string.IsNullOrWhiteSpace(arguments))
{
    throw new Exception(
        "Launch options cannot be empty.");
}

AppendConsole(
    "Launch options:");

AppendConsole(
    arguments);

                AppendConsole("");

                bool started =
                    _asaServerManager.Start(
                        serverDirectory,
                        arguments);

                if (!started)
                {
                    throw new Exception(
                        "The ASA server process could not be started.");
                }

                AppendConsole(
                    "ASA server process started.");

                UpdateProcessId();

                _ = MonitorServerStartupAsync();
            }
            catch (Exception ex)
            {
                AppendConsole(
                    "SERVER START ERROR:");

                AppendConsole(
                    ex.ToString());

                _serverOperationRunning = false;

                UpdateRuntimeStatus(
                    "● OFFLINE",
                    Brushes.IndianRed);

                UpdateServerStatus();
                UpdateServerButtons();

                MessageBox.Show(
                    ex.Message,
                    "Server Start Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

private async void SaveWorldButton_Click(
    object sender,
    RoutedEventArgs e)
{
    if (!_asaServerManager.IsRunning)
    {
        AppendConsole(
            "Save World skipped: server is not running.");

        return;
    }

    SaveWorldButton.IsEnabled = false;

    try
    {
        AppendConsole("");
        AppendConsole(
            "========================================");
        AppendConsole(
            "SAVING WORLD");
        AppendConsole(
            "========================================");

        AppendConsole(
            "Sending command: cheat SaveWorld");

        bool sent =
            await _asaServerManager.SendCommandAsync(
                "SaveWorld");

        if (!sent)
        {
            throw new Exception(
                "Could not send the SaveWorld command to the server.");
        }

        AppendConsole(
            "SaveWorld command sent.");

        AppendConsole(
            "Waiting for the server to process the save...");
    }
    catch (Exception ex)
    {
        AppendConsole(
            "SAVE WORLD ERROR:");

        AppendConsole(
            ex.ToString());

        MessageBox.Show(
            ex.Message,
            "Save World Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
    finally
    {
        UpdateServerButtons();
    }
}

        private void BrowseClusterDirectoryButton_Click(
    object sender,
    RoutedEventArgs e)
{
    try
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog();

        dialog.Description =
            "Select the cluster directory";

        dialog.UseDescriptionForTitle = true;

        string currentDirectory =
            ClusterDirectoryTextBox?.Text?.Trim() ?? "";

        if (Directory.Exists(currentDirectory))
        {
            dialog.SelectedPath = currentDirectory;
        }

        if (dialog.ShowDialog() ==
            System.Windows.Forms.DialogResult.OK)
        {
            if (ClusterDirectoryTextBox != null)
            {
                ClusterDirectoryTextBox.Text =
                    dialog.SelectedPath;
            }

            UpdateLaunchOptions();
        }
    }
    catch (Exception ex)
    {
        AppendConsole(
            "CLUSTER DIRECTORY BROWSE ERROR:");

        AppendConsole(ex.ToString());

        MessageBox.Show(
            ex.Message,
            "Cluster Directory",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}

    public async Task StartServerForClusterAsync()
{
    if (_serverOperationRunning ||
        _asaServerManager.IsRunning)
    {
        AppendConsole(
            "Cluster start skipped: server is already running.");

        return;
    }

    string serverDirectory =
        ServerPathTextBox?.Text?.Trim() ?? "";

    if (string.IsNullOrWhiteSpace(serverDirectory))
    {
        AppendConsole(
            "Cluster start skipped: server folder is not configured.");

        return;
    }

    SaveConfiguration();

    if (!FindAsaServerExecutable(
        serverDirectory))
    {
        AppendConsole(
            "Cluster start skipped: ASA server executable was not found.");

        UpdateServerStatus();

        return;
    }

    try
    {
        _serverOperationRunning = true;

        UpdateRuntimeStatus(
            "● STARTING",
            Brushes.Gold);

        UpdateServerButtons();

        AppendConsole("");
        AppendConsole(
            "========================================");

        AppendConsole(
            "STARTING CLUSTER SERVER");

        AppendConsole(
            "========================================");

        AppendConsole("");

        AppendConsole(
            "Server: " +
            ServerDisplayName);

        AppendConsole("");

        string arguments =
            BuildServerArguments();

        AppendConsole(
            "Launch options:");

        AppendConsole(
            arguments);

        AppendConsole("");

        bool started =
            _asaServerManager.Start(
                serverDirectory,
                arguments);

        if (!started)
        {
            throw new Exception(
                "The ASA server process could not be started.");
        }

        AppendConsole(
            "ASA server process started.");

        UpdateProcessId();

        _ = MonitorServerStartupAsync();
    }
    catch (Exception ex)
    {
        AppendConsole(
            "SERVER START ERROR:");

        AppendConsole(
            ex.ToString());

        _serverOperationRunning = false;

        UpdateRuntimeStatus(
            "● OFFLINE",
            Brushes.IndianRed);

        UpdateServerStatus();
        UpdateServerButtons();

        AppendConsole(
            "Cluster server failed to start.");

        return;
    }

    await Task.CompletedTask;
}

public async Task StopServerForClusterAsync()
{
    if (!_asaServerManager.IsRunning &&
        !_serverOperationRunning)
    {
        AppendConsole(
            "Cluster stop skipped: server is not running.");

        return;
    }

    try
    {
        _serverOperationRunning = true;

        UpdateRuntimeStatus(
            "● STOPPING",
            Brushes.Gold);

        UpdateServerButtons();

        AppendConsole("");
        AppendConsole(
            "========================================");
        AppendConsole(
            "STOPPING CLUSTER SERVER");
        AppendConsole(
            "========================================");
        AppendConsole("");

        AppendConsole(
            "Server: " +
            ServerDisplayName);

        if (_asaServerManager.IsRunning)
        {
            await _asaServerManager.StopAsync();
        }

        AppendConsole(
            "ASA server process stopped.");

        _serverOperationRunning = false;

        UpdateRuntimeStatus(
            "● OFFLINE",
            Brushes.IndianRed);

        UpdateServerStatus();
        UpdateServerButtons();

        AppendConsole(
            "Cluster server stopped.");
    }
    catch (Exception ex)
    {
        AppendConsole(
            "SERVER STOP ERROR:");

        AppendConsole(
            ex.ToString());

        _serverOperationRunning = false;

        UpdateRuntimeStatus(
            "● OFFLINE",
            Brushes.IndianRed);

        UpdateServerStatus();
        UpdateServerButtons();
    }
}


        // =========================================================
        // MONITOR SERVER STARTUP
        // =========================================================

        private async Task MonitorServerStartupAsync()
        {
            try
            {
                const int timeoutSeconds = 60;

                for (int i = 0;
                     i < timeoutSeconds;
                     i++)
                {
                    await Task.Delay(1000);

                    if (!_asaServerManager.IsRunning)
                    {
                        if (!_asaServerManager.ProcessId.HasValue)
                            break;

                        continue;
                    }

                    await Dispatcher.InvokeAsync(() =>
                    {
                        _serverOperationRunning = false;

                        UpdateRuntimeStatus(
                            "● RUNNING",
                            Brushes.LightGreen);

                        UpdateServerStatus();
                        UpdateServerButtons();
                        UpdateProcessId();

                        AppendConsole("");
                        AppendConsole(
                            "ASA SERVER IS RUNNING.");
                    });

                    return;
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    if (_asaServerManager.IsRunning)
                    {
                        _serverOperationRunning = false;

                        UpdateRuntimeStatus(
                            "● RUNNING",
                            Brushes.LightGreen);

                        UpdateServerStatus();
                        UpdateServerButtons();
                        UpdateProcessId();

                        return;
                    }

                    _serverOperationRunning = false;

                    UpdateRuntimeStatus(
                        "● STARTING",
                        Brushes.Gold);

                    UpdateServerStatus();
                    UpdateServerButtons();

                    AppendConsole("");
                    AppendConsole(
                        "ASA is still starting. Waiting for the server process to report running.");
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    AppendConsole(
                        "SERVER MONITOR ERROR:");

                    AppendConsole(
                        ex.ToString());

                    _serverOperationRunning = false;

                    UpdateServerButtons();
                });
            }
        }

        // =========================================================
        // STOP SERVER
        // =========================================================

        private async void StopServerButton_Click(
    object sender,
    RoutedEventArgs e)
{
    if (!_asaServerManager.IsRunning)
    {
        AppendConsole(
            "Stop skipped: server is not running.");

        return;
    }

    StopServerButton.IsEnabled = false;

    try
    {
        AppendConsole("");
        AppendConsole("========================================");
        AppendConsole("STOPPING SERVER");
        AppendConsole("========================================");
        AppendConsole("");

        AppendConsole(
            "Server: " +
            ServerDisplayName);

        await _asaServerManager.StopAsync();

        AppendConsole("");
        AppendConsole(
            "Server stopped successfully.");

        UpdateRuntimeStatus(
            "● OFFLINE",
            Brushes.IndianRed);

        UpdateProcessId();
        UpdateServerStatus();
        UpdateServerButtons();
    }
    catch (Exception ex)
    {
        AppendConsole("");
        AppendConsole(
            "SERVER STOP ERROR:");

        AppendConsole(
            ex.ToString());

        MessageBox.Show(
            ex.Message,
            "Server Stop Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        UpdateServerStatus();
        UpdateServerButtons();
    }
}

        // =========================================================
        // WAIT FOR STOP
        // =========================================================

        private async Task WaitForServerToStopAsync()
        {
            for (int i = 0; i < 30; i++)
            {
                if (!_asaServerManager.IsRunning)
                    return;

                await Task.Delay(500);
            }
        }

        // =========================================================
        // RESTART SERVER
        // =========================================================

        private async void RestartServerButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_serverOperationRunning)
                return;

            string serverDirectory =
                ServerPathTextBox?.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(
                serverDirectory))
            {
                MessageBox.Show(
                    "Please select the ASA server folder first.",
                    "Server Folder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (!FindAsaServerExecutable(
                serverDirectory))
            {
                MessageBox.Show(
                    "The ASA server executable could not be found.",
                    "Server Not Installed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            try
            {
                _serverOperationRunning = true;

                UpdateRuntimeStatus(
                    "● RESTARTING",
                    Brushes.Gold);

                UpdateServerStatus();
                UpdateServerButtons();

                AppendConsole("");
                AppendConsole(
                    "========================================");

                AppendConsole(
                    "RESTARTING ASA SERVER");

                AppendConsole(
                    "========================================");

                AppendConsole("");

                string arguments =
                    BuildServerArguments();

                AppendConsole(
                    "Launch options:");

                AppendConsole(
                    arguments);

                AppendConsole("");

                await _asaServerManager.RestartAsync(
                    serverDirectory,
                    arguments);

                AppendConsole(
                    "ASA server restart initiated.");

                UpdateProcessId();

                _serverOperationRunning = false;

                UpdateRuntimeStatus(
                    "● STARTING",
                    Brushes.Gold);

                UpdateServerStatus();
                UpdateServerButtons();

                _ = MonitorServerStartupAsync();
            }
            catch (Exception ex)
            {
                AppendConsole(
                    "SERVER RESTART ERROR:");

                AppendConsole(
                    ex.ToString());

                _serverOperationRunning = false;

                UpdateRuntimeStatus(
                    "● OFFLINE",
                    Brushes.IndianRed);

                UpdateServerStatus();
                UpdateServerButtons();
            }
        }

        // =========================================================
        // PROCESS ID
        // =========================================================

        private void UpdateProcessId()
        {
            if (ServerProcessIdText == null)
                return;

            if (_asaServerManager.ProcessId.HasValue)
            {
                ServerProcessIdText.Text =
                    "PID: " +
                    _asaServerManager.ProcessId.Value;
            }
            else
            {
                ServerProcessIdText.Text = "";
            }
        }

        // =========================================================
        // GAME.INI
        // =========================================================

        private void EditGameIniButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenIniFile(
                GetGameIniPath(),
                "Game.ini");
        }

        // =========================================================
        // GAMEUSERSETTINGS.INI
        // =========================================================

        private void EditGameUserSettingsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenIniFile(
                GetGameUserSettingsIniPath(),
                "GameUserSettings.ini");
        }

        // =========================================================
        // OPEN INI
        // =========================================================

        private void OpenIniFile(
            string filePath,
            string displayName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(
                    ServerPathTextBox?.Text))
                {
                    MessageBox.Show(
                        "Please select your ASA server folder first.",
                        "Server Folder",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                string directory =
                    GetAsaConfigDirectory();

                if (string.IsNullOrWhiteSpace(directory))
                {
                    MessageBox.Show(
                        "The ASA configuration directory could not be determined.",
                        "Configuration",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return;
                }

                Directory.CreateDirectory(
                    directory);

                if (!File.Exists(filePath))
                {
                    File.WriteAllText(
                        filePath,
                        "");
                }

                string notepadPlusPlus =
                    FindNotepadPlusPlus();

                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName =
                            string.IsNullOrWhiteSpace(
                                notepadPlusPlus)
                                ? "notepad.exe"
                                : notepadPlusPlus,

                        Arguments =
                            "\"" +
                            filePath +
                            "\"",

                        UseShellExecute = true
                    });

                AppendConsole("");
                AppendConsole(
                    "Opened " +
                    displayName +
                    ":");

                AppendConsole(
                    filePath);
            }
            catch (Exception ex)
            {
                AppendConsole(
                    "INI EDITOR ERROR:");

                AppendConsole(
                    ex.ToString());

                MessageBox.Show(
                    ex.Message,
                    "INI Editor Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // =========================================================
        // NOTEPAD++
        // =========================================================

        private string FindNotepadPlusPlus()
        {
            string[] paths =
            {
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ProgramFiles),
                    "Notepad++",
                    "notepad++.exe"),

                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ProgramFilesX86),
                    "Notepad++",
                    "notepad++.exe"),

                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "Programs",
                    "Notepad++",
                    "notepad++.exe")
            };

            foreach (string path in paths)
            {
                if (File.Exists(path))
                    return path;
            }

            try
            {
                ProcessStartInfo psi =
                    new ProcessStartInfo
                    {
                        FileName = "where.exe",
                        Arguments = "notepad++.exe",
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                using Process? process =
                    Process.Start(psi);

                if (process != null)
                {
                    string output =
                        process.StandardOutput
                            .ReadToEnd()
                            .Trim();

                    process.WaitForExit();

                    if (!string.IsNullOrWhiteSpace(
                        output))
                    {
                        string[] outputLines =
                            output.Split(
                                new[]
                                {
                                    '\r',
                                    '\n'
                                },
                                StringSplitOptions.RemoveEmptyEntries);

                        if (outputLines.Length > 0)
                        {
                            string firstPath =
                                outputLines[0].Trim();

                            if (File.Exists(firstPath))
                                return firstPath;
                        }
                    }
                }
            }
            catch
            {
            }

            return "";
        }

        // =========================================================
        // STEAMCMD STATUS
        // =========================================================

        private void UpdateSteamCmdStatus()
        {
            if (SteamCmdLocationText == null)
                return;

            SteamCmdLocationText.Text =
                _steamCmdDownloader.SteamCmdPath;

            if (_steamCmdDownloader.IsInstalled())
            {
                if (SteamCmdStatusText != null)
                {
                    SteamCmdStatusText.Text =
                        "● READY";

                    SteamCmdStatusText.Foreground =
                        Brushes.LightGreen;
                }
            }
            else
            {
                if (SteamCmdStatusText != null)
                {
                    SteamCmdStatusText.Text =
                        "● NOT INSTALLED";

                    SteamCmdStatusText.Foreground =
                        Brushes.IndianRed;
                }
            }
        }

        // =========================================================
        // INSTALL STEAMCMD
        // =========================================================

        private async void InstallSteamCmdButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_operationRunning)
                return;

            if (InstallSteamCmdButton != null)
                InstallSteamCmdButton.IsEnabled = false;

            try
            {
                if (SteamCmdStatusText != null)
                {
                    SteamCmdStatusText.Text =
                        "● INSTALLING...";

                    SteamCmdStatusText.Foreground =
                        Brushes.Gold;
                }

                AppendConsole("");
                AppendConsole(
                    "========================================");

                AppendConsole(
                    "STEAMCMD SETUP");

                AppendConsole(
                    "========================================");

                AppendConsole("");

                AppendConsole(
                    "Server: " +
                    ServerDisplayName);

                AppendConsole(
                    "SteamCMD directory:");

                AppendConsole(
                    _steamCmdDirectory);

                AppendConsole("");

                await _steamCmdDownloader.DownloadAsync(null);

                UpdateSteamCmdStatus();

                AppendConsole("");
                AppendConsole(
                    "SteamCMD setup completed.");
            }
            catch (Exception ex)
            {
                if (SteamCmdStatusText != null)
                {
                    SteamCmdStatusText.Text =
                        "● ERROR";

                    SteamCmdStatusText.Foreground =
                        Brushes.IndianRed;
                }

                AppendConsole(
                    "STEAMCMD ERROR:");

                AppendConsole(
                    ex.ToString());

                MessageBox.Show(
                    ex.Message,
                    "SteamCMD Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                if (InstallSteamCmdButton != null)
                    InstallSteamCmdButton.IsEnabled = true;

                UpdateSteamCmdStatus();
            }
        }

        // =========================================================
        // ASA OUTPUT
        // =========================================================

        private void AsaServer_OutputReceived(
            string text)
        {
            Dispatcher.Invoke(() =>
            {
                AppendConsole(
                    "[ASA] " +
                    text);

                string lower =
                    text?.ToLowerInvariant() ?? "";

                if (lower.Contains(
                        "full startup took") ||
                    lower.Contains(
                        "server has successfully started") ||
                    lower.Contains(
                        "listening on") ||
                    lower.Contains(
                        "game server initialized"))
                {
                    UpdateRuntimeStatus(
                        "● RUNNING",
                        Brushes.LightGreen);

                    _serverOperationRunning = false;

                    UpdateServerStatus();
                    UpdateServerButtons();
                    UpdateProcessId();
                }
            });
        }

        private void AsaServer_ErrorReceived(
            string text)
        {
            Dispatcher.Invoke(() =>
            {
                AppendConsole(
                    "[ASA ERROR] " +
                    text);
            });
        }

        // =========================================================
        // SERVER EXITED
        // =========================================================

        private void AsaServer_Exited(
            int exitCode)
        {
            Dispatcher.Invoke(() =>
            {
                AppendConsole("");
                AppendConsole(
                    "========================================");

                AppendConsole(
                    "ASA SERVER PROCESS EXITED");

                AppendConsole(
                    "Server: " +
                    ServerDisplayName);

                AppendConsole(
                    "Exit code: " +
                    exitCode);

                AppendConsole(
                    "========================================");

                UpdateRuntimeStatus(
                    "● OFFLINE",
                    Brushes.IndianRed);

                if (ServerProcessIdText != null)
                    ServerProcessIdText.Text = "";

                _serverOperationRunning = false;

                UpdateServerStatus();
                UpdateServerButtons();
            });
        }

        // =========================================================
        // SERVER EXECUTABLE
        // =========================================================

        private bool FindAsaServerExecutable(
            string installDirectory)
        {
            if (string.IsNullOrWhiteSpace(
                installDirectory))
            {
                return false;
            }

            string expectedPath =
                Path.Combine(
                    installDirectory,
                    "ShooterGame",
                    "Binaries",
                    "Win64",
                    "ArkAscendedServer.exe");

            return File.Exists(
                expectedPath);
        }

        // =========================================================
        // SERVER STATUS
        // =========================================================

        private void UpdateServerStatus()
        {
            if (ServerStatusText == null)
                return;

            if (_asaServerManager.IsRunning)
            {
                ServerStatusText.Text =
                    "● RUNNING";

                ServerStatusText.Foreground =
                    Brushes.LightGreen;

                return;
            }

            ServerStatusText.Text =
                "● OFFLINE";

            ServerStatusText.Foreground =
                Brushes.IndianRed;
        }

        // =========================================================
        // RUNTIME STATUS
        // =========================================================

        private void UpdateRuntimeStatus(
            string text,
            Brush color)
        {
            if (ServerRuntimeStatusText == null)
                return;

            ServerRuntimeStatusText.Text =
                text;

            ServerRuntimeStatusText.Foreground =
                color;

            if (_asaServerManager.IsRunning)
            {
                if (ServerStatusText != null)
                {
                    ServerStatusText.Text =
                        "● RUNNING";

                    ServerStatusText.Foreground =
                        Brushes.LightGreen;
                }
            }
            else if (text.Contains(
                         "STARTING") ||
                     text.Contains(
                         "STOPPING") ||
                     text.Contains(
                         "RESTARTING"))
            {
                if (ServerStatusText != null)
                {
                    ServerStatusText.Text =
                        text;

                    ServerStatusText.Foreground =
                        color;
                }
            }
            else
            {
                UpdateServerStatus();
            }
        }

        // =========================================================
        // SERVER BUTTON STATE
        // =========================================================

      private void UpdateServerButtons()
{
    bool running =
        _asaServerManager.IsRunning;

    bool busy =
        _serverOperationRunning;

    StartServerButton.IsEnabled =
        !running &&
        !busy;

    StopServerButton.IsEnabled =
        running &&
        !busy;

    RestartServerButton.IsEnabled =
        running &&
        !busy;

    SaveWorldButton.IsEnabled =
        running &&
        !busy;

    UpdateProcessId();
}

        // =========================================================
        // STEAMCMD OPERATION STATE
        // =========================================================

        private void SetOperationRunning(
            bool running,
            string? operationText = null)
        {
            _operationRunning =
                running;

            if (InstallServerButton != null)
            {
                InstallServerButton.IsEnabled =
                    !running;

                InstallServerButton.Content =
                    running
                        ? (
                            operationText ??
                            "Working..."
                          )
                        : "Install Server";
            }

            if (VerifyServerButton != null)
            {
                VerifyServerButton.IsEnabled =
                    !running;

                VerifyServerButton.Content =
                    running
                        ? (
                            operationText ??
                            "Working..."
                          )
                        : "Verify Files";
            }

            UpdateServerButtons();
        }

        // =========================================================
        // CLEAR CONSOLE
        // =========================================================

        private void ClearConsoleButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                if (ConsoleTextBox == null)
                    return;

                ConsoleTextBox.Clear();
            }
            catch (Exception ex)
            {
                AppendConsole(
                    "CONSOLE CLEAR ERROR:");

                AppendConsole(
                    ex.Message);
            }
        }

        // =========================================================
        // CONSOLE
        // =========================================================

        private void AppendConsole(
            string text)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() =>
                {
                    AppendConsole(text);
                });

                return;
            }

            if (ConsoleTextBox == null)
                return;

            if (string.IsNullOrEmpty(text))
            {
                ConsoleTextBox.AppendText(
                    Environment.NewLine);

                ConsoleTextBox.ScrollToEnd();

                return;
            }

            ConsoleTextBox.AppendText(
                text);

            ConsoleTextBox.AppendText(
                Environment.NewLine);

            ConsoleTextBox.ScrollToEnd();
        }

    private void AppendConsoleLive(
    string text)
{
    if (!Dispatcher.CheckAccess())
    {
        Dispatcher.Invoke(() =>
        {
            AppendConsoleLive(text);
        });

        return;
    }

    if (ConsoleTextBox == null)
        return;

    if (string.IsNullOrEmpty(text))
        return;

    ConsoleTextBox.AppendText(
        text);

    ConsoleTextBox.ScrollToEnd();
}

        // =========================================================
        // SHUTDOWN
        // =========================================================

        public void ShutdownServerManager()
        {
            try
            {
                if (ServerPathTextBox != null &&
                    !string.IsNullOrWhiteSpace(
                        ServerPathTextBox.Text))
                {
                    SaveConfiguration();
                }

                if (_asaServerManager.IsRunning)
                {
                    try
                    {
                        _asaServerManager.StopAsync()
                            .GetAwaiter()
                            .GetResult();
                    }
                    catch
                    {
                    }
                }

                _asaServerManager.Dispose();
            }
            catch
            {
                // Ignore shutdown errors.
            }
        }
    }
}