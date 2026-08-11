using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ASAServerManager.Server
{
    public class AsaServerManager
    {
        private Process? _serverProcess;

        public bool IsRunning =>
            _serverProcess != null &&
            !_serverProcess.HasExited;

        public int? ProcessId =>
            IsRunning
                ? _serverProcess!.Id
                : null;

        public event Action<string>? OutputReceived;
        public event Action<string>? ErrorReceived;
        public event Action<int>? ServerExited;

        public bool Start(
            string serverDirectory,
            string arguments)
        {
            if (IsRunning)
                return false;

            string executable =
                Path.Combine(
                    serverDirectory,
                    "ShooterGame",
                    "Binaries",
                    "Win64",
                    "ArkAscendedServer.exe");

            if (!File.Exists(executable))
            {
                throw new FileNotFoundException(
                    "ArkAscendedServer.exe was not found.",
                    executable);
            }

            ProcessStartInfo startInfo =
    new ProcessStartInfo
    {
        FileName = executable,
        Arguments = arguments,
        WorkingDirectory =
            Path.GetDirectoryName(executable)
            ?? serverDirectory,

        UseShellExecute = false,

        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,

        CreateNoWindow = true,

        StandardOutputEncoding =
            System.Text.Encoding.UTF8,

        StandardErrorEncoding =
            System.Text.Encoding.UTF8
    };

            _serverProcess =
                new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                };

            _serverProcess.OutputDataReceived +=
                ServerProcess_OutputDataReceived;

            _serverProcess.ErrorDataReceived +=
                ServerProcess_ErrorDataReceived;

            _serverProcess.Exited +=
                ServerProcess_Exited;

            bool started =
                _serverProcess.Start();

            if (!started)
            {
                _serverProcess.Dispose();
                _serverProcess = null;

                return false;
            }

            _serverProcess.BeginOutputReadLine();
            _serverProcess.BeginErrorReadLine();

            return true;
        }

        public async Task<bool> SendCommandAsync(string command)
{
    if (!IsRunning || _serverProcess == null)
        return false;

    try
    {
        await _serverProcess.StandardInput.WriteLineAsync(command);
        await _serverProcess.StandardInput.FlushAsync();

        return true;
    }
    catch
    {
        return false;
    }
}

        public async Task StopAsync()
        {
            if (!IsRunning)
                return;

            try
            {
                _serverProcess!.Kill(
                    entireProcessTree: true);
            }
            catch
            {
                // Process may have already exited.
            }

            await Task.Run(() =>
            {
                try
                {
                    _serverProcess?.WaitForExit();
                }
                catch
                {
                }
            });
        }


        public async Task RestartAsync(
            string serverDirectory,
            string arguments)
        {
            if (IsRunning)
            {
                await StopAsync();

                await Task.Delay(1000);
            }

            Start(
                serverDirectory,
                arguments);
        }

        public void Dispose()
        {
            if (_serverProcess != null)
            {
                try
                {
                    if (!_serverProcess.HasExited)
                    {
                        _serverProcess.Kill(
                            entireProcessTree: true);
                    }
                }
                catch
                {
                }

                _serverProcess.Dispose();
                _serverProcess = null;
            }
        }

        private void ServerProcess_OutputDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                OutputReceived?.Invoke(
                    e.Data);
            }
        }

        private void ServerProcess_ErrorDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                ErrorReceived?.Invoke(
                    e.Data);
            }
        }

        private void ServerProcess_Exited(
            object? sender,
            EventArgs e)
        {
            int exitCode = -1;

            try
            {
                if (_serverProcess != null)
                {
                    exitCode =
                        _serverProcess.ExitCode;
                }
            }
            catch
            {
            }

            ServerExited?.Invoke(
                exitCode);
        }
    }
}