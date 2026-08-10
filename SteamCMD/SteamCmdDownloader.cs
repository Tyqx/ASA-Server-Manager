using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace ASAServerManager.SteamCMD
{
    public class SteamCmdDownloader
    {
        private const string SteamCmdUrl =
            "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";

        private readonly string _steamCmdDirectory;

        public SteamCmdDownloader(string steamCmdDirectory)
        {
            _steamCmdDirectory = steamCmdDirectory;
        }

        public string SteamCmdDirectory
        {
            get
            {
                return _steamCmdDirectory;
            }
        }

        public string SteamCmdPath
        {
            get
            {
                return Path.Combine(
                    _steamCmdDirectory,
                    "steamcmd.exe");
            }
        }

        public bool IsInstalled()
        {
            return File.Exists(SteamCmdPath);
        }

        public async Task DownloadAsync(
            Action<string> outputReceived = null)
        {
            Directory.CreateDirectory(_steamCmdDirectory);

            string zipPath = Path.Combine(
                _steamCmdDirectory,
                "steamcmd.zip");

            if (outputReceived != null)
            {
                outputReceived(
                    "SteamCMD directory:");
                outputReceived(
                    _steamCmdDirectory);

                outputReceived(
                    "SteamCMD executable:");
                outputReceived(
                    SteamCmdPath);

                outputReceived(
                    "Downloading SteamCMD...");
            }

            using (HttpClient client = new HttpClient())
            {
                client.Timeout =
                    TimeSpan.FromMinutes(10);

                using (HttpResponseMessage response =
                       await client.GetAsync(
                           SteamCmdUrl,
                           HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    using (Stream input =
                           await response.Content.ReadAsStreamAsync())
                    using (FileStream output =
                           new FileStream(
                               zipPath,
                               FileMode.Create,
                               FileAccess.Write,
                               FileShare.None))
                    {
                        await input.CopyToAsync(output);
                    }
                }
            }

            if (outputReceived != null)
            {
                outputReceived(
                    "SteamCMD download complete.");

                outputReceived(
                    "Extracting SteamCMD...");
            }

            ZipFile.ExtractToDirectory(
                zipPath,
                _steamCmdDirectory,
                true);

            File.Delete(zipPath);

            if (!IsInstalled())
            {
                throw new InvalidOperationException(
                    "SteamCMD extraction completed, but steamcmd.exe was not found at:" +
                    Environment.NewLine +
                    SteamCmdPath);
            }

            if (outputReceived != null)
            {
                outputReceived(
                    "steamcmd.exe found.");

                outputReceived(
                    "SteamCMD is ready.");
            }
        }
    }
}