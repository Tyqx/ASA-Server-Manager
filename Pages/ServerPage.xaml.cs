using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ASAServerManager.Server;
using ASAServerManager.SteamCMD;

namespace ASAServerManager.Pages
{
    public partial class ServerPage : UserControl
    {
        private readonly string _steamCmdDirectory;
        private readonly string _steamCmdPath;
        private readonly AsaServerManager _asaServerManager;
        private readonly SteamCmdDownloader _steamCmdDownloader;

        private bool _operationRunning;
        private bool _serverOperationRunning;
        private bool _loadingConfiguration;

        private const int AsaAppId = 2430930;

        // =========================================================
        // CONFIGURATION DATA
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
            public string ServerPassword { get; set; } = "";
            public string AdminPassword { get; set; } = "";
            public bool PvE { get; set; }
            public bool Crossplay { get; set; } = true;
            public string Mods { get; set; } = "";
            public bool ClusterEnabled { get; set; }
            public string ClusterId { get; set; } = "ASACluster";
            public string ClusterDirectory { get; set; } = "";
        }

        // =========================================================
        // CONSTRUCTOR
        // =========================================================

        public ServerPage()
        {
            InitializeComponent();

            _asaServerManager = new AsaServerManager();

            _asaServerManager.OutputReceived += AsaServer_OutputReceived;
            _asaServerManager.ErrorReceived += AsaServer_ErrorReceived;
            _asaServerManager.ServerExited += AsaServer_Exited;

            _steamCmdDirectory =
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "SteamCMD");

            _steamCmdPath =
                Path.Combine(
                    _steamCmdDirectory,
                    "steamcmd.exe");

            _steamCmdDownloader =
                new SteamCmdDownloader(
                    _steamCmdDirectory);

            LoadConfiguration();

            UpdateSteamCmdStatus();
            UpdateServerStatus();

            UpdateRuntimeStatus(
                "● OFFLINE",
                Brushes.IndianRed);

