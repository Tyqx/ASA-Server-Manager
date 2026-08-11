using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ASAServerManager.Pages;

namespace ASAServerManager
{
    public partial class MainWindow : Window
    {
        private readonly List<ServerPage> _serverPages =
            new List<ServerPage>();

        private int _nextServerNumber = 1;

        private bool _closing;

        // =========================================================
        // SERVER TAB PERSISTENCE
        // =========================================================

        private class SavedServer
        {
            public string ServerId { get; set; } = "";
            public string ServerName { get; set; } = "";
        }

        private class MainWindowConfiguration
        {
            public List<SavedServer> Servers { get; set; } =
                new List<SavedServer>();

            public string SelectedServerId { get; set; } = "";
        }

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
            LoadServerTabs();

            if (_serverPages.Count == 0)
            {
                CreateServerTab();
            }
        }

        // =========================================================
        // CREATE NEW SERVER TAB
        // =========================================================

        private void CreateServerTab()
        {
            string serverId =
                Guid.NewGuid().ToString("N");

            string serverName =
                "Server " +
                _nextServerNumber;

            _nextServerNumber++;

            CreateServerTab(
                serverName,
                serverId);

            SaveServerTabs();
        }

        // =========================================================
        // CREATE SERVER TAB FROM SAVED DATA
        // =========================================================

        private void CreateServerTab(
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

            ServerTabControl.Items.Add(
                tab);
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
                        System.Windows.Media.Brushes.White,

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
                        System.Windows.Media.Brushes.Transparent,

                    Foreground =
                        System.Windows.Media.Brushes.LightGray,

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

            _serverPages.Remove(
                serverPage);

            ServerTabControl.Items.Remove(
                tab);

            SaveServerTabs();

            if (_serverPages.Count == 0)
            {
                CreateServerTab();
            }
        }

        // =========================================================
        // SAVE SERVER TABS
        // =========================================================

        private void SaveServerTabs()
        {
            try
            {
                var configuration =
                    new MainWindowConfiguration();

                foreach (ServerPage serverPage
                         in _serverPages)
                {
                    configuration.Servers.Add(
                        new SavedServer
                        {
                            ServerId =
                                serverPage.ServerId,

                            ServerName =
                                serverPage.ServerDisplayName
                        });
                }

                if (ServerTabControl.SelectedItem
                    is TabItem selectedTab &&
                    selectedTab.Tag is ServerPage selectedPage)
                {
                    configuration.SelectedServerId =
                        selectedPage.ServerId;
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
                // Do not prevent the application from closing
                // because of a persistence error.
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

                if (configuration == null)
                    return;

                if (configuration.Servers == null)
                    return;

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

                    CreateServerTab(
                        serverName,
                        savedServer.ServerId);
                }

                // =================================================
                // UPDATE NEXT SERVER NUMBER
                // =================================================

                _nextServerNumber =
                    _serverPages.Count + 1;

                // =================================================
                // RESTORE SELECTED TAB
                // =================================================

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

                // If there was no saved selection,
                // select the first tab.
                if (ServerTabControl.SelectedItem == null &&
                    ServerTabControl.Items.Count > 0)
                {
                    ServerTabControl.SelectedIndex = 0;
                }
            }
            catch
            {
                // If the saved tab file is invalid,
                // the application can still start normally.
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

            if (ServerTabControl.SelectedItem
                is TabItem tab &&
                tab.Tag is ServerPage serverPage)
            {
                SaveServerTabs();
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

            _closing = true;

            // Save the tab list BEFORE shutting
            // down the individual ServerPages.
            SaveServerTabs();

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