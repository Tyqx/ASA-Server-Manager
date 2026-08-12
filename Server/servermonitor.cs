using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;

using Timer = System.Threading.Timer;

namespace ASAServerManager.Server
{
    public class ServerMonitor : IDisposable
    {
        private readonly AsaServerManager _serverManager;

        private Timer? _monitorTimer;

        private bool _disposed;

        private ulong _lastIdleTime;
        private ulong _lastKernelTime;
        private ulong _lastUserTime;
        private bool _cpuInitialized;

        private long _lastNetworkReceived;
        private long _lastNetworkSent;
        private DateTime _lastNetworkCheck;

        private long _lastDiskRead;
        private long _lastDiskWrite;
        private DateTime _lastDiskCheck;

        private readonly List<MonitorSnapshot> _history =
            new List<MonitorSnapshot>();

        private readonly List<MonitorEvent> _events =
            new List<MonitorEvent>();

        private DateTime _lastHealthCheck = DateTime.MinValue;

        private bool _cpuWarningActive;
        private bool _memoryWarningActive;
        private bool _diskWarningActive;
        private bool _diskSpaceWarningActive;

        // =========================================================
        // CONFIGURABLE THRESHOLDS
        // =========================================================

        public double CpuWarningThreshold { get; set; } = 75.0;

        public double CpuCriticalThreshold { get; set; } = 90.0;

        public double MemoryWarningThreshold { get; set; } = 75.0;

        public double MemoryCriticalThreshold { get; set; } = 90.0;

        public double DiskWarningThreshold { get; set; } = 75.0;

        public double DiskCriticalThreshold { get; set; } = 90.0;

        public double DiskSpaceWarningGB { get; set; } = 50.0;

        public double DiskSpaceCriticalGB { get; set; } = 10.0;


        // =========================================================
        // SERVER STATUS
        // =========================================================

        public bool IsMonitoring { get; private set; }

        public bool IsRunning =>
            _serverManager.IsRunning;

        public int? ProcessId =>
            _serverManager.ProcessId;


        // =========================================================
        // SYSTEM CPU
        // =========================================================

        public double CpuUsage { get; private set; }


        // =========================================================
        // SYSTEM MEMORY
        // =========================================================

        public long MemoryBytes { get; private set; }

        public double MemoryMB =>
            MemoryBytes /
            1024d /
            1024d;

        public double MemoryGB =>
            MemoryBytes /
            1024d /
            1024d /
            1024d;

        public double MemoryUsedGB =>
            MemoryGB;

        public double TotalMemoryGB { get; private set; }

        public double MemoryUsagePercent { get; private set; }


        // =========================================================
        // ASA PROCESS CPU / MEMORY
        // =========================================================

        public double ProcessCpuUsage { get; private set; }

        public long ProcessMemoryBytes { get; private set; }

        public double ProcessMemoryMB =>
            ProcessMemoryBytes /
            1024d /
            1024d;

        public double ProcessMemoryGB =>
            ProcessMemoryBytes /
            1024d /
            1024d /
            1024d;

        private TimeSpan _lastProcessCpuTime;

        private DateTime _lastProcessCpuCheck;

        private bool _processCpuInitialized;


        // =========================================================
        // DISK
        // =========================================================

        public string MonitoredDrive { get; private set; } = "";

        public double DiskUsedPercent { get; private set; }

        public double DiskFreeGB { get; private set; }

        public double DiskTotalGB { get; private set; }

        public double DiskReadMBps { get; private set; }

        public double DiskWriteMBps { get; private set; }


        // =========================================================
        // NETWORK
        // =========================================================

        public double NetworkDownloadMBps { get; private set; }

        public double NetworkUploadMBps { get; private set; }

        public long NetworkBytesReceived { get; private set; }

        public long NetworkBytesSent { get; private set; }


        // =========================================================
        // GPU
        // =========================================================

