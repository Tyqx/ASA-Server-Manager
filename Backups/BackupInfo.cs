using System;

namespace ASAServerManager.Backup
{
    public class BackupInfo
    {
        public string Name { get; set; } = "";

        public string Directory { get; set; } = "";

        public DateTime Created { get; set; }

        public long SizeBytes { get; set; }

        public string Type { get; set; } = "Manual";
    }
}