using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ASAServerManager.SteamCMD
{
    public class SteamCmdManager
    {
        private readonly string _steamCmdPath;

        public SteamCmdManager(string steamCmdPath)
        {
            _steamCmdPath = steamCmdPath;
        }

        public bool IsAvailable()
        {
            return File.Exists(_steamCmdPath);
        }

        public async Task<int> RunAsync(
            string arguments,
            string workingDirectory,
            Action<string> outputReceived)
        {
            if (!IsAvailable())
            {
                throw new FileNotFoundException(
                    "SteamCMD was not found.",
                    _steamCmdPath);
            }

            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                workingDirectory =
                    Path.GetDirectoryName(_steamCmdPath)
                    ?? AppDomain.CurrentDomain.BaseDirectory;
            }

            Directory.CreateDirectory(
                workingDirectory);

            return await Task.Run(() =>
                RunWithConPty(
                    arguments,
                    workingDirectory,
                    outputReceived));
        }

        private int RunWithConPty(
            string arguments,
            string workingDirectory,
            Action<string> outputReceived)
        {
            IntPtr inputRead = IntPtr.Zero;
            IntPtr inputWrite = IntPtr.Zero;

            IntPtr outputRead = IntPtr.Zero;
            IntPtr outputWrite = IntPtr.Zero;

            IntPtr pseudoConsole = IntPtr.Zero;

            IntPtr attributeList = IntPtr.Zero;

            IntPtr processHandle = IntPtr.Zero;
            IntPtr threadHandle = IntPtr.Zero;

            try
            {
                outputReceived?.Invoke(
                    "Starting SteamCMD with ConPTY...");

                outputReceived?.Invoke(
                    "Executable: " +
                    _steamCmdPath);

                outputReceived?.Invoke(
                    "Arguments: " +
                    arguments);

                outputReceived?.Invoke("");

                // -------------------------------------------------
                // Create pipes
                // -------------------------------------------------

                if (!CreatePipe(
                        out inputRead,
                        out inputWrite,
                        IntPtr.Zero,
                        0))
                {
                    throw new System.ComponentModel.Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "Failed to create input pipe.");
                }

                if (!CreatePipe(
                        out outputRead,
                        out outputWrite,
                        IntPtr.Zero,
                        0))
                {
                    throw new System.ComponentModel.Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "Failed to create output pipe.");
                }

                // The parent should NOT inherit these ends.
                SetHandleInformation(
                    inputWrite,
                    HANDLE_FLAG_INHERIT,
                    0);

                SetHandleInformation(
                    outputRead,
                    HANDLE_FLAG_INHERIT,
                    0);

                // -------------------------------------------------
                // Create the pseudo console
                // -------------------------------------------------

                COORD consoleSize =
                    new COORD
                    {
                        X = 160,
                        Y = 50
                    };

                int hr =
                    ConptyCreatePseudoConsole(
                        consoleSize,
                        inputRead,
                        outputWrite,
                        0,
                        out pseudoConsole);

                if (hr != 0)
                {
                    throw new System.ComponentModel.Win32Exception(
                        hr,
                        "ConptyCreatePseudoConsole failed.");
                }

                // -------------------------------------------------
                // Prepare process attribute list
                // -------------------------------------------------

                IntPtr attributeListSize =
                    IntPtr.Zero;

                InitializeProcThreadAttributeList(
                    IntPtr.Zero,
                    1,
                    0,
                    ref attributeListSize);

                attributeList =
                    Marshal.AllocHGlobal(
                        attributeListSize);

                if (!InitializeProcThreadAttributeList(
                        attributeList,
                        1,
                        0,
                        ref attributeListSize))
                {
                    throw new System.ComponentModel.Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "InitializeProcThreadAttributeList failed.");
                }

                IntPtr pseudoConsolePtr =
                    pseudoConsole;

                if (!UpdateProcThreadAttribute(
                        attributeList,
                        0,
                        PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                        pseudoConsolePtr,
                        (IntPtr)IntPtr.Size,
                        IntPtr.Zero,
                        IntPtr.Zero))
                {
                    throw new System.ComponentModel.Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "UpdateProcThreadAttribute failed.");
                }

                // -------------------------------------------------
                // Start SteamCMD
                // -------------------------------------------------

                STARTUPINFOEX startupInfo =
                    new STARTUPINFOEX();

                startupInfo.StartupInfo.cb =
                    Marshal.SizeOf<STARTUPINFOEX>();

                startupInfo.lpAttributeList =
                    attributeList;

                PROCESS_INFORMATION processInfo =
                    new PROCESS_INFORMATION();

                string commandLine =
                    "\"" +
                    _steamCmdPath +
                    "\" " +
                    arguments;

                IntPtr commandLinePtr =
                    Marshal.StringToHGlobalUni(
                        commandLine);

                try
                {
                    bool created =
                        CreateProcess(
                            null,
                            commandLinePtr,
                            IntPtr.Zero,
                            IntPtr.Zero,
                            false,
                            EXTENDED_STARTUPINFO_PRESENT |
                            CREATE_UNICODE_ENVIRONMENT,
                            IntPtr.Zero,
                            workingDirectory,
                            ref startupInfo,
                            out processInfo);

                    if (!created)
                    {
                        throw new System.ComponentModel.Win32Exception(
                            Marshal.GetLastWin32Error(),
                            "CreateProcess failed.");
                    }

                    processHandle =
                        processInfo.hProcess;

                    threadHandle =
                        processInfo.hThread;
                }
                finally
                {
                    Marshal.FreeHGlobal(
                        commandLinePtr);
                }

                // The pseudo console owns these now.
                CloseHandle(inputRead);
                inputRead = IntPtr.Zero;

                CloseHandle(outputWrite);
                outputWrite = IntPtr.Zero;

                // -------------------------------------------------
                // Read output continuously
                // -------------------------------------------------

                ReadOutput(
                    outputRead,
                    outputReceived);

                // -------------------------------------------------
                // Wait for SteamCMD
                // -------------------------------------------------

                WaitForSingleObject(
                    processHandle,
                    INFINITE);

                uint exitCode;

                if (!GetExitCodeProcess(
                        processHandle,
                        out exitCode))
                {
                    throw new System.ComponentModel.Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "GetExitCodeProcess failed.");
                }

                return (int)exitCode;
            }
            finally
            {
                if (attributeList != IntPtr.Zero)
                {
                    DeleteProcThreadAttributeList(
                        attributeList);

                    Marshal.FreeHGlobal(
                        attributeList);
                }

                if (pseudoConsole != IntPtr.Zero)
                {
                    ConptyClosePseudoConsole(
                        pseudoConsole);
                }

                if (inputRead != IntPtr.Zero)
                    CloseHandle(inputRead);

                if (inputWrite != IntPtr.Zero)
                    CloseHandle(inputWrite);

                if (outputRead != IntPtr.Zero)
                    CloseHandle(outputRead);

                if (outputWrite != IntPtr.Zero)
                    CloseHandle(outputWrite);

                if (processHandle != IntPtr.Zero)
                    CloseHandle(processHandle);

                if (threadHandle != IntPtr.Zero)
                    CloseHandle(threadHandle);
            }
        }

        private void ReadOutput(
            IntPtr outputHandle,
            Action<string> outputReceived)
        {
            byte[] buffer =
                new byte[4096];

            while (true)
            {
                bool success =
                    ReadFile(
                        outputHandle,
                        buffer,
                        buffer.Length,
                        out uint bytesRead,
                        IntPtr.Zero);

                if (!success)
                {
                    int error =
                        Marshal.GetLastWin32Error();

                    if (error == ERROR_BROKEN_PIPE)
                        break;

                    throw new System.ComponentModel.Win32Exception(
                        error,
                        "Failed reading ConPTY output.");
                }

                if (bytesRead == 0)
                    break;

                string text =
                    Encoding.UTF8.GetString(
                        buffer,
                        0,
                        (int)bytesRead);

                outputReceived?.Invoke(text);
            }
        }

        // =========================================================
        // Native structures
        // =========================================================

        [StructLayout(LayoutKind.Sequential)]
        private struct COORD
        {
            public short X;
            public short Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        [StructLayout(
            LayoutKind.Sequential,
            CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;

            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;

            public int dwXCountChars;
            public int dwYCountChars;

            public int dwFillAttribute;
            public int dwFlags;

            public short wShowWindow;
            public short cbReserved2;

            public IntPtr lpReserved2;

            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        // =========================================================
        // Constants
        // =========================================================

        private const uint EXTENDED_STARTUPINFO_PRESENT =
            0x00080000;

        private const uint CREATE_UNICODE_ENVIRONMENT =
            0x00000400;

        private const uint HANDLE_FLAG_INHERIT =
            0x00000001;

        private const uint PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE =
            0x00020016;

        private const uint INFINITE =
            0xFFFFFFFF;

        private const int ERROR_BROKEN_PIPE =
            109;

        // =========================================================
        // Kernel32
        // =========================================================

        [DllImport(
            "kernel32.dll",
            SetLastError = true)]
        private static extern bool CreatePipe(
            out IntPtr hReadPipe,
            out IntPtr hWritePipe,
            IntPtr lpPipeAttributes,
            uint nSize);

        [DllImport(
            "kernel32.dll",
            SetLastError = true)]
        private static extern bool SetHandleInformation(
            IntPtr hObject,
            uint dwMask,
            uint dwFlags);

        [DllImport(
            "kernel32.dll",
            SetLastError = true)]
        private static extern bool ReadFile(
            IntPtr hFile,
            byte[] lpBuffer,
            int nNumberOfBytesToRead,
            out uint lpNumberOfBytesRead,
            IntPtr lpOverlapped);

        [DllImport(
            "kernel32.dll",
            SetLastError = true)]
        private static extern bool InitializeProcThreadAttributeList(
            IntPtr lpAttributeList,
            int dwAttributeCount,
            int dwFlags,
            ref IntPtr lpSize);

        [DllImport(
            "kernel32.dll",
            SetLastError = true)]
        private static extern bool UpdateProcThreadAttribute(
            IntPtr lpAttributeList,
            uint dwFlags,
            uint attribute,
            IntPtr lpValue,
            IntPtr cbSize,
            IntPtr lpPreviousValue,
            IntPtr lpReturnSize);

        [DllImport(
            "kernel32.dll",
            SetLastError = true)]
        private static extern void DeleteProcThreadAttributeList(
            IntPtr lpAttributeList);

        [DllImport(
            "kernel32.dll",
            SetLastError = true,
            CharSet = CharSet.Unicode)]
        private static extern bool CreateProcess(
            string lpApplicationName,
            IntPtr lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport(
            "kernel32.dll",
            SetLastError = true)]
        private static extern uint WaitForSingleObject(
            IntPtr hHandle,
            uint dwMilliseconds);

        [DllImport(
            "kernel32.dll",
            SetLastError = true)]
        private static extern bool GetExitCodeProcess(
            IntPtr hProcess,
            out uint lpExitCode);

        [DllImport(
            "kernel32.dll",
            SetLastError = true)]
        private static extern bool CloseHandle(
            IntPtr hObject);

        // =========================================================
        // ConPTY
        // =========================================================

        [DllImport(
            "conpty.dll",
            CallingConvention = CallingConvention.Winapi)]
        private static extern int ConptyCreatePseudoConsole(
            COORD size,
            IntPtr hInput,
            IntPtr hOutput,
            uint dwFlags,
            out IntPtr phPC);

        [DllImport(
            "conpty.dll",
            CallingConvention = CallingConvention.Winapi)]
        private static extern void ConptyClosePseudoConsole(
            IntPtr hPC);
    }
}