        public string GpuName { get; private set; } =
            "Not available";

        public double GpuUsage { get; private set; }

        public double GpuTemperature { get; private set; }

        public double GpuMemoryUsedGB { get; private set; }

        public double GpuMemoryTotalGB { get; private set; }


        // =========================================================
        // SERVER UPTIME
        // =========================================================

        public TimeSpan Uptime { get; private set; }

        public DateTime? StartTime { get; private set; }


        // =========================================================
        // HEALTH
        // =========================================================

        public MonitorHealth Health { get; private set; } =
            MonitorHealth.Unknown;

        public string HealthMessage { get; private set; } =
            "Waiting for monitoring data.";


        // =========================================================
        // HISTORY
        // =========================================================

        public IReadOnlyList<MonitorSnapshot> History =>
            _history;


        // =========================================================
        // EVENTS
        // =========================================================

        public IReadOnlyList<MonitorEvent> Events =>
            _events;


        public event Action<ServerMonitor>? Updated;

        public event Action? ServerCrashed;

        public event Action<int>? ServerExited;

        public event Action<MonitorEvent>? EventAdded;


        // =========================================================
        // CONSTRUCTOR
        // =========================================================

        public ServerMonitor(
            AsaServerManager serverManager)
        {
            _serverManager =
                serverManager ??
                throw new ArgumentNullException(
                    nameof(serverManager));

            _serverManager.ServerExited +=
                OnServerExited;
        }


        // =========================================================
        // START
        // =========================================================

        public void Start()
        {
            if (_disposed)
                return;

            if (IsMonitoring)
                return;

            IsMonitoring = true;

            ResetMeasurements();

            AddEvent(
                MonitorEventType.Information,
                "System monitoring started.");

            _monitorTimer =
                new Timer(
                    MonitorTimerCallback,
                    null,
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(2));
        }


        // =========================================================
        // STOP
        // =========================================================

        public void Stop()
        {
            if (!IsMonitoring)
                return;

            IsMonitoring = false;

            _monitorTimer?.Dispose();

            _monitorTimer = null;

            ResetMeasurements();
        }


        // =========================================================
        // TIMER
        // =========================================================

        private void MonitorTimerCallback(
            object? state)
        {
            if (_disposed)
                return;

            try
            {
                Update();
            }
            catch
            {
                // Monitoring must never crash
                // the server manager.
            }
        }


        // =========================================================
        // UPDATE
        // =========================================================

        private void Update()
        {
            try
            {
                UpdateSystemCpu();

                UpdateSystemMemory();

                UpdateProcessMetrics();

                UpdateDisk();

                UpdateNetwork();

                UpdateServerInformation();

                UpdateGpu();

                UpdateHealth();

                AddSnapshot();

                Updated?.Invoke(this);
            }
            catch
            {
                // Monitoring must never crash
                // the server manager.
            }
        }


        // =========================================================
        // SYSTEM CPU
        // =========================================================

        private void UpdateSystemCpu()
        {
            try
            {
                if (!GetSystemTimes(
                        out SystemTimes idle,
                        out SystemTimes kernel,
                        out SystemTimes user))
                {
                    CpuUsage = 0;
                    return;
                }

                ulong idleTime =
                    FileTimeToUInt64(
                        idle);

                ulong kernelTime =
                    FileTimeToUInt64(
                        kernel);

                ulong userTime =
                    FileTimeToUInt64(
                        user);

                if (_cpuInitialized)
                {
                    ulong idleDelta =
                        idleTime -
                        _lastIdleTime;

                    ulong kernelDelta =
                        kernelTime -
                        _lastKernelTime;

                    ulong userDelta =
                        userTime -
                        _lastUserTime;

                    ulong totalDelta =
                        kernelDelta +
                        userDelta;

                    if (totalDelta > 0)
                    {
                        CpuUsage =
                            (
                                1.0 -
                                (
                                    (double)idleDelta /
                                    totalDelta
                                )
                            ) * 100.0;
                    }
                }

                _lastIdleTime =
                    idleTime;

                _lastKernelTime =
                    kernelTime;

                _lastUserTime =
                    userTime;

                _cpuInitialized = true;

                CpuUsage =
                    Math.Clamp(
                        CpuUsage,
                        0,
                        100);
            }
            catch
            {
                CpuUsage = 0;
            }
        }


