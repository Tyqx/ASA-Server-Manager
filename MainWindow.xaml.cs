using System;
using System.Windows;
using ASAServerManager.Pages;

namespace ASAServerManager
{
    public partial class MainWindow : Window
    {
        private ServerPage? _serverPage;

        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            if (_serverPage == null)
            {
                _serverPage = new ServerPage();
            }

            PageContainer.Content = _serverPage;
        }

        private void MainWindow_Closing(
            object? sender,
            System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                _serverPage?.ShutdownServerManager();
            }
            catch
            {
                // Ignore shutdown errors.
            }
        }
    }
}