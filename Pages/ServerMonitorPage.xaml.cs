using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ASAServerManager.Backup;

using ASAServerManager.Server;

using UserControl = System.Windows.Controls.UserControl;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;

namespace ASAServerManager.Pages
{
    public partial class ServerMonitorPage : UserControl
    {
        private readonly AsaServerManager _serverManager;
        private readonly ServerMonitor _monitor;

        private bool _disposed;

        // =========================================================
        // GRAPH SETTINGS
        // =========================================================

        private const int MaxSamples = 30;

        private readonly List<double> _cpuHistory = new();
        private readonly List<double> _memoryHistory = new();

        // =========================================================
        // CPU BASELINE
        // =========================================================

        private ulong _lastIdleTime;
        private ulong _lastKernelTime;
        private ulong _lastUserTime;

        private bool _hasCpuBaseline;

        // =========================================================
        // NETWORK BASELINE
        // =========================================================

        private long _lastNetworkReceived;
        private long _lastNetworkSent;

        private DateTime _lastNetworkCheck;

        private bool _hasNetworkBaseline;

        // =========================================================
        // CONSTRUCTOR
        // =========================================================

        public ServerMonitorPage(
            AsaServerManager serverManager)
        {
            InitializeComponent();

            _serverManager =
                serverManager ??
                throw new ArgumentNullException(
                    nameof(serverManager));

            _monitor =
                new ServerMonitor(
                    _serverManager);

            _monitor.Updated +=
                Monitor_Updated;

            _monitor.ServerCrashed +=
                Monitor_ServerCrashed;

            _monitor.ServerExited +=
                Monitor_ServerExited;

            ProcessorCountText.Text =
                Environment.ProcessorCount.ToString();

            MonitoringIntervalText.Text =
                "2 seconds";

            Loaded +=
                ServerMonitorPage_Loaded;

            Unloaded +=
                ServerMonitorPage_Unloaded;
        }

        // =========================================================
        // LOADED
        // =========================================================

        private void ServerMonitorPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            if (_disposed)
                return;

            ResetGraphHistory();
            ResetNetworkBaseline();

            _monitor.Start();

            UpdateDisplay();
        }

        // =========================================================
        // UNLOADED
        // =========================================================

        private void ServerMonitorPage_Unloaded(
            object sender,
            RoutedEventArgs e)
        {
            if (_disposed)
                return;

            _monitor.Stop();
        }

        // =========================================================
        // MONITOR UPDATED
        // =========================================================

        private void Monitor_Updated(
            ServerMonitor monitor)
        {
            if (_disposed)
                return;

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(
                    new Action(UpdateDisplay));

                return;
            }

