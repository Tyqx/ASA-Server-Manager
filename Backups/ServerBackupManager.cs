using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ASAServerManager.Backup
{
    public class ServerBackupManager : IDisposable
    {
        private CancellationTokenSource? _automaticBackupCancellation;
        private Task? _automaticBackupTask;

        private bool _disposed;

        // =========================================================
        // CREATE BACKUP
        // =========================================================

        public async Task<BackupInfo?> CreateBackupAsync(
            string serverPath,
            string backupDirectory,
            string type = "Manual")
        {
            if (_disposed)
                return null;

            if (string.IsNullOrWhiteSpace(serverPath))
                return null;

            if (!Directory.Exists(serverPath))
                return null;

            string rootBackupDirectory =
                GetBackupDirectory(
                    serverPath,
                    backupDirectory);

            Directory.CreateDirectory(
                rootBackupDirectory);

            string timestamp =
                DateTime.Now.ToString(
                    "yyyy-MM-dd_HHmmss");

            string backupPath =
                Path.Combine(
                    rootBackupDirectory,
                    timestamp);

            // Prevent an accidental duplicate timestamp.
            int counter = 1;

            while (Directory.Exists(backupPath))
            {
                backupPath =
                    Path.Combine(
                        rootBackupDirectory,
                        $"{timestamp}_{counter}");

                counter++;
            }

            Directory.CreateDirectory(
                backupPath);

            try
            {
                await Task.Run(
                    () =>
                        CopyServerData(
                            serverPath,
                            backupPath));

                long size =
                    CalculateDirectorySize(
                        backupPath);

                BackupInfo info =
                    new BackupInfo
                    {
                        Name =
                            Path.GetFileName(
                                backupPath),

                        Directory =
                            backupPath,

                        Created =
                            DateTime.Now,

                        SizeBytes =
                            size,

                        Type =
                            type
                    };

                await SaveBackupMetadataAsync(
                    info);

                return info;
            }
            catch
            {
                // Remove incomplete backup.
                try
                {
                    if (Directory.Exists(backupPath))
                    {
                        Directory.Delete(
                            backupPath,
                            true);
                    }
                }
                catch
                {
                }

                throw;
            }
        }

        // =========================================================
        // COPY SERVER DATA
        // =========================================================

        private static void CopyServerData(
            string serverPath,
            string backupPath)
        {
            // -----------------------------------------------------
            // ASA SAVE DATA
            // -----------------------------------------------------

            string savedDirectory =
                Path.Combine(
                    serverPath,
                    "ShooterGame",
                    "Saved");

            if (Directory.Exists(savedDirectory))
            {
                string destination =
                    Path.Combine(
                        backupPath,
                        "Saved");

                CopyDirectory(
                    savedDirectory,
                    destination);
            }

            // -----------------------------------------------------
            // GAME.INI
            // -----------------------------------------------------

            string gameIni =
                Path.Combine(
                    serverPath,
                    "ShooterGame",
                    "Saved",
                    "Config",
                    "WindowsServer",
                    "Game.ini");

            if (File.Exists(gameIni))
            {
                File.Copy(
                    gameIni,
                    Path.Combine(
                        backupPath,
                        "Game.ini"),
                    true);
            }

            // -----------------------------------------------------
            // GAMEUSERSETTINGS.INI
            // -----------------------------------------------------

            string gameUserSettings =
                Path.Combine(
                    serverPath,
                    "ShooterGame",
                    "Saved",
                    "Config",
                    "WindowsServer",
                    "GameUserSettings.ini");

            if (File.Exists(gameUserSettings))
            {
                File.Copy(
                    gameUserSettings,
                    Path.Combine(
                        backupPath,
                        "GameUserSettings.ini"),
                    true);
            }
        }

        // =========================================================
        // COPY DIRECTORY
        // =========================================================

        private static void CopyDirectory(
            string sourceDirectory,
            string destinationDirectory)
        {
            Directory.CreateDirectory(
                destinationDirectory);

            foreach (
                string file
                in Directory.GetFiles(
                    sourceDirectory))
            {
                string destinationFile =
                    Path.Combine(
                        destinationDirectory,
                        Path.GetFileName(file));

                File.Copy(
                    file,
                    destinationFile,
                    true);
            }

            foreach (
                string directory
                in Directory.GetDirectories(
                    sourceDirectory))
            {
                string destinationSubdirectory =
                    Path.Combine(
                        destinationDirectory,
                        Path.GetFileName(directory));

                CopyDirectory(
                    directory,
                    destinationSubdirectory);
            }
        }

        // =========================================================
        // GET BACKUP DIRECTORY
        // =========================================================

        public static string GetBackupDirectory(
            string serverPath,
            string backupDirectory)
        {
            if (!string.IsNullOrWhiteSpace(
                    backupDirectory))
            {
                return Path.Combine(
                    backupDirectory,
                    GetSafeServerName(serverPath));
            }

            string? serverRoot =
                Path.GetPathRoot(serverPath);

            if (string.IsNullOrWhiteSpace(
                    serverRoot))
            {
                serverRoot =
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.CommonApplicationData);
            }

            return Path.Combine(
                serverRoot,
                "ASA-Backups",
                GetSafeServerName(serverPath));
        }

        // =========================================================
        // SERVER NAME
        // =========================================================

        private static string GetSafeServerName(
            string serverPath)
        {
            string? directory =
                Path.GetDirectoryName(
                    serverPath.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar));

            string name =
                Path.GetFileName(
                    directory ??
                    serverPath);

            if (string.IsNullOrWhiteSpace(name))
                name = "ASA Server";

            foreach (
                char invalid
                in Path.GetInvalidFileNameChars())
            {
                name =
                    name.Replace(
                        invalid,
                        '_');
            }

            return name;
        }

        // =========================================================
        // GET BACKUPS
        // =========================================================

        public List<BackupInfo> GetBackups(
            string serverPath,
            string backupDirectory)
        {
            List<BackupInfo> backups =
                new();

            string rootDirectory =
                GetBackupDirectory(
                    serverPath,
                    backupDirectory);

            if (!Directory.Exists(
                    rootDirectory))
            {
                return backups;
            }

            foreach (
                string directory
                in Directory.GetDirectories(
                    rootDirectory))
            {
                try
                {
                    BackupInfo? info =
                        LoadBackupMetadata(
                            directory);

                    if (info == null)
                    {
                        info =
                            new BackupInfo
                            {
                                Name =
                                    Path.GetFileName(
                                        directory),

                                Directory =
                                    directory,

                                Created =
                                    Directory
                                        .GetCreationTime(
                                            directory),

                                SizeBytes =
                                    CalculateDirectorySize(
                                        directory),

                                Type =
                                    "Manual"
                            };
                    }

                    backups.Add(info);
                }
                catch
                {
                    // Ignore invalid backup folders.
                }
            }

            return backups
                .OrderByDescending(
                    x => x.Created)
                .ToList();
        }

        // =========================================================
        // DELETE BACKUP
        // =========================================================

        public bool DeleteBackup(
            BackupInfo backup)
        {
            if (backup == null)
                return false;

            try
            {
                if (!Directory.Exists(
                        backup.Directory))
                {
                    return false;
                }

                Directory.Delete(
                    backup.Directory,
                    true);

                return true;
            }
            catch
            {
                return false;
            }
        }

        // =========================================================
        // RESTORE BACKUP
        // =========================================================

        public async Task<bool> RestoreBackupAsync(
            string serverPath,
            BackupInfo backup,
            string backupDirectory)
        {
            if (_disposed)
                return false;

            if (backup == null)
                return false;

            if (!Directory.Exists(
                    backup.Directory))
            {
                return false;
            }

            if (!Directory.Exists(
                    serverPath))
            {
                return false;
            }

            // -----------------------------------------------------
            // CREATE SAFETY BACKUP FIRST
            // -----------------------------------------------------

            await CreateBackupAsync(
                serverPath,
                backupDirectory,
                "Safety");

            // -----------------------------------------------------
            // RESTORE SAVED
            // -----------------------------------------------------

            string savedBackup =
                Path.Combine(
                    backup.Directory,
                    "Saved");

            string savedDestination =
                Path.Combine(
                    serverPath,
                    "ShooterGame",
                    "Saved");

            if (Directory.Exists(
                    savedBackup))
            {
                if (Directory.Exists(
                        savedDestination))
                {
                    Directory.Delete(
                        savedDestination,
                        true);
                }

                await Task.Run(
                    () =>
                        CopyDirectory(
                            savedBackup,
                            savedDestination));
            }

            // -----------------------------------------------------
            // RESTORE GAME.INI
            // -----------------------------------------------------

            RestoreFile(
                backup.Directory,
                "Game.ini",
                Path.Combine(
                    serverPath,
                    "ShooterGame",
                    "Saved",
                    "Config",
                    "WindowsServer",
                    "Game.ini"));

            // -----------------------------------------------------
            // RESTORE GAMEUSERSETTINGS.INI
            // -----------------------------------------------------

            RestoreFile(
                backup.Directory,
                "GameUserSettings.ini",
                Path.Combine(
                    serverPath,
                    "ShooterGame",
                    "Saved",
                    "Config",
                    "WindowsServer",
                    "GameUserSettings.ini"));

            return true;
        }

        // =========================================================
        // RESTORE FILE
        // =========================================================

        private static void RestoreFile(
            string backupDirectory,
            string fileName,
            string destination)
        {
            string source =
                Path.Combine(
                    backupDirectory,
                    fileName);

            if (!File.Exists(source))
                return;

            string? destinationDirectory =
                Path.GetDirectoryName(
                    destination);

            if (!string.IsNullOrWhiteSpace(
                    destinationDirectory))
            {
                Directory.CreateDirectory(
                    destinationDirectory);
            }

            File.Copy(
                source,
                destination,
                true);
        }

        // =========================================================
        // RETENTION
        // =========================================================

        public void ApplyRetention(
            string serverPath,
            string backupDirectory,
            int retentionCount)
        {
            if (retentionCount < 1)
                retentionCount = 1;

            List<BackupInfo> backups =
                GetBackups(
                    serverPath,
                    backupDirectory);

            if (backups.Count <= retentionCount)
                return;

            foreach (
                BackupInfo backup
                in backups
                    .Skip(retentionCount))
            {
                DeleteBackup(backup);
            }
        }

        // =========================================================
        // AUTOMATIC BACKUPS
        // =========================================================

        public void StartAutomaticBackups(
            string serverPath,
            string backupDirectory,
            int intervalHours,
            int retentionCount)
        {
            StopAutomaticBackups();

            if (string.IsNullOrWhiteSpace(
                    serverPath))
            {
                return;
            }

            if (intervalHours < 1)
                intervalHours = 1;

            if (retentionCount < 1)
                retentionCount = 1;

            _automaticBackupCancellation =
                new CancellationTokenSource();

            CancellationToken token =
                _automaticBackupCancellation.Token;

            _automaticBackupTask =
                Task.Run(
                    async () =>
                    {
                        TimeSpan interval =
                            TimeSpan.FromHours(
                                intervalHours);

                        while (
                            !token.IsCancellationRequested)
                        {
                            try
                            {
                                await Task.Delay(
                                    interval,
                                    token);

                                if (token.IsCancellationRequested)
                                    break;

                                await CreateBackupAsync(
                                    serverPath,
                                    backupDirectory,
                                    "Automatic");

                                ApplyRetention(
                                    serverPath,
                                    backupDirectory,
                                    retentionCount);
                            }
                            catch (
                                OperationCanceledException)
                            {
                                break;
                            }
                            catch
                            {
                                // Automatic backups must
                                // never crash the application.
                            }
                        }
                    },
                    token);
        }

        // =========================================================
        // STOP AUTOMATIC BACKUPS
        // =========================================================

        public void StopAutomaticBackups()
        {
            try
            {
                _automaticBackupCancellation?.Cancel();
            }
            catch
            {
            }

            _automaticBackupCancellation =
                null;

            _automaticBackupTask =
                null;
        }

        // =========================================================
        // SIZE
        // =========================================================

        private static long CalculateDirectorySize(
            string directory)
        {
            long total = 0;

            try
            {
                foreach (
                    string file
                    in Directory.GetFiles(
                        directory))
                {
                    try
                    {
                        total +=
                            new FileInfo(file).Length;
                    }
                    catch
                    {
                    }
                }

                foreach (
                    string subdirectory
                    in Directory.GetDirectories(
                        directory))
                {
                    total +=
                        CalculateDirectorySize(
                            subdirectory);
                }
            }
            catch
            {
            }

            return total;
        }

        // =========================================================
        // METADATA
        // =========================================================

        private static async Task SaveBackupMetadataAsync(
            BackupInfo info)
        {
            try
            {
                string file =
                    Path.Combine(
                        info.Directory,
                        "backup.json");

                string json =
                    JsonSerializer.Serialize(
                        info,
                        new JsonSerializerOptions
                        {
                            WriteIndented = true
                        });

                await File.WriteAllTextAsync(
                    file,
                    json);
            }
            catch
            {
                // Metadata failure should not
                // invalidate the actual backup.
            }
        }

        private static BackupInfo? LoadBackupMetadata(
            string directory)
        {
            string file =
                Path.Combine(
                    directory,
                    "backup.json");

            if (!File.Exists(file))
                return null;

            try
            {
                string json =
                    File.ReadAllText(file);

                return
                    JsonSerializer.Deserialize<BackupInfo>(
                        json);
            }
            catch
            {
                return null;
            }
        }

        // =========================================================
        // DISPOSE
        // =========================================================

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            StopAutomaticBackups();
        }
    }
}