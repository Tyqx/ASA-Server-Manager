using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using ASAServerManager.Pages;
using ASAServerManager.Server;

using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using PasswordBox = System.Windows.Controls.PasswordBox;
using Orientation = System.Windows.Controls.Orientation;
using MessageBox = System.Windows.MessageBox;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace ASAServerManager
{
    public partial class MainWindow : Window
    {
        private readonly List<ServerPage> _serverPages =
            new List<ServerPage>();

        private ServerMonitorPage? _monitorPage;

        private TabItem? _monitorTab;

        private int _nextServerNumber = 1;

        private bool _closing;

        private bool _initializing;

        // =========================================================
        // SERVER TAB PERSISTENCE
        // =========================================================

        private class SavedServer
        {
            public string ServerId { get; set; } = "";

            public string ServerName { get; set; } = "";

            public string ServerPassword { get; set; } = "";

            public string AdminPassword { get; set; } = "";
        }

        private class MainWindowConfiguration
        {
            public List<SavedServer> Servers { get; set; } =
                new List<SavedServer>();

            public string SelectedServerId { get; set; } = "";
        }

        // =========================================================
        // CONFIGURATION PATH
        // =========================================================

        private string GetMainWindowConfigDirectory()
        {
            string directory =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "ASA Server Manager");

            Directory.CreateDirectory(directory);

            return directory;
        }

        private string GetMainWindowConfigPath()
        {
            return Path.Combine(
                GetMainWindowConfigDirectory(),
                "servers.json");
        }

        // =========================================================
        // CONSTRUCTOR
        // =========================================================

        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        // =========================================================
        // WINDOW LOADED
        // =========================================================

        private void MainWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            if (_closing)
                return;

            _initializing = true;

            try
            {
                LoadServerTabs();

                if (_serverPages.Count == 0)
                {
                    CreateServerTab(
                        selectAfterCreate: false);
                }

                CreateMonitorTab();

                EnsureMonitorIsFirst();

                if (_serverPages.Count > 0)
                {
                    SelectServerTab(
                        _serverPages[0]);
                }
            }
            finally
            {
                _initializing = false;
            }

            UpdateMonitorForSelectedServer();

            /*
             * Do NOT immediately rewrite servers.json here unless
             * necessary.
             *
             * The server pages have just been created and their
             * controls may still be initializing. Rewriting the
             * configuration here could turn valid passwords into
             * empty strings.
             */
        }

        // =========================================================
        // CREATE MONITOR TAB
        // =========================================================

        private void CreateMonitorTab()
        {
            if (_monitorTab != null)
                return;

            if (_serverPages.Count == 0)
                return;

            ServerPage serverPage =
                _serverPages[0];

            _monitorPage =
                new ServerMonitorPage(
                    serverPage.AsaServerManager);

            _monitorTab =
                new TabItem
                {
                    Header =
                        CreateMonitorTabHeader(),

                    Content =
                        _monitorPage,

                    Tag =
                        "MONITOR"
                };

            ServerTabControl.Items.Insert(
                0,
                _monitorTab);
        }

        // =========================================================
        // ENSURE MONITOR IS FIRST
        // =========================================================

        private void EnsureMonitorIsFirst()
        {
            if (_monitorTab == null)
                return;

            int currentIndex =
                ServerTabControl.Items.IndexOf(
                    _monitorTab);

            if (currentIndex < 0)
                return;

            if (currentIndex == 0)
                return;

            ServerTabControl.Items.Remove(
                _monitorTab);

            ServerTabControl.Items.Insert(
                0,
                _monitorTab);
        }

        // =========================================================
        // MONITOR TAB HEADER
        // =========================================================

        private StackPanel CreateMonitorTabHeader()
        {
            var panel =
                new StackPanel
                {
                    Orientation =
                        Orientation.Horizontal
                };

            var icon =
                new TextBlock
                {
                    Text = "●",

                    Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(
                                77,
                                163,
                                255)),

                    VerticalAlignment =
                        VerticalAlignment.Center,

                    Margin =
                        new Thickness(
                            0,
                            0,
                            6,
                            0)
                };

            var title =
                new TextBlock
                {
                    Text = "Monitor",

                    Foreground =
                        Brushes.White,

                    VerticalAlignment =
                        VerticalAlignment.Center
                };

            panel.Children.Add(icon);
            panel.Children.Add(title);

            return panel;
        }

        // =========================================================
        // CREATE NEW SERVER TAB
        // =========================================================

        private void CreateServerTab(
            bool selectAfterCreate = true)
        {
            string serverId =
                Guid.NewGuid().ToString("N");

            string serverName =
                "Server " +
                _nextServerNumber;

            _nextServerNumber++;

            ServerPage serverPage =
                CreateServerTab(
                    serverName,
                    serverId);

            SaveServerTabs();

            if (selectAfterCreate)
            {
                SelectServerTab(
                    serverPage);
            }
        }

        // =========================================================
        // CREATE SERVER TAB
        // =========================================================

        private ServerPage CreateServerTab(
            string serverName,
            string serverId)
        {
            ServerPage serverPage =
                new ServerPage(
                    serverName,
                    serverId);

            _serverPages.Add(
                serverPage);

            TabItem tab =
                new TabItem();

            tab.Tag =
                serverPage;

            tab.Header =
                CreateTabHeader(
                    tab,
                    serverPage);

            tab.Content =
                serverPage;

            if (_monitorTab != null &&
                ServerTabControl.Items.Contains(
                    _monitorTab))
            {
                int monitorIndex =
                    ServerTabControl.Items.IndexOf(
                        _monitorTab);

                ServerTabControl.Items.Insert(
                    monitorIndex + 1,
                    tab);
            }
            else
            {
                ServerTabControl.Items.Add(
                    tab);
            }

            return serverPage;
        }

        // =========================================================
        // TAB HEADER
        // =========================================================

        private StackPanel CreateTabHeader(
            TabItem tab,
            ServerPage serverPage)
        {
            var panel =
                new StackPanel
                {
                    Orientation =
                        Orientation.Horizontal
                };

            var title =
                new TextBlock
                {
                    Text =
                        serverPage.ServerDisplayName,

                    Foreground =
                        Brushes.White,

                    VerticalAlignment =
                        VerticalAlignment.Center,

                    Margin =
                        new Thickness(
                            0,
                            0,
                            10,
                            0)
                };

            var closeButton =
                new Button
                {
                    Content = "×",

                    Width = 20,

                    Height = 20,

                    Padding =
                        new Thickness(0),

                    Margin =
                        new Thickness(0),

                    Background =
                        Brushes.Transparent,

                    Foreground =
                        Brushes.LightGray,

                    BorderThickness =
                        new Thickness(0),

                    FontSize = 15,

                    FontWeight =
                        FontWeights.Bold,

                    Cursor =
                        System.Windows.Input.Cursors.Hand
                };

            closeButton.Click +=
                (sender, e) =>
                {
                    CloseServerTab(
                        tab,
                        serverPage);

                    e.Handled = true;
                };

            panel.Children.Add(title);
            panel.Children.Add(closeButton);

            return panel;
        }

        // =========================================================
        // ADD SERVER BUTTON
        // =========================================================

        private void AddServerButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_closing)
                return;

            CreateServerTab();
        }

        // =========================================================
        // CLOSE SERVER TAB
        // =========================================================

        private void CloseServerTab(
            TabItem tab,
            ServerPage serverPage)
        {
            if (_closing)
                return;

            if (serverPage == null)
                return;

            try
            {
                bool isRunning =
                    serverPage.IsServerRunning;

                if (isRunning)
                {
                    MessageBoxResult result =
                        MessageBox.Show(
                            "This server is currently running.\n\n" +
                            "Closing this tab will stop the server manager for this server.\n\n" +
                            "Do you want to close the tab?",
                            "Server Running",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                        return;
                }

                serverPage.ShutdownServerManager();
            }
            catch
            {
            }

            bool wasSelected =
                ServerTabControl.SelectedItem == tab;

            _serverPages.Remove(
                serverPage);

            ServerTabControl.Items.Remove(
                tab);

            if (_serverPages.Count == 0)
            {
                CreateServerTab(
                    selectAfterCreate: false);
            }

            if (wasSelected)
            {
                SelectServerTab(
                    _serverPages[0]);
            }

            EnsureMonitorIsFirst();

            SaveServerTabs();

            UpdateMonitorForSelectedServer();
        }

        // =========================================================
        // SELECT SERVER TAB
        // =========================================================

        private void SelectServerTab(
            ServerPage serverPage)
        {
            if (serverPage == null)
                return;

            foreach (TabItem tab
                     in ServerTabControl.Items
                         .OfType<TabItem>())
            {
                if (tab.Tag is ServerPage page &&
                    ReferenceEquals(
                        page,
                        serverPage))
                {
                    ServerTabControl.SelectedItem =
                        tab;

                    return;
                }
            }
        }

        // =========================================================
        // UPDATE MONITOR
        // =========================================================

        private void UpdateMonitorForSelectedServer()
        {
            if (_monitorTab == null)
                return;

            ServerPage? selectedServer =
                null;

            if (ServerTabControl.SelectedItem
                is TabItem selectedTab &&
                selectedTab.Tag is ServerPage selectedPage)
            {
                selectedServer =
                    selectedPage;
            }

            if (selectedServer == null)
                return;

            if (_monitorPage != null &&
                ReferenceEquals(
                    _monitorPage.ServerManager,
                    selectedServer.AsaServerManager))
            {
                return;
            }

            ServerMonitorPage? oldMonitor =
                _monitorPage;

            ServerMonitorPage newMonitor =
                new ServerMonitorPage(
                    selectedServer.AsaServerManager);

            _monitorPage =
                newMonitor;

            _monitorTab.Content =
                newMonitor;

            try
            {
                oldMonitor?.Dispose();
            }
            catch
            {
            }
        }

        // =========================================================
        // FIND WPF CONTROL
        //
        // FindName() can be unreliable when multiple UserControls
        // contain controls with the same x:Name.
        //
        // Search the logical tree instead.
        // =========================================================

        private T? FindServerControl<T>(
            ServerPage serverPage,
            string name)
            where T : FrameworkElement
        {
            if (serverPage == null)
                return null;

            try
            {
                T? result =
                    LogicalTreeHelper.FindLogicalNode(
                        serverPage,
                        name) as T;

                if (result != null)
                    return result;
            }
            catch
            {
            }

            try
            {
                return serverPage.FindName(
                    name) as T;
            }
            catch
            {
                return null;
            }
        }

        // =========================================================
        // GET CURRENT SERVER PASSWORD
        // =========================================================

        private string GetServerPassword(
            ServerPage serverPage)
        {
            // First try visible TextBox.
            try
            {
                TextBox? textBox =
                    FindServerControl<TextBox>(
                        serverPage,
                        "ServerPasswordTextBox");

                if (textBox != null &&
                    !string.IsNullOrEmpty(textBox.Text))
                {
                    return textBox.Text;
                }
            }
            catch
            {
            }

            // Then try PasswordBox.
            try
            {
                PasswordBox? passwordBox =
                    FindServerControl<PasswordBox>(
                        serverPage,
                        "ServerPasswordBox");

                if (passwordBox != null &&
                    !string.IsNullOrEmpty(
                        passwordBox.Password))
                {
                    return passwordBox.Password;
                }
            }
            catch
            {
            }

            return "";
        }

        // =========================================================
        // GET CURRENT ADMIN PASSWORD
        // =========================================================

        private string GetAdminPassword(
            ServerPage serverPage)
        {
            // First try visible TextBox.
            try
            {
                TextBox? textBox =
                    FindServerControl<TextBox>(
                        serverPage,
                        "AdminPasswordTextBox");

                if (textBox != null &&
                    !string.IsNullOrEmpty(textBox.Text))
                {
                    return textBox.Text;
                }
            }
            catch
            {
            }

            // Then try PasswordBox.
            try
            {
                PasswordBox? passwordBox =
                    FindServerControl<PasswordBox>(
                        serverPage,
                        "AdminPasswordBox");

                if (passwordBox != null &&
                    !string.IsNullOrEmpty(
                        passwordBox.Password))
                {
                    return passwordBox.Password;
                }
            }
            catch
            {
            }

            return "";
        }

        // =========================================================
        // RESTORE PASSWORDS
        // =========================================================

        private void RestoreServerPasswords(
            ServerPage serverPage,
            SavedServer savedServer)
        {
            if (serverPage == null ||
                savedServer == null)
            {
                return;
            }

            string serverPassword =
                savedServer.ServerPassword ?? "";

            string adminPassword =
                savedServer.AdminPassword ?? "";

            // -----------------------------------------------------
            // SERVER PASSWORD TEXTBOX
            // -----------------------------------------------------

            try
            {
                TextBox? textBox =
                    FindServerControl<TextBox>(
                        serverPage,
                        "ServerPasswordTextBox");

                if (textBox != null)
                {
                    textBox.Text =
                        serverPassword;
                }
            }
            catch
            {
            }

            // -----------------------------------------------------
            // SERVER PASSWORD BOX
            // -----------------------------------------------------

            try
            {
                PasswordBox? passwordBox =
                    FindServerControl<PasswordBox>(
                        serverPage,
                        "ServerPasswordBox");

                if (passwordBox != null)
                {
                    passwordBox.Password =
                        serverPassword;
                }
            }
            catch
            {
            }

            // -----------------------------------------------------
            // ADMIN PASSWORD TEXTBOX
            // -----------------------------------------------------

            try
            {
                TextBox? textBox =
                    FindServerControl<TextBox>(
                        serverPage,
                        "AdminPasswordTextBox");

                if (textBox != null)
                {
                    textBox.Text =
                        adminPassword;
                }
            }
            catch
            {
            }

            // -----------------------------------------------------
            // ADMIN PASSWORD BOX
            // -----------------------------------------------------

            try
            {
                PasswordBox? passwordBox =
                    FindServerControl<PasswordBox>(
                        serverPage,
                        "AdminPasswordBox");

                if (passwordBox != null)
                {
                    passwordBox.Password =
                        adminPassword;
                }
            }
            catch
            {
            }
        }

        // =========================================================
        // READ EXISTING JSON
        //
        // This is extremely important.
        //
        // If a ServerPage temporarily reports an empty password,
        // we keep the password already stored in servers.json
        // instead of overwriting it with "".
        // =========================================================

        private MainWindowConfiguration LoadExistingConfiguration()
        {
            try
            {
                string path =
                    GetMainWindowConfigPath();

                if (!File.Exists(path))
                    return new MainWindowConfiguration();

                string json =
                    File.ReadAllText(path);

                if (string.IsNullOrWhiteSpace(json))
                    return new MainWindowConfiguration();

                MainWindowConfiguration? configuration =
                    JsonSerializer.Deserialize<MainWindowConfiguration>(
                        json);

                return configuration ??
                       new MainWindowConfiguration();
            }
            catch
            {
                return new MainWindowConfiguration();
            }
        }

        // =========================================================
        // SAVE SERVER TABS
        // =========================================================

        private void SaveServerTabs()
        {
            if (_closing && _serverPages.Count == 0)
                return;

            try
            {
                /*
                 * Load the previous configuration first.
                 *
                 * This allows us to preserve passwords even if
                 * WPF temporarily gives us an empty control.
                 */

                MainWindowConfiguration oldConfiguration =
                    LoadExistingConfiguration();

                var configuration =
                    new MainWindowConfiguration();

                foreach (ServerPage serverPage
                         in _serverPages)
                {
                    SavedServer? oldServer =
                        oldConfiguration.Servers
                            .FirstOrDefault(
                                x =>
                                    string.Equals(
                                        x.ServerId,
                                        serverPage.ServerId,
                                        StringComparison.OrdinalIgnoreCase));

                    string currentServerPassword =
                        GetServerPassword(
                            serverPage);

                    string currentAdminPassword =
                        GetAdminPassword(
                            serverPage);

                    // -------------------------------------------------
                    // NEVER replace an existing password with blank.
                    // -------------------------------------------------

                    string serverPassword =
                        !string.IsNullOrEmpty(
                            currentServerPassword)
                            ? currentServerPassword
                            : oldServer?.ServerPassword ?? "";

                    string adminPassword =
                        !string.IsNullOrEmpty(
                            currentAdminPassword)
                            ? currentAdminPassword
                            : oldServer?.AdminPassword ?? "";

                    configuration.Servers.Add(
                        new SavedServer
                        {
                            ServerId =
                                serverPage.ServerId,

                            ServerName =
                                serverPage.ServerDisplayName,

                            ServerPassword =
                                serverPassword,

                            AdminPassword =
                                adminPassword
                        });
                }

                // -----------------------------------------------------
                // Save selected server.
                // -----------------------------------------------------

                if (ServerTabControl.SelectedItem
                    is TabItem selectedTab &&
                    selectedTab.Tag is ServerPage selectedPage)
                {
                    configuration.SelectedServerId =
                        selectedPage.ServerId;
                }
                else
                {
                    configuration.SelectedServerId =
                        oldConfiguration.SelectedServerId;
                }

                string json =
                    JsonSerializer.Serialize(
                        configuration,
                        new JsonSerializerOptions
                        {
                            WriteIndented = true
                        });

                File.WriteAllText(
                    GetMainWindowConfigPath(),
                    json);
            }
            catch
            {
                // Never allow persistence errors to crash the app.
            }
        }

        // =========================================================
        // LOAD SERVER TABS
        // =========================================================

        private void LoadServerTabs()
        {
            try
            {
                string path =
                    GetMainWindowConfigPath();

                if (!File.Exists(path))
                    return;

                string json =
                    File.ReadAllText(path);

                if (string.IsNullOrWhiteSpace(json))
                    return;

                MainWindowConfiguration? configuration =
                    JsonSerializer.Deserialize<MainWindowConfiguration>(
                        json);

                if (configuration == null ||
                    configuration.Servers == null)
                {
                    return;
                }

                string selectedServerId =
                    configuration.SelectedServerId;

                foreach (SavedServer savedServer
                         in configuration.Servers)
                {
                    if (string.IsNullOrWhiteSpace(
                        savedServer.ServerId))
                    {
                        continue;
                    }

                    string serverName =
                        string.IsNullOrWhiteSpace(
                            savedServer.ServerName)
                            ? "ASA Server"
                            : savedServer.ServerName;

                    ServerPage serverPage =
                        CreateServerTab(
                            serverName,
                            savedServer.ServerId);

                    // -------------------------------------------------
                    // Restore passwords for THIS SPECIFIC SERVER ID.
                    // -------------------------------------------------

                    RestoreServerPasswords(
                        serverPage,
                        savedServer);
                }

                _nextServerNumber =
                    _serverPages.Count + 1;

                // -----------------------------------------------------
                // Restore selected server.
                // -----------------------------------------------------

                if (!string.IsNullOrWhiteSpace(
                    selectedServerId))
                {
                    foreach (TabItem tab
                             in ServerTabControl.Items
                                 .OfType<TabItem>())
                    {
                        if (tab.Tag is ServerPage page &&
                            string.Equals(
                                page.ServerId,
                                selectedServerId,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            ServerTabControl.SelectedItem =
                                tab;

                            break;
                        }
                    }
                }
            }
            catch
            {
                // Bad JSON should never stop the application.
            }
        }

        // =========================================================
        // TAB SELECTION
        // =========================================================

        private void ServerTabControl_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (e.Source != ServerTabControl)
                return;

            if (_closing)
                return;

            if (_initializing)
                return;

            if (ServerTabControl.SelectedItem
                is TabItem tab &&
                tab.Tag is ServerPage serverPage)
            {
                UpdateMonitorForSelectedServer();

                SaveServerTabs();

                return;
            }

            if (ServerTabControl.SelectedItem ==
                _monitorTab)
            {
                return;
            }
        }

        // =========================================================
        // WINDOW CLOSING
        // =========================================================

        private void MainWindow_Closing(
            object? sender,
            System.ComponentModel.CancelEventArgs e)
        {
            if (_closing)
                return;

            // -----------------------------------------------------
            // SAVE FIRST.
            //
            // ServerPage controls still exist here, so every
            // server's passwords can be read.
            // -----------------------------------------------------

            try
            {
                SaveServerTabs();
            }
            catch
            {
            }

            _closing = true;

            // -----------------------------------------------------
            // DISPOSE MONITOR
            // -----------------------------------------------------

            try
            {
                _monitorPage?.Dispose();
            }
            catch
            {
            }

            // -----------------------------------------------------
            // SHUT DOWN SERVERS
            // -----------------------------------------------------

            try
            {
                foreach (ServerPage serverPage
                         in _serverPages.ToList())
                {
                    try
                    {
                        serverPage.ShutdownServerManager();
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }
    }
}