            UpdateDisplay();
        }

        // =========================================================
        // UPDATE DISPLAY
        // =========================================================

        private void UpdateDisplay()
        {
            if (_disposed)
                return;

            // =====================================================
            // SERVER STATUS
            // =====================================================

            bool running =
                _monitor.IsRunning;

            if (running)
            {
                ServerStatusText.Text =
                    "● ONLINE";

                ServerStatusText.Foreground =
                    Brushes.LightGreen;

                ServerNameText.Text =
                    "ASA SERVER";

                HealthText.Text =
                    "Server is running normally.";
            }
            else
            {
                ServerStatusText.Text =
                    "● OFFLINE";

                ServerStatusText.Foreground =
                    Brushes.IndianRed;

                HealthText.Text =
                    "Server is offline.";
            }

            // =====================================================
            // SERVER CPU
            // =====================================================

            double serverCpu =
                Math.Clamp(
                    _monitor.CpuUsage,
                    0,
                    100);

            ServerCpuValueText.Text =
                $"{serverCpu:0.0}%";

            // =====================================================
            // SERVER MEMORY
            // =====================================================

            ServerMemoryValueText.Text =
                $"{_monitor.MemoryGB:0.0} GB";

            // =====================================================
            // UPTIME
            // =====================================================

            UptimeValueText.Text =
                FormatUptime(
                    _monitor.Uptime);

            // =====================================================
            // PID
            // =====================================================

            ProcessIdValueText.Text =
                _monitor.ProcessId?.ToString()
                ?? "—";

            // =====================================================
            // SYSTEM CPU
            // =====================================================

            double systemCpu =
                GetSystemCpuUsage();

            AddHistory(
                _cpuHistory,
                systemCpu);

            SystemCpuCurrentText.Text =
                $"{systemCpu:0.0}%";

            CpuCurrentStatText.Text =
                $"{systemCpu:0.0}%";

            CpuAverageText.Text =
                $"{GetAverage(_cpuHistory):0.0}%";

            CpuPeakText.Text =
                $"{GetPeak(_cpuHistory):0.0}%";

            // =====================================================
            // SYSTEM MEMORY
            // =====================================================

            SystemMemoryInfo memory =
                GetSystemMemory();

            double memoryPercent =
                memory.UsedPercent;

            AddHistory(
                _memoryHistory,
                memoryPercent);

            SystemMemoryCurrentText.Text =
                $"{memoryPercent:0.0}%";

            MemoryCurrentStatText.Text =
                $"{memoryPercent:0.0}%";

            MemoryAverageText.Text =
                $"{GetAverage(_memoryHistory):0.0}%";

            MemoryPeakText.Text =
                $"{GetPeak(_memoryHistory):0.0}%";

            SystemMemoryGBText.Text =
                $"{memory.UsedGB:0.0} / " +
                $"{memory.TotalGB:0.0} GB";

            SystemMemoryDescriptionText.Text =
                $"{memory.UsedGB:0.0} GB used of " +
                $"{memory.TotalGB:0.0} GB";

            TotalMemoryText.Text =
                $"{memory.TotalGB:0.0} GB";

            SystemMemoryInfoText.Text =
                $"{memory.UsedGB:0.0} GB used " +
                $"({memoryPercent:0.0}%)";

            // =====================================================
            // DISK
            // =====================================================

            UpdateDiskInformation();

            // =====================================================
            // NETWORK
            // =====================================================

            UpdateNetworkInformation();

            // =====================================================
            // GRAPHS
            // =====================================================

            DrawGraph(
                CpuGraphCanvas,
                CpuGraphLine,
                _cpuHistory);

            DrawGraph(
                MemoryGraphCanvas,
                MemoryGraphLine,
                _memoryHistory);
        }

        // =========================================================
        // DISK INFORMATION
        // =========================================================

        private void UpdateDiskInformation()
        {
            try
            {
                string? serverPath =
                    GetServerExecutablePath();

                if (string.IsNullOrWhiteSpace(serverPath))
                {
                    ClearDiskInformation();
                    return;
                }

                string? driveRoot =
    System.IO.Path.GetPathRoot(serverPath);

                if (string.IsNullOrWhiteSpace(driveRoot))
                {
                    ClearDiskInformation();
                    return;
                }

                DriveInfo drive =
                    new DriveInfo(driveRoot);

                if (!drive.IsReady)
                {
                    ClearDiskInformation();
                    return;
                }

                // -------------------------------------------------
                // ONLY FREE SPACE
                // -------------------------------------------------

                long freeBytes =
                    drive.AvailableFreeSpace;

                long totalBytes =
                    drive.TotalSize;

                if (totalBytes <= 0)
                {
                    ClearDiskInformation();
                    return;
                }

                double freeGB =
                    BytesToGB(freeBytes);

                double totalGB =
                    BytesToGB(totalBytes);

                double freePercent =
                    (
                        (double)freeBytes /
                        totalBytes
                    ) * 100.0;

                freePercent =
                    Math.Clamp(
                        freePercent,
                        0,
                        100);

                // -------------------------------------------------
                // DISPLAY FREE SPACE
                // -------------------------------------------------

                DiskUsedText.Text =
                    $"{freeGB:0.0} GB free";

                DiskTotalText.Text =
                    $"{totalGB:0.0} GB total";

                DiskPercentText.Text =
                    $"{freePercent:0.0}% free";

                double usedPercent =
    100.0 - freePercent;

DiskProgressBar.Minimum = 0;
DiskProgressBar.Maximum = 100;
DiskProgressBar.Value =
    Math.Clamp(
        usedPercent,
        0,
        100);

                SystemDriveText.Text =
                    driveRoot;
            }
            catch
            {
                ClearDiskInformation();
            }
        }

        // =========================================================
        // GET ASA SERVER EXECUTABLE
        // =========================================================

        private string? GetServerExecutablePath()
        {
            try
            {
                if (_serverManager.ServerProcess == null)
                    return null;

                if (_serverManager.ServerProcess.HasExited)
                    return null;

                return
                    _serverManager
                        .ServerProcess
                        .MainModule?
                        .FileName;
            }
            catch
            {
                return null;
            }
        }

        // =========================================================
        // CLEAR DISK
        // =========================================================

        private void ClearDiskInformation()
        {
            DiskUsedText.Text =
                "—";

            DiskTotalText.Text =
                "—";

            DiskPercentText.Text =
                "—";

            DiskProgressBar.Value =
                0;

            SystemDriveText.Text =
                "—";
        }

        // =========================================================
        // BYTES -> GB
        // =========================================================

        private static double BytesToGB(
            long bytes)
        {
            return
                bytes /
                1024d /
                1024d /
                1024d;
        }

        // =========================================================
        // NETWORK INFORMATION
        // =========================================================

        private void UpdateNetworkInformation()
        {
            try
            {
                long totalReceived = 0;
                long totalSent = 0;

                foreach (
                    NetworkInterface networkInterface
                    in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (networkInterface.OperationalStatus !=
                        OperationalStatus.Up)
                    {
                        continue;
                    }

                    if (networkInterface.NetworkInterfaceType ==
                        NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    if (networkInterface.NetworkInterfaceType ==
                        NetworkInterfaceType.Tunnel)
                    {
                        continue;
                    }

                    IPv4InterfaceStatistics statistics;

                    try
                    {
                        statistics =
                            networkInterface
                                .GetIPv4Statistics();
                    }
                    catch
                    {
                        continue;
                    }

                    totalReceived +=
                        statistics.BytesReceived;

                    totalSent +=
                        statistics.BytesSent;
                }

                double receivedMB =
                    totalReceived /
                    1024d /
                    1024d;

                double sentMB =
                    totalSent /
                    1024d /
                    1024d;

                double totalMB =
                    receivedMB +
                    sentMB;

                double receivedRateMbps = 0;
                double sentRateMbps = 0;

                DateTime now =
                    DateTime.UtcNow;

                if (_hasNetworkBaseline)
                {
                    double elapsed =
                        (
                            now -
                            _lastNetworkCheck
                        ).TotalSeconds;

                    if (elapsed > 0)
                    {
                        long receivedDelta =
                            totalReceived -
                            _lastNetworkReceived;

                        long sentDelta =
                            totalSent -
                            _lastNetworkSent;

                        if (receivedDelta >= 0)
                        {
                            receivedRateMbps =
                                (
                                    receivedDelta *
                                    8d /
                                    1024d /
                                    1024d
                                ) /
                                elapsed;
                        }

                        if (sentDelta >= 0)
                        {
                            sentRateMbps =
                                (
                                    sentDelta *
                                    8d /
                                    1024d /
                                    1024d
                                ) /
                                elapsed;
                        }
                    }
                }

                _lastNetworkReceived =
                    totalReceived;

                _lastNetworkSent =
                    totalSent;

                _lastNetworkCheck =
                    now;

                _hasNetworkBaseline =
                    true;

                NetworkDownloadText.Text =
                    FormatNetworkRate(
                        receivedRateMbps);

                NetworkUploadText.Text =
                    FormatNetworkRate(
                        sentRateMbps);

                NetworkDownloadTotalText.Text =
                    $"{receivedMB:0.0} MB total";

                NetworkUploadTotalText.Text =
                    $"{sentMB:0.0} MB total";
            }
            catch
            {
                // Network monitoring should never crash the page.
            }
        }

        // =========================================================
        // NETWORK RATE
        // =========================================================

        private static string FormatNetworkRate(
            double mbps)
        {
            if (mbps < 0)
                mbps = 0;

            if (mbps < 1)
            {
                return
                    $"{mbps * 1024:0} Kbps";
            }

            return
                $"{mbps:0.0} Mbps";
        }

        // =========================================================
        // RESET NETWORK
        // =========================================================

        private void ResetNetworkBaseline()
        {
            _lastNetworkReceived = 0;
            _lastNetworkSent = 0;

            _lastNetworkCheck =
                default;

            _hasNetworkBaseline =
                false;
        }

        // =========================================================
        // GRAPH HISTORY
        // =========================================================

        private static void AddHistory(
            List<double> history,
            double value)
        {
            history.Add(
                Math.Clamp(
                    value,
                    0,
                    100));

            while (
                history.Count >
                MaxSamples)
            {
                history.RemoveAt(0);
            }
        }

        // =========================================================
        // DRAW GRAPH
        // =========================================================

        private static void DrawGraph(
            Canvas canvas,
            Polyline line,
            List<double> history)
        {
            if (canvas == null ||
                line == null)
            {
                return;
            }

            if (canvas.ActualWidth <= 0 ||
                canvas.ActualHeight <= 0)
            {
                return;
            }

            line.Points.Clear();

            if (history.Count == 0)
                return;

            double width =
                canvas.ActualWidth;

            double height =
                canvas.ActualHeight;

            double xStep =
                history.Count > 1
                    ? width /
                      (MaxSamples - 1)
                    : width;

            int startOffset =
                MaxSamples -
                history.Count;

            for (
                int i = 0;
                i < history.Count;
                i++)
            {
                double value =
                    Math.Clamp(
                        history[i],
                        0,
                        100);

                double x =
                    (startOffset + i) *
                    xStep;

                double y =
                    height -
                    (
                        value /
                        100d *
                        height
                    );

                line.Points.Add(
                    new Point(
                        x,
                        y));
            }
        }

        // =========================================================
        // GRAPH RESIZE
        // =========================================================

        private void GraphCanvas_SizeChanged(
            object sender,
            SizeChangedEventArgs e)
        {
            if (_disposed)
                return;

            DrawGraph(
                CpuGraphCanvas,
                CpuGraphLine,
                _cpuHistory);

            DrawGraph(
                MemoryGraphCanvas,
                MemoryGraphLine,
                _memoryHistory);
        }

        // =========================================================
        // RESET GRAPH HISTORY
        // =========================================================

        private void ResetGraphHistory()
        {
            _cpuHistory.Clear();
            _memoryHistory.Clear();

            _hasCpuBaseline = false;

            _lastIdleTime = 0;
            _lastKernelTime = 0;
            _lastUserTime = 0;

            if (CpuGraphLine != null)
                CpuGraphLine.Points.Clear();

            if (MemoryGraphLine != null)
                MemoryGraphLine.Points.Clear();
        }

        // =========================================================
        // SYSTEM CPU
        // =========================================================

        private double GetSystemCpuUsage()
        {
            if (!GetSystemTimes(
                    out FileTime idleTime,
                    out FileTime kernelTime,
                    out FileTime userTime))
            {
                return 0;
            }

            ulong idle =
                FileTimeToUInt64(
                    idleTime);

            ulong kernel =
                FileTimeToUInt64(
                    kernelTime);

            ulong user =
                FileTimeToUInt64(
                    userTime);

            if (!_hasCpuBaseline)
            {
                _lastIdleTime = idle;
                _lastKernelTime = kernel;
                _lastUserTime = user;

                _hasCpuBaseline = true;

                return 0;
            }

            ulong idleDelta =
                idle -
                _lastIdleTime;

            ulong kernelDelta =
                kernel -
                _lastKernelTime;

            ulong userDelta =
                user -
                _lastUserTime;

            _lastIdleTime = idle;
            _lastKernelTime = kernel;
            _lastUserTime = user;

            ulong totalDelta =
                kernelDelta +
                userDelta;

            if (totalDelta == 0)
                return 0;

            double usage =
                (
                    1.0 -
                    (
                        (double)idleDelta /
                        totalDelta
                    )
                ) *
                100.0;

            return Math.Clamp(
                usage,
                0,
                100);
        }

        // =========================================================
        // SYSTEM MEMORY
        // =========================================================

        private static SystemMemoryInfo GetSystemMemory()
        {
            MemoryStatus status =
                new();

            status.Length =
                (uint)Marshal.SizeOf<MemoryStatus>();

            if (!GlobalMemoryStatusEx(
                    ref status))
            {
                return new SystemMemoryInfo();
            }

            double totalBytes =
                status.TotalPhysicalMemory;

            double availableBytes =
                status.AvailablePhysicalMemory;

            if (totalBytes <= 0)
                return new SystemMemoryInfo();

            double usedBytes =
                totalBytes -
                availableBytes;

            if (usedBytes < 0)
                usedBytes = 0;

            double totalGB =
                totalBytes /
                1024d /
                1024d /
                1024d;

            double usedGB =
                usedBytes /
                1024d /
                1024d /
                1024d;

            double availableGB =
                availableBytes /
                1024d /
                1024d /
                1024d;

            double usedPercent =
                (
                    usedBytes /
                    totalBytes
                ) *
                100d;

            return new SystemMemoryInfo
            {
                TotalGB =
                    totalGB,

                UsedGB =
                    usedGB,

                AvailableGB =
                    availableGB,

                UsedPercent =
                    Math.Clamp(
                        usedPercent,
                        0,
                        100)
            };
        }

        // =========================================================
        // AVERAGE
        // =========================================================

        private static double GetAverage(
            List<double> values)
        {
            if (values.Count == 0)
                return 0;

            double total = 0;

            foreach (double value in values)
                total += value;

            return
                total /
                values.Count;
        }

        // =========================================================
        // PEAK
        // =========================================================

        private static double GetPeak(
            List<double> values)
        {
            if (values.Count == 0)
                return 0;

            double peak = 0;

            foreach (double value in values)
            {
                if (value > peak)
                    peak = value;
            }

            return peak;
        }

        // =========================================================
        // FORMAT UPTIME
        // =========================================================

        private static string FormatUptime(
            TimeSpan uptime)
        {
            if (uptime.TotalDays >= 1)
            {
                return
                    $"{(int)uptime.TotalDays}d " +
                    $"{uptime.Hours:00}:" +
                    $"{uptime.Minutes:00}:" +
                    $"{uptime.Seconds:00}";
            }

            return
                $"{uptime.Hours:00}:" +
                $"{uptime.Minutes:00}:" +
                $"{uptime.Seconds:00}";
        }

        // =========================================================
        // SERVER EXITED
        // =========================================================

        private void Monitor_ServerExited(
            int exitCode)
        {
            if (_disposed)
                return;

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(
                    new Action(
                        () =>
                            Monitor_ServerExited(
                                exitCode)));

                return;
            }

            UpdateDisplay();

            if (exitCode == 0)
            {
                HealthText.Text =
                    "Server stopped normally.";
            }
            else
            {
                HealthText.Text =
                    $"Server exited unexpectedly. " +
                    $"Exit code: {exitCode}";
            }
        }

        // =========================================================
        // SERVER CRASHED
        // =========================================================

        private void Monitor_ServerCrashed()
        {
            if (_disposed)
                return;

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(
                    new Action(
                        Monitor_ServerCrashed));

                return;
            }

            ServerStatusText.Text =
                "● CRASHED";

            ServerStatusText.Foreground =
                Brushes.OrangeRed;

            HealthText.Text =
                "The server process exited unexpectedly.";
        }

        // =========================================================
        // DISPOSE
        // =========================================================

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _monitor.Updated -=
                Monitor_Updated;

            _monitor.ServerCrashed -=
                Monitor_ServerCrashed;

            _monitor.ServerExited -=
                Monitor_ServerExited;

            _monitor.Dispose();
        }

        // =========================================================
        // WINDOWS API
        // =========================================================

        [DllImport(
            "kernel32.dll",
            SetLastError = true)]
        private static extern bool GetSystemTimes(
            out FileTime idleTime,
            out FileTime kernelTime,
            out FileTime userTime);

        [DllImport(
            "kernel32.dll",
            SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(
            ref MemoryStatus lpBuffer);

        // =========================================================
        // FILE TIME
        // =========================================================

        [StructLayout(
            LayoutKind.Sequential)]
        private struct FileTime
        {
            public uint LowDateTime;
            public uint HighDateTime;
        }

        private static ulong FileTimeToUInt64(
            FileTime fileTime)
        {
            return
                ((ulong)fileTime.HighDateTime << 32) |
                fileTime.LowDateTime;
        }

        // =========================================================
        // MEMORY STATUS
        // =========================================================

        [StructLayout(
            LayoutKind.Sequential)]
        private struct MemoryStatus
        {
            public uint Length;
            public uint MemoryLoad;

            public ulong TotalPhysicalMemory;
            public ulong AvailablePhysicalMemory;

            public ulong TotalPageFile;
            public ulong AvailablePageFile;

            public ulong TotalVirtual;
            public ulong AvailableVirtual;

            public ulong AvailableExtendedVirtual;
        }

        // =========================================================
        // MEMORY INFO
        // =========================================================

        private struct SystemMemoryInfo
        {
            public double TotalGB;
            public double UsedGB;
            public double AvailableGB;
            public double UsedPercent;
        }
    }
}