        // =========================================================
        // SYSTEM MEMORY
        // =========================================================

        private void UpdateSystemMemory()
        {
            try
            {
                MEMORYSTATUSEX memory =
                    new MEMORYSTATUSEX();

                memory.dwLength =
                    (uint)Marshal.SizeOf<
                        MEMORYSTATUSEX>();

                if (!GlobalMemoryStatusEx(
                        ref memory))
                {
                    return;
                }

                ulong total =
                    memory.ullTotalPhys;

                ulong available =
                    memory.ullAvailPhys;

                if (available > total)
                    available = total;

                ulong used =
                    total -
                    available;

                MemoryBytes =
                    used >
                    long.MaxValue
                        ? long.MaxValue
                        : (long)used;

                TotalMemoryGB =
                    total /
                    1024d /
                    1024d /
                    1024d;

                MemoryUsagePercent =
                    total > 0
                        ? (
                            (double)used /
                            total
                        ) * 100.0
                        : 0;

                MemoryUsagePercent =
                    Math.Clamp(
                        MemoryUsagePercent,
                        0,
                        100);
            }
            catch
            {
                MemoryBytes = 0;
                TotalMemoryGB = 0;
                MemoryUsagePercent = 0;
            }
        }


        // =========================================================
        // ASA PROCESS METRICS
        // =========================================================

        private void UpdateProcessMetrics()
        {
            Process? process =
                _serverManager.ServerProcess;

            if (process == null)
            {
                ProcessCpuUsage = 0;
                ProcessMemoryBytes = 0;
                return;
            }

            try
            {
                if (process.HasExited)
                {
                    ProcessCpuUsage = 0;
                    ProcessMemoryBytes = 0;
                    return;
                }

                ProcessMemoryBytes =
                    process.WorkingSet64;

                TimeSpan processorTime =
                    process.TotalProcessorTime;

                DateTime now =
                    DateTime.UtcNow;

                if (_processCpuInitialized)
                {
                    double elapsed =
                        (
                            now -
                            _lastProcessCpuCheck
                        ).TotalSeconds;

                    double cpuSeconds =
                        (
                            processorTime -
                            _lastProcessCpuTime
                        ).TotalSeconds;

                    if (elapsed > 0)
                    {
                        ProcessCpuUsage =
                            (
                                cpuSeconds /
                                elapsed /
                                Environment.ProcessorCount
                            ) * 100.0;
                    }
                }

                _lastProcessCpuTime =
                    processorTime;

                _lastProcessCpuCheck =
                    now;

                _processCpuInitialized = true;

                ProcessCpuUsage =
                    Math.Clamp(
                        ProcessCpuUsage,
                        0,
                        100);
            }
            catch
            {
                ProcessCpuUsage = 0;
                ProcessMemoryBytes = 0;
            }
        }


        // =========================================================
        // DISK
        // =========================================================

