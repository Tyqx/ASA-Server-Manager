using System;
using System.Threading.Tasks;
using ASAServerManager.SteamCMD;

namespace ASAServerManager.ASA
{
    public class ASAServerInstaller
    {
        private const int AsaAppId = 2430930;

        private readonly SteamCmdManager _steamCmd;

        public ASAServerInstaller(
            SteamCmdManager steamCmd)
        {
            _steamCmd = steamCmd;
        }

        public Task<int> InstallAsync(
            string installDirectory,
            Action<string> outputReceived)
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

            return _steamCmd.RunAsync(
                arguments,
                installDirectory,
                outputReceived);
        }

        public Task<int> VerifyAsync(
            string installDirectory,
            Action<string> outputReceived)
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

            return _steamCmd.RunAsync(
                arguments,
                installDirectory,
                outputReceived);
        }
    }
}