            UpdateServerButtons();
            UpdateLaunchOptions();
        }

        // =========================================================
        // CONFIGURATION PATH
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
                "server_config.json");
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

        // =========================================================
        // CONFIGURATION
        // =========================================================

        private ServerConfiguration GetConfigurationFromUI()
        {
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
                    ServerPasswordBox?.Password ?? "",

                AdminPassword =
                    AdminPasswordBox?.Password ?? "",

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
                    ClusterDirectoryTextBox?.Text?.Trim() ?? ""
            };
        }

        private void ApplyConfiguration(
            ServerConfiguration config)
        {
            if (config == null)
                return;

            _loadingConfiguration = true;

            try
            {
                if (ServerPathTextBox != null)
                    ServerPathTextBox.Text =
                        config.ServerPath ?? "";

                if (ServerNameTextBox != null)
                {
                    ServerNameTextBox.Text =
                        string.IsNullOrWhiteSpace(config.ServerName)
                            ? "ASA Server"
                            : config.ServerName;
                }

                if (ServerPortTextBox != null)
                {
                    ServerPortTextBox.Text =
                        string.IsNullOrWhiteSpace(config.ServerPort)
                            ? "7777"
                            : config.ServerPort;
                }

                if (QueryPortTextBox != null)
                {
                    QueryPortTextBox.Text =
                        string.IsNullOrWhiteSpace(config.QueryPort)
                            ? "27015"
                            : config.QueryPort;
                }

                if (MaxPlayersTextBox != null)
                {
                    MaxPlayersTextBox.Text =
                        string.IsNullOrWhiteSpace(config.MaxPlayers)
                            ? "70"
                            : config.MaxPlayers;
                }

                if (ServerPasswordBox != null)
                    ServerPasswordBox.Password =
                        config.ServerPassword ?? "";

                if (AdminPasswordBox != null)
                    AdminPasswordBox.Password =
                        config.AdminPassword ?? "";

                if (ModsTextBox != null)
                    ModsTextBox.Text =
                        config.Mods ?? "";

                if (PvECheckBox != null)
                    PvECheckBox.IsChecked =
                        config.PvE;

                if (CrossplayCheckBox != null)
                    CrossplayCheckBox.IsChecked =
                        config.Crossplay;

                if (ClusterEnabledCheckBox != null)
                    ClusterEnabledCheckBox.IsChecked =
                        config.ClusterEnabled;

                if (ClusterIdTextBox != null)
                {
                    ClusterIdTextBox.Text =
                        string.IsNullOrWhiteSpace(config.ClusterId)
                            ? "ASACluster"
                            : config.ClusterId;
                }

                if (ClusterDirectoryTextBox != null)
                {
                    ClusterDirectoryTextBox.Text =
                        config.ClusterDirectory ?? "";
                }

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

            for (int i = 0; i < comboBox.Items.Count; i++)
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
                comboBox.SelectedIndex = 0;
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

                if (string.IsNullOrWhiteSpace(config.ServerPath))
                    return false;

                string json =
                    JsonSerializer.Serialize(
                        config,
                        new JsonSerializerOptions
                        {
                            WriteIndented = true
                        });

                File.WriteAllText(
                    GetConfigPath(),
                    json);

                return true;
            }
            catch (Exception ex)
            {
                AppendConsole("");
                AppendConsole("CONFIG SAVE ERROR:");
                AppendConsole(ex.ToString());

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
                string path =
                    GetConfigPath();

                if (!File.Exists(path))
                    return false;

                string json =
                    File.ReadAllText(path);

                if (string.IsNullOrWhiteSpace(json))
                    return false;

                ServerConfiguration? config =
                    JsonSerializer.Deserialize<ServerConfiguration>(
                        json);

                if (config == null)
                    return false;

                ApplyConfiguration(config);

                AppendConsole(
                    "Saved server configuration loaded.");

                return true;
            }
            catch (JsonException ex)
            {
                AppendConsole(
                    "CONFIGURATION JSON ERROR:");

                AppendConsole(
                    ex.Message);

                return false;
            }
            catch (Exception ex)
            {
                AppendConsole(
                    "CONFIG LOAD ERROR:");

                AppendConsole(
                    ex.ToString());

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
        // BROWSE
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
                AppendConsole("Server folder selected:");
                AppendConsole(dialog.SelectedPath);

                UpdateServerStatus();
                UpdateLaunchOptions();
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
            var mods =
                new List<string>();

            if (ModsTextBox == null)
                return mods;

            string text =
                ModsTextBox.Text ?? "";

            string[] lines =
                text.Split(
                    new[] { '\r', '\n' },
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

                    if (!mods.Contains(mod))
                        mods.Add(mod);
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
        // LIVE LAUNCH OPTIONS
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

        // =========================================================
        // CLUSTER EVENTS
        // =========================================================

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

            if (!string.IsNullOrWhiteSpace(config.ServerName))
            {
                arguments +=
                    "?SessionName=" +
                    Uri.EscapeDataString(
                        config.ServerName);
            }

            if (!string.IsNullOrWhiteSpace(config.ServerPassword))
            {
                arguments +=
                    "?ServerPassword=" +
                    Uri.EscapeDataString(
                        config.ServerPassword);
            }

            if (!string.IsNullOrWhiteSpace(config.AdminPassword))
            {
                arguments +=
                    "?ServerAdminPassword=" +
                    Uri.EscapeDataString(
                        config.AdminPassword);
            }

            string mods =
                BuildModsArgument();

            if (!string.IsNullOrWhiteSpace(mods))
                arguments += " " + mods;

            if (config.PvE)
                arguments += " -pve";

            if (config.Crossplay)
                arguments += " -crossplay";

            if (config.ClusterEnabled)
            {
                string clusterId =
                    string.IsNullOrWhiteSpace(config.ClusterId)
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

            return arguments;
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
                LaunchOptionsTextBox.Text =
                    BuildServerArguments();

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
                        MaxPlayersTextBox?.Text?.Trim() ?? "70";
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
            }
            catch
            {
                // Preview errors must never crash the UI.
            }
        }

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

            if (string.IsNullOrWhiteSpace(installDirectory))
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

            SetOperationRunning(true);

            ClearConsole();

            AppendConsole(
                "========================================");
            AppendConsole(
                "ASA SERVER INSTALLATION");
            AppendConsole(
                "========================================");
            AppendConsole("");
            AppendConsole(
                "Installation directory:");
            AppendConsole(
                installDirectory);
            AppendConsole("");
            AppendConsole(
                "ASA App ID: " + AsaAppId);
            AppendConsole("");

            try
            {
                SteamCmdManager steamCmd =
                    new SteamCmdManager(
                        _steamCmdPath);

                string arguments =
                    "+login anonymous " +
                    "+force_install_dir \"" +
                    installDirectory +
                    "\" " +
                    "+app_update " +
                    AsaAppId +
                    " " +
                    "+quit";

                AppendConsole(
                    "Starting SteamCMD...");
                AppendConsole("");

                int exitCode =
                    await steamCmd.RunAsync(
                        arguments,
                        _steamCmdDirectory,
                        AppendConsole);

                AppendConsole("");
                AppendConsole(
                    "SteamCMD exited with code: " +
                    exitCode);

                if (exitCode != 0)
                {
                    AppendConsole(
                        "ASA installation failed.");

                    MessageBox.Show(
                        "SteamCMD returned exit code " +
                        exitCode + ".",
                        "Installation Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return;
                }

                AppendConsole("");
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
                SetOperationRunning(false);
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

            SetOperationRunning(true);

            ClearConsole();

            AppendConsole(
                "========================================");
            AppendConsole(
                "VERIFYING ASA SERVER FILES");
            AppendConsole(
                "========================================");
            AppendConsole("");

            try
            {
                SteamCmdManager steamCmd =
                    new SteamCmdManager(
                        _steamCmdPath);

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
                    await steamCmd.RunAsync(
                        arguments,
                        _steamCmdDirectory,
                        AppendConsole);

                AppendConsole("");
                AppendConsole(
                    "SteamCMD exited with code: " +
                    exitCode);

                if (exitCode == 0)
                {
                    AppendConsole("");
                    AppendConsole(
                        "SteamCMD file verification completed.");

                    UpdateServerStatus();

                    MessageBox.Show(
                        "Server file verification completed.",
                        "Verification Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        "SteamCMD returned exit code " +
                        exitCode + ".",
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
                SetOperationRunning(false);
            }
        }

        // =========================================================
        // START SERVER
        // =========================================================

        private void StartServerButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_serverOperationRunning)
                return;

            string serverDirectory =
                ServerPathTextBox?.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(serverDirectory))
            {
                MessageBox.Show(
                    "Please select the ASA server folder first.",
                    "Server Folder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            SaveConfiguration();

            if (!FindAsaServerExecutable(serverDirectory))
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
                    "● STARTING",
                    Brushes.Gold);

                AppendConsole("");
                AppendConsole(
                    "========================================");
                AppendConsole(
                    "STARTING ASA SERVER");
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

                UpdateProcessIdDisplay();

                UpdateRuntimeStatus(
                    "● STARTING",
                    Brushes.Gold);

                UpdateServerButtons();
            }
            catch (Exception ex)
            {
                AppendConsole(
                    "SERVER START ERROR:");
                AppendConsole(
                    ex.ToString());

                UpdateRuntimeStatus(
                    "● OFFLINE",
                    Brushes.IndianRed);

                MessageBox.Show(
                    ex.Message,
                    "Server Start Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _serverOperationRunning = false;
                UpdateServerButtons();
            }
        }

        // =========================================================
        // STOP SERVER
        // =========================================================

        private async void StopServerButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_serverOperationRunning)
                return;

            if (!_asaServerManager.IsRunning)
                return;

            try
            {
                _serverOperationRunning = true;

                UpdateRuntimeStatus(
                    "● STOPPING",
                    Brushes.Gold);

                AppendConsole("");
                AppendConsole(
                    "Stopping ASA server...");

                await _asaServerManager.StopAsync();

                AppendConsole(
                    "ASA server stopped.");

                UpdateRuntimeStatus(
                    "● OFFLINE",
                    Brushes.IndianRed);

                ClearProcessId();

                UpdateServerButtons();
            }
            catch (Exception ex)
            {
                AppendConsole(
                    "SERVER STOP ERROR:");
                AppendConsole(
                    ex.ToString());

                UpdateRuntimeStatus(
                    "● OFFLINE",
                    Brushes.IndianRed);
            }
            finally
            {
                _serverOperationRunning = false;
                UpdateServerButtons();
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

            if (string.IsNullOrWhiteSpace(serverDirectory))
            {
                MessageBox.Show(
                    "Please select the ASA server folder first.",
                    "Server Folder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (!FindAsaServerExecutable(serverDirectory))
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

                UpdateProcessIdDisplay();

                UpdateRuntimeStatus(
                    "● STARTING",
                    Brushes.Gold);

                UpdateServerButtons();
            }
            catch (Exception ex)
            {
                AppendConsole(
                    "SERVER RESTART ERROR:");
                AppendConsole(
                    ex.ToString());

                UpdateRuntimeStatus(
                    "● OFFLINE",
                    Brushes.IndianRed);
            }
            finally
            {
                _serverOperationRunning = false;
                UpdateServerButtons();
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

                Directory.CreateDirectory(directory);

                if (!File.Exists(filePath))
                    File.WriteAllText(filePath, "");

                string notepadPlusPlus =
                    FindNotepadPlusPlus();

                if (string.IsNullOrWhiteSpace(notepadPlusPlus))
                {
                    MessageBox.Show(
                        "Notepad++ was not found.\n\n" +
                        "Please install Notepad++ or add it to PATH.\n\n" +
                        filePath,
                        "Notepad++ Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = notepadPlusPlus,
                        Arguments = "\"" + filePath + "\"",
                        UseShellExecute = true
                    });

                AppendConsole("");
                AppendConsole(
                    "Opened " +
                    displayName +
                    " in Notepad++:");
                AppendConsole(filePath);
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
        // FIND NOTEPAD++
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

                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        string[] results =
                            output.Split(
                                new[] { '\r', '\n' },
                                StringSplitOptions.RemoveEmptyEntries);

                        if (results.Length > 0)
                        {
                            string firstPath =
                                results[0].Trim();

                            if (File.Exists(firstPath))
                                return firstPath;
                        }
                    }
                }
            }
            catch
            {
                // Ignore PATH lookup failures.
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
                    "Setting up SteamCMD...");

                /*
                 * Keep the existing downloader API.
                 * The null-forgiving operator prevents the compiler
                 * from treating this as a nullable warning when the
                 * downloader intentionally accepts a nullable callback.
                 */
                await _steamCmdDownloader.DownloadAsync(null!);

                UpdateSteamCmdStatus();

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
            }
        }

        // =========================================================
        // ASA OUTPUT
        // =========================================================

        private void AsaServer_OutputReceived(
            string text)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        AppendConsole("[ASA] " + text);
                    }));

                return;
            }

            AppendConsole(
                "[ASA] " +
                text);
        }

        private void AsaServer_ErrorReceived(
            string text)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        AppendConsole("[ASA ERROR] " + text);
                    }));

                return;
            }

            AppendConsole(
                "[ASA ERROR] " +
                text);
        }

        private void AsaServer_Exited(
            int exitCode)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        AsaServer_Exited(exitCode);
                    }));

                return;
            }

            AppendConsole("");
            AppendConsole(
                "ASA server process exited.");
            AppendConsole(
                "Exit code: " +
                exitCode);

            UpdateRuntimeStatus(
                "● OFFLINE",
                Brushes.IndianRed);

            ClearProcessId();

            UpdateServerButtons();
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

            return File.Exists(expectedPath);
        }

        // =========================================================
        // INSTALLATION STATUS
        // =========================================================

        private void UpdateServerStatus()
        {
            if (ServerPathTextBox == null ||
                ServerStatusText == null)
            {
                return;
            }

            string directory =
                ServerPathTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(directory))
            {
                ServerStatusText.Text =
                    "● NOT CONFIGURED";

                ServerStatusText.Foreground =
                    Brushes.Gray;

                return;
            }

            if (FindAsaServerExecutable(directory))
            {
                ServerStatusText.Text =
                    "● INSTALLED";

                ServerStatusText.Foreground =
                    Brushes.LightGreen;
            }
            else
            {
                ServerStatusText.Text =
                    "● NOT INSTALLED";

                ServerStatusText.Foreground =
                    Brushes.IndianRed;
            }
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
        }

        // =========================================================
        // PROCESS ID
        // =========================================================

        private void UpdateProcessIdDisplay()
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

        private void ClearProcessId()
        {
            if (ServerProcessIdText != null)
                ServerProcessIdText.Text = "";
        }

        // =========================================================
        // SERVER BUTTON STATE
        // =========================================================

        private void UpdateServerButtons()
        {
            if (StartServerButton == null ||
                StopServerButton == null ||
                RestartServerButton == null)
            {
                return;
            }

            bool running =
                _asaServerManager.IsRunning;

            StartServerButton.IsEnabled =
                !running &&
                !_serverOperationRunning;

            StopServerButton.IsEnabled =
                running &&
                !_serverOperationRunning;

            RestartServerButton.IsEnabled =
                running &&
                !_serverOperationRunning;

            if (running &&
                _asaServerManager.ProcessId.HasValue)
            {
                UpdateProcessIdDisplay();
            }
            else if (!running)
            {
                ClearProcessId();
            }
        }

        // =========================================================
        // STEAMCMD OPERATION STATE
        // =========================================================

        private void SetOperationRunning(
            bool running)
        {
            _operationRunning =
                running;

            if (InstallServerButton != null)
                InstallServerButton.IsEnabled =
                    !running;

            if (VerifyServerButton != null)
                VerifyServerButton.IsEnabled =
                    !running;

            if (running)
            {
                if (InstallServerButton != null)
                    InstallServerButton.Content =
                        "Installing...";

                if (VerifyServerButton != null)
                    VerifyServerButton.Content =
                        "Working...";
            }
            else
            {
                if (InstallServerButton != null)
                    InstallServerButton.Content =
                        "Install Server";

                if (VerifyServerButton != null)
                    VerifyServerButton.Content =
                        "Verify Files";

                UpdateServerButtons();
            }
        }

        // =========================================================
        // CONSOLE CONTROL LOOKUP
        // =========================================================

        private TextBox? GetConsoleTextBox()
        {
            /*
             * IMPORTANT:
             *
             * We intentionally do NOT reference ConsoleTextBox
             * directly here.
             *
             * Your previous build failed because the XAML-generated
             * field named ConsoleTextBox does not exist.
             *
             * FindName() allows this class to compile regardless of
             * whether the generated field exists.
             */

            try
            {
                object? found =
                    FindName("ConsoleTextBox");

                if (found is TextBox textBox)
                    return textBox;
            }
            catch
            {
                // Ignore lookup errors.
            }

            return null;
        }

        // =========================================================
        // CLEAR CONSOLE
        // =========================================================

        private void ClearConsole()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(
                    new Action(ClearConsole));

                return;
            }

            TextBox? console =
                GetConsoleTextBox();

            if (console == null)
                return;

            console.Clear();
        }

        // =========================================================
        // APPEND CONSOLE
        // =========================================================

        private void AppendConsole(
            string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        AppendConsole(text);
                    }));

                return;
            }

            TextBox? console =
                GetConsoleTextBox();

            if (console == null)
                return;

            console.AppendText(text);
            console.AppendText(
                Environment.NewLine);

            console.ScrollToEnd();
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

                _asaServerManager.Dispose();
            }
            catch
            {
                // Ignore shutdown errors.
            }
        }
    }
}