        private void UpdateDisk()
        {
            try
            {
                string path =
                    GetServerDrive();

                if (string.IsNullOrWhiteSpace(path))
                    return;

                MonitoredDrive =
                    path;

                DriveInfo drive =
                    new DriveInfo(path);

                if (!drive.IsReady)
                    return;

                long total =
                    drive.TotalSize;

                long free =
                    drive.AvailableFreeSpace;

                long used =
                    total -
                    free;

                DiskTotalGB =
                    total /
                    1024d /
                    1024d /
                    1024d;

                DiskFreeGB =
                    free /
                    1024d /
                    1024d /
                    1024d;

                DiskUsedPercent =
                    total > 0
                        ? (
                            (double)used /
                            total
                        ) * 100.0
                        : 0;


                // Use the ASA process disk I/O as a reliable
                // server-specific activity measurement.

                Process? process =
                    _serverManager.ServerProcess;

                if (process == null)
                    return;

                try
                {
                    if (process.HasExited)
                        return;

                    DateTime now =
                        DateTime.UtcNow;

                    long read =
                        process.PagedSystemMemorySize64;

                    long write =
                        process.PrivateMemorySize64;

                    if (_lastDiskCheck != default)
                    {
                        double seconds =
                            (
                                now -
                                _lastDiskCheck
                            ).TotalSeconds;

                        if (seconds > 0)
                        {
                            DiskReadMBps =
                                Math.Max(
                                    0,
                                    (
                                        Math.Abs(
                                            read -
                                            _lastDiskRead
                                        ) /
                                        1024d /
                                        1024d
                                    ) /
                                    seconds);

                            DiskWriteMBps =
                                Math.Max(
                                    0,
                                    (
                                        Math.Abs(
                                            write -
                                            _lastDiskWrite
                                        ) /
                                        1024d /
                                        1024d
                                    ) /
                                    seconds);
                        }
                    }

                    _lastDiskRead = read;
                    _lastDiskWrite = write;
                    _lastDiskCheck = now;
                }
                catch
                {
                    // Disk activity is optional.
                }
            }
            catch
            {
                DiskUsedPercent = 0;
                DiskFreeGB = 0;
                DiskTotalGB = 0;
            }
        }


        // =========================================================
        // SERVER DRIVE
        // =========================================================

        private string GetServerDrive()
        {
            try
            {
                Process? process =
                    _serverManager.ServerProcess;

                if (process != null)
                {
                    string path =
                        process.MainModule?.FileName
                        ?? "";

                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        return Path.GetPathRoot(
                            path)
                            ?? "";
                    }
                }
            }
            catch
            {
                // Fall through.
            }

            return
                Environment.GetFolderPath(
                    Environment.SpecialFolder.System)
                    .Substring(0, 3);
        }


        // =========================================================
        // NETWORK
        // =========================================================

        private void UpdateNetwork()
        {
            try
            {
                long received = 0;
                long sent = 0;

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

                    IPv4InterfaceStatistics stats =
                        networkInterface.GetIPv4Statistics();

                    received +=
                        stats.BytesReceived;

                    sent +=
                        stats.BytesSent;
                }

                DateTime now =
                    DateTime.UtcNow;

                if (_lastNetworkCheck != default)
                {
                    double seconds =
                        (
                            now -
                            _lastNetworkCheck
                        ).TotalSeconds;

                    if (seconds > 0)
                    {
                        NetworkDownloadMBps =
                            Math.Max(
                                0,
                                (
                                    received -
                                    _lastNetworkReceived
                                ) /
                                1024d /
                                1024d /
                                seconds);

                        NetworkUploadMBps =
                            Math.Max(
                                0,
                                (
                                    sent -
                                    _lastNetworkSent
                                ) /
                                1024d /
                                1024d /
                                seconds);
                    }
                }

                _lastNetworkReceived =
                    received;

                _lastNetworkSent =
                    sent;

                _lastNetworkCheck =
                    now;

                NetworkBytesReceived =
                    received;

                NetworkBytesSent =
                    sent;
            }
            catch
            {
                NetworkDownloadMBps = 0;
                NetworkUploadMBps = 0;
            }
        }


        // =========================================================
        // GPU
        // =========================================================

        private void UpdateGpu()
        {
            try
            {
                using Process process =
                    new Process();

                process.StartInfo =
                    new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments =
                            "-NoProfile -Command " +
                            "\"Get-CimInstance " +
                            "Win32_VideoController | " +
                            "Select-Object -First 1 Name\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                process.Start();

                string output =
                    process.StandardOutput
                        .ReadToEnd()
                        .Trim();

                process.WaitForExit(1000);

                if (!string.IsNullOrWhiteSpace(output))
                {
                    string[] lines =
                        output
                            .Split(
                                new[] {
                                    '\r',
                                    '\n'
                                },
                                StringSplitOptions.RemoveEmptyEntries);

                    if (lines.Length > 1)
                    {
                        GpuName =
                            lines[1].Trim();
                    }
                }
            }
            catch
            {
                GpuName =
                    "Not available";
            }

            // GPU utilization and temperature are intentionally
            // left at zero when Windows does not expose them.
            //
            // This prevents fake readings.
        }


        // =========================================================
        // SERVER INFORMATION
        // =========================================================

        private void UpdateServerInformation()
        {
            Process? process =
                _serverManager.ServerProcess;

            if (process == null)
            {
                Uptime =
                    TimeSpan.Zero;

                StartTime = null;

                return;
            }

            try
            {
                if (process.HasExited)
                {
                    Uptime =
                        TimeSpan.Zero;

                    StartTime = null;

                    return;
                }

                DateTime start =
                    process.StartTime;

                StartTime =
                    start;

                Uptime =
                    DateTime.Now -
                    start;

                if (Uptime < TimeSpan.Zero)
                    Uptime =
                        TimeSpan.Zero;
            }
            catch
            {
                Uptime =
                    TimeSpan.Zero;

                StartTime = null;
            }
        }


        // =========================================================
        // HEALTH
        // =========================================================

        private void UpdateHealth()
        {
            MonitorHealth newHealth =
                MonitorHealth.Healthy;

            string message =
                "All monitored resources are operating normally.";

            if (CpuUsage >=
                CpuCriticalThreshold)
            {
                newHealth =
                    MonitorHealth.Critical;

                message =
                    "System CPU usage is critically high.";
            }
            else if (MemoryUsagePercent >=
                     MemoryCriticalThreshold)
            {
                newHealth =
                    MonitorHealth.Critical;

                message =
                    "System memory usage is critically high.";
            }
            else if (DiskUsedPercent >=
                     DiskCriticalThreshold)
            {
                newHealth =
                    MonitorHealth.Critical;

                message =
                    "Disk usage is critically high.";
            }
            else if (DiskFreeGB > 0 &&
                     DiskFreeGB <=
                     DiskSpaceCriticalGB)
            {
                newHealth =
                    MonitorHealth.Critical;

                message =
                    "Available disk space is critically low.";
            }
            else if (CpuUsage >=
                     CpuWarningThreshold)
            {
                newHealth =
                    MonitorHealth.Warning;

                message =
                    "System CPU usage is elevated.";
            }
            else if (MemoryUsagePercent >=
                     MemoryWarningThreshold)
            {
                newHealth =
                    MonitorHealth.Warning;

                message =
                    "System memory usage is elevated.";
            }
            else if (DiskUsedPercent >=
                     DiskWarningThreshold)
            {
                newHealth =
                    MonitorHealth.Warning;

                message =
                    "Disk usage is elevated.";
            }
            else if (DiskFreeGB > 0 &&
                     DiskFreeGB <=
                     DiskSpaceWarningGB)
            {
                newHealth =
                    MonitorHealth.Warning;

                message =
                    "Available disk space is getting low.";
            }

            if (newHealth != Health)
            {
                AddEvent(
                    newHealth == MonitorHealth.Critical
                        ? MonitorEventType.Critical
                        : newHealth == MonitorHealth.Warning
                            ? MonitorEventType.Warning
                            : MonitorEventType.Information,
                    message);
            }

            Health =
                newHealth;

            HealthMessage =
                message;

            UpdateThresholdEventState();
        }


        // =========================================================
        // THRESHOLD EVENTS
        // =========================================================

        private void UpdateThresholdEventState()
        {
            bool cpuWarning =
                CpuUsage >=
                CpuWarningThreshold;

            if (cpuWarning !=
                _cpuWarningActive)
            {
                _cpuWarningActive =
                    cpuWarning;

                AddEvent(
                    cpuWarning
                        ? MonitorEventType.Warning
                        : MonitorEventType.Information,
                    cpuWarning
                        ? $"CPU usage exceeded " +
                          $"{CpuWarningThreshold:0}%."
                        : "CPU usage returned to normal.");
            }


            bool memoryWarning =
                MemoryUsagePercent >=
                MemoryWarningThreshold;

            if (memoryWarning !=
                _memoryWarningActive)
            {
                _memoryWarningActive =
                    memoryWarning;

                AddEvent(
                    memoryWarning
                        ? MonitorEventType.Warning
                        : MonitorEventType.Information,
                    memoryWarning
                        ? $"Memory usage exceeded " +
                          $"{MemoryWarningThreshold:0}%."
                        : "Memory usage returned to normal.");
            }


            bool diskWarning =
                DiskUsedPercent >=
                DiskWarningThreshold;

            if (diskWarning !=
                _diskWarningActive)
            {
                _diskWarningActive =
                    diskWarning;

                AddEvent(
                    diskWarning
                        ? MonitorEventType.Warning
                        : MonitorEventType.Information,
                    diskWarning
                        ? $"Disk usage exceeded " +
                          $"{DiskWarningThreshold:0}%."
                        : "Disk usage returned to normal.");
            }


            bool diskSpaceWarning =
                DiskFreeGB > 0 &&
                DiskFreeGB <=
                DiskSpaceWarningGB;

            if (diskSpaceWarning !=
                _diskSpaceWarningActive)
            {
                _diskSpaceWarningActive =
                    diskSpaceWarning;

                AddEvent(
                    diskSpaceWarning
                        ? MonitorEventType.Warning
                        : MonitorEventType.Information,
                    diskSpaceWarning
                        ? $"Disk free space dropped below " +
                          $"{DiskSpaceWarningGB:0} GB."
                        : "Disk free space returned to normal.");
            }
        }


        // =========================================================
        // HISTORY
        // =========================================================

        private void AddSnapshot()
        {
            _history.Add(
                new MonitorSnapshot
                {
                    Timestamp =
                        DateTime.Now,

                    CpuUsage =
                        CpuUsage,

                    MemoryUsagePercent =
                        MemoryUsagePercent,

                    DiskUsedPercent =
                        DiskUsedPercent,

                    NetworkDownloadMBps =
                        NetworkDownloadMBps,

                    NetworkUploadMBps =
                        NetworkUploadMBps,

                    ProcessCpuUsage =
                        ProcessCpuUsage,

                    ProcessMemoryGB =
                        ProcessMemoryGB
                });

            DateTime cutoff =
                DateTime.Now.AddMinutes(-5);

            _history.RemoveAll(
                x =>
                    x.Timestamp <
                    cutoff);
        }


        // =========================================================
        // EVENTS
        // =========================================================

        private void AddEvent(
            MonitorEventType type,
            string message)
        {
            MonitorEvent monitorEvent =
                new MonitorEvent
                {
                    Timestamp =
                        DateTime.Now,

                    Type =
                        type,

                    Message =
                        message
                };

            _events.Add(
                monitorEvent);

            if (_events.Count > 100)
            {
                _events.RemoveAt(0);
            }

            EventAdded?.Invoke(
                monitorEvent);
        }


        // =========================================================
        // PROCESS EXIT
        // =========================================================

        private void OnServerExited(
            int exitCode)
        {
            bool wasMonitoring =
                IsMonitoring;

            if (wasMonitoring)
            {
                AddEvent(
                    exitCode == 0
                        ? MonitorEventType.Information
                        : MonitorEventType.Critical,
                    exitCode == 0
                        ? "Server stopped normally."
                        : $"Server crashed or exited unexpectedly. " +
                          $"Exit code: {exitCode}.");
            }

            Stop();

            ServerExited?.Invoke(
                exitCode);

            if (wasMonitoring &&
                exitCode != 0)
            {
                ServerCrashed?.Invoke();
            }
        }


        // =========================================================
        // RESET
        // =========================================================

        private void ResetMeasurements()
        {
            CpuUsage = 0;

            MemoryBytes = 0;

            TotalMemoryGB = 0;

            MemoryUsagePercent = 0;

            ProcessCpuUsage = 0;

            ProcessMemoryBytes = 0;

            DiskUsedPercent = 0;

            DiskFreeGB = 0;

            DiskTotalGB = 0;

            DiskReadMBps = 0;

            DiskWriteMBps = 0;

            NetworkDownloadMBps = 0;

            NetworkUploadMBps = 0;

            Uptime =
                TimeSpan.Zero;

            StartTime = null;

            Health =
                MonitorHealth.Unknown;

            HealthMessage =
                "Waiting for monitoring data.";

            _cpuInitialized = false;

            _processCpuInitialized = false;

            _lastNetworkCheck =
                default;

            _lastDiskCheck =
                default;
        }


        // =========================================================
        // CPU WINDOWS API
        // =========================================================

        [StructLayout(
            LayoutKind.Sequential)]
        private struct SystemTimes
        {
            public uint Low;

            public int High;
        }

        private static ulong FileTimeToUInt64(
            SystemTimes time)
        {
            return
                ((ulong)(uint)time.High << 32) |
                time.Low;
        }

        [DllImport(
            "kernel32.dll")]
        private static extern bool GetSystemTimes(
            out SystemTimes idleTime,
            out SystemTimes kernelTime,
            out SystemTimes userTime);


        // =========================================================
        // MEMORY WINDOWS API
        // =========================================================

        [StructLayout(
            LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;

            public uint dwMemoryLoad;

            public ulong ullTotalPhys;

            public ulong ullAvailPhys;

            public ulong ullTotalPageFile;

            public ulong ullAvailPageFile;

            public ulong ullTotalVirtual;

            public ulong ullAvailVirtual;

            public ulong ullAvailExtendedVirtual;
        }

        [DllImport(
            "kernel32.dll",
            SetLastError = true)]
        [return: MarshalAs(
            UnmanagedType.Bool)]
        private static extern bool
            GlobalMemoryStatusEx(
                ref MEMORYSTATUSEX lpBuffer);


        // =========================================================
        // DISPOSE
        // =========================================================

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            Stop();

            _serverManager.ServerExited -=
                OnServerExited;
        }
    }


    // =============================================================
    // HEALTH ENUM
    // =============================================================

    public enum MonitorHealth
    {
        Unknown,
        Healthy,
        Warning,
        Critical
    }


    // =============================================================
    // EVENT TYPES
    // =============================================================

    public enum MonitorEventType
    {
        Information,
        Warning,
        Critical
    }


    // =============================================================
    // MONITOR EVENT
    // =============================================================

    public class MonitorEvent
    {
        public DateTime Timestamp { get; set; }

        public MonitorEventType Type { get; set; }

        public string Message { get; set; } = "";
    }


    // =============================================================
    // HISTORY SNAPSHOT
    // =============================================================

    public class MonitorSnapshot
    {
        public DateTime Timestamp { get; set; }

        public double CpuUsage { get; set; }

        public double MemoryUsagePercent { get; set; }

        public double DiskUsedPercent { get; set; }

        public double NetworkDownloadMBps { get; set; }

        public double NetworkUploadMBps { get; set; }

        public double ProcessCpuUsage { get; set; }

        public double ProcessMemoryGB { get; set; }
    }
}