// Made by PapaGordon
using System;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Engine;
using WindowsGSM.GameServer.Query;

namespace WindowsGSM.Plugins
{
    // MeFriendos build based on WindowsGSM.ARMA3 by BattlefieldDuck.
    public class ARMA3 : SteamCMDAgent
    {
        private const uint WM_CLOSE = 0x0010;
        private const int SW_HIDE = 0;
        private const int SW_SHOWNORMAL = 1;
        private static readonly object ConsoleAttachLock = new object();

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        private static readonly ConstructorInfo DataReceivedEventArgsConstructor =
            typeof(DataReceivedEventArgs).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);

        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.ARMA3",
            author = "MeFriendos",
            description = "WindowsGSM plugin for Arma 3 Dedicated Server (MeFriendos build)",
            version = "0.1.4",
            url = "https://github.com/PapaGordon/WindowsGSM.ARMA3",
            color = "#9eff99"
        };

        // - Standard Constructor and properties
        public ARMA3(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData;

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => false;
        public override string AppId => "233780";

        // - Game server Fixed variables
        public override string StartPath => "arma3server_x64.exe";
        public string FullName = "Arma 3 Dedicated Server";
        public bool AllowsEmbedConsole = true;
        public int PortIncrements = 100;
        public object QueryMethod = new A2S();

        // - Game server default values
        public string Port = "2302";
        public string QueryPort = "2303";
        public string Defaultmap = "empty";
        public string Maxplayers = "64";
        public string Additional = "-profiles=ArmaHosts -config=server.cfg";

        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG() { }

        private static string GetStartParameterValue(string arguments, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(arguments) || string.IsNullOrWhiteSpace(parameterName))
                return null;

            string marker = "-" + parameterName + "=";
            int index = arguments.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return null;

            int valueStart = index + marker.Length;
            if (valueStart >= arguments.Length)
                return string.Empty;

            bool valueQuoted = arguments[valueStart] == '"';
            bool wholeArgumentQuoted = index > 0 && arguments[index - 1] == '"';

            if (valueQuoted)
            {
                valueStart++;
                int valueEnd = arguments.IndexOf('"', valueStart);
                if (valueEnd < 0)
                    valueEnd = arguments.Length;

                return arguments.Substring(valueStart, valueEnd - valueStart);
            }

            if (wholeArgumentQuoted)
            {
                int valueEnd = arguments.IndexOf('"', valueStart);
                if (valueEnd < 0)
                    valueEnd = arguments.Length;

                return arguments.Substring(valueStart, valueEnd - valueStart);
            }

            int end = valueStart;
            while (end < arguments.Length && !char.IsWhiteSpace(arguments[end]))
                end++;

            return arguments.Substring(valueStart, end - valueStart).Trim('"');
        }

        private static bool HasStartSwitch(string arguments, string switchName)
        {
            if (string.IsNullOrWhiteSpace(arguments) || string.IsNullOrWhiteSpace(switchName))
                return false;

            string marker = "-" + switchName;
            int searchFrom = 0;

            while (searchFrom < arguments.Length)
            {
                int index = arguments.IndexOf(marker, searchFrom, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                    return false;

                int end = index + marker.Length;
                bool leftBoundary = index == 0 || char.IsWhiteSpace(arguments[index - 1]) || arguments[index - 1] == '"';
                bool rightBoundary = end >= arguments.Length || char.IsWhiteSpace(arguments[end]) || arguments[end] == '"';

                if (leftBoundary && rightBoundary)
                    return true;

                searchFrom = index + marker.Length;
            }

            return false;
        }

        private string GetRptDirectory(string arguments)
        {
            string configuredPath = GetStartParameterValue(arguments, "profiles");

            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Arma 3");
            }

            configuredPath = Environment.ExpandEnvironmentVariables(configuredPath.Trim());

            if (Path.IsPathRooted(configuredPath))
                return Path.GetFullPath(configuredPath);

            return Path.GetFullPath(Path.Combine(
                ServerPath.GetServersServerFiles(_serverData.ServerID),
                configuredPath));
        }

        private static string[] GetRptFiles(string directory)
        {
            var files = new List<string>();

            try
            {
                if (!Directory.Exists(directory))
                    return files.ToArray();

                try
                {
                    files.AddRange(Directory.GetFiles(directory, "*.rpt", SearchOption.TopDirectoryOnly));
                }
                catch
                {
                    // Continue with accessible child directories.
                }

                string[] childDirectories;
                try
                {
                    childDirectories = Directory.GetDirectories(directory);
                }
                catch
                {
                    childDirectories = new string[0];
                }

                foreach (string childDirectory in childDirectories)
                {
                    try
                    {
                        files.AddRange(Directory.GetFiles(childDirectory, "*.rpt", SearchOption.TopDirectoryOnly));
                    }
                    catch
                    {
                        // Ignore inaccessible profile subdirectories.
                    }
                }
            }
            catch
            {
                // Return whatever was discovered so far.
            }

            return files.ToArray();
        }

        private static Dictionary<string, long> SnapshotRptFiles(string directory)
        {
            var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (!Directory.Exists(directory))
                    return result;

                foreach (string file in GetRptFiles(directory))
                {
                    try
                    {
                        result[file] = new FileInfo(file).Length;
                    }
                    catch
                    {
                        // Ignore files that disappear while taking the snapshot.
                    }
                }
            }
            catch
            {
                // The server itself must still be allowed to start if log discovery fails.
            }

            return result;
        }

        private static bool LooksLikeArmaRpt(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            return !string.IsNullOrWhiteSpace(fileName) &&
                   fileName.IndexOf("arma3", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FindActiveRptFile(
            string directory,
            Dictionary<string, long> snapshot)
        {
            try
            {
                if (!Directory.Exists(directory))
                    return null;

                string newestPath = null;
                DateTime newestWrite = DateTime.MinValue;

                foreach (string file in GetRptFiles(directory))
                {
                    if (!LooksLikeArmaRpt(file))
                        continue;

                    try
                    {
                        var info = new FileInfo(file);
                        long previousLength;
                        bool existedBefore = snapshot.TryGetValue(file, out previousLength);
                        bool isNewOrChanged = !existedBefore || info.Length != previousLength;

                        if (!isNewOrChanged)
                            continue;

                        if (newestPath == null || info.LastWriteTimeUtc > newestWrite)
                        {
                            newestPath = file;
                            newestWrite = info.LastWriteTimeUtc;
                        }
                    }
                    catch
                    {
                        // Retry on the next polling pass.
                    }
                }

                return newestPath;
            }
            catch
            {
                return null;
            }
        }

        private void AddEmbeddedConsoleLine(ServerConsole serverConsole, string line)
        {
            if (serverConsole == null || line == null)
                return;

            try
            {
                if (DataReceivedEventArgsConstructor != null)
                {
                    var args = (DataReceivedEventArgs)DataReceivedEventArgsConstructor.Invoke(new object[] { line });
                    serverConsole.AddOutput(null, args);
                    return;
                }

                int serverId;
                if (int.TryParse(Convert.ToString(_serverData.ServerID), out serverId) &&
                    WindowsGSM.MainWindow._serverMetadata.ContainsKey(serverId))
                {
                    WindowsGSM.MainWindow._serverMetadata[serverId].ServerConsole.Add(line);
                }
            }
            catch
            {
                // Console mirroring must never interfere with the game server process.
            }
        }

        private async Task FollowRptLog(
            Process process,
            string rptDirectory,
            Dictionary<string, long> snapshot,
            ServerConsole serverConsole)
        {
            string rptFile = null;
            DateTime nextWaitingNotice = DateTime.UtcNow.AddSeconds(15);
            bool waitingNoticeShown = false;

            AddEmbeddedConsoleLine(serverConsole, "[WindowsGSM] Waiting for Arma 3 RPT output...");

            while (rptFile == null)
            {
                rptFile = FindActiveRptFile(rptDirectory, snapshot);
                if (rptFile != null)
                    break;

                try
                {
                    if (process.HasExited)
                    {
                        // One final lookup catches an RPT file written during process shutdown.
                        rptFile = FindActiveRptFile(rptDirectory, snapshot);
                        break;
                    }
                }
                catch
                {
                    break;
                }

                if (!waitingNoticeShown && DateTime.UtcNow >= nextWaitingNotice)
                {
                    waitingNoticeShown = true;
                    AddEmbeddedConsoleLine(
                        serverConsole,
                        "[WindowsGSM] Still waiting for an RPT file. Check -profiles= and make sure -noLogs is not enabled.");
                }

                await Task.Delay(250);
            }

            if (rptFile == null)
            {
                AddEmbeddedConsoleLine(serverConsole, "[WindowsGSM] No Arma 3 RPT file became available.");
                return;
            }

            long startOffset = 0;
            long oldLength;
            if (snapshot.TryGetValue(rptFile, out oldLength))
                startOffset = oldLength;

            FileStream stream = null;

            while (stream == null)
            {
                try
                {
                    stream = new FileStream(
                        rptFile,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);
                }
                catch (IOException)
                {
                    try
                    {
                        if (process.HasExited)
                            return;
                    }
                    catch
                    {
                        return;
                    }

                    await Task.Delay(200);
                }
                catch (UnauthorizedAccessException)
                {
                    AddEmbeddedConsoleLine(serverConsole, "[WindowsGSM] RPT file found, but it cannot be read (access denied).");
                    return;
                }
                catch
                {
                    return;
                }
            }

            using (stream)
            {
                if (startOffset > 0 && startOffset <= stream.Length)
                    stream.Seek(startOffset, SeekOrigin.Begin);

                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    AddEmbeddedConsoleLine(
                        serverConsole,
                        "[WindowsGSM] Reading " + Path.GetFileName(rptFile));

                    while (true)
                    {
                        string line = reader.ReadLine();
                        if (line != null)
                        {
                            AddEmbeddedConsoleLine(serverConsole, line);
                            continue;
                        }

                        bool exited;
                        try
                        {
                            exited = process.HasExited;
                        }
                        catch
                        {
                            exited = true;
                        }

                        if (exited)
                        {
                            // Give Arma a short moment to flush the last RPT lines.
                            await Task.Delay(300);
                            line = reader.ReadLine();
                            if (line == null)
                                break;

                            AddEmbeddedConsoleLine(serverConsole, line);
                            continue;
                        }

                        await Task.Delay(150);
                    }
                }
            }
        }

        private static string GetWindowClassName(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
                return string.Empty;

            try
            {
                var className = new StringBuilder(256);
                int length = GetClassName(hWnd, className, className.Capacity);
                return length > 0 ? className.ToString() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static IntPtr FindTopLevelWindowForProcess(Process process)
        {
            if (process == null)
                return IntPtr.Zero;

            int processId;
            try
            {
                processId = process.Id;
            }
            catch
            {
                return IntPtr.Zero;
            }

            IntPtr visibleWindow = IntPtr.Zero;
            IntPtr preferredConsoleWindow = IntPtr.Zero;

            try
            {
                EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
                {
                    uint windowProcessId;
                    GetWindowThreadProcessId(hWnd, out windowProcessId);

                    if (windowProcessId != (uint)processId || !IsWindow(hWnd))
                        return true;

                    string className = GetWindowClassName(hWnd);
                    if (string.Equals(className, "ConsoleWindowClass", StringComparison.OrdinalIgnoreCase))
                    {
                        preferredConsoleWindow = hWnd;
                        return false;
                    }

                    if (visibleWindow == IntPtr.Zero && IsWindowVisible(hWnd))
                        visibleWindow = hWnd;

                    return true;
                }, IntPtr.Zero);
            }
            catch
            {
                return IntPtr.Zero;
            }

            return preferredConsoleWindow != IntPtr.Zero ? preferredConsoleWindow : visibleWindow;
        }

        private static IntPtr GetAttachedConsoleWindow(Process process, out string windowClass, out int attachError)
        {
            windowClass = string.Empty;
            attachError = 0;

            if (process == null)
                return IntPtr.Zero;

            lock (ConsoleAttachLock)
            {
                try
                {
                    if (process.HasExited)
                        return IntPtr.Zero;

                    // Console attachment is process-wide. Never detach a console that WindowsGSM
                    // was already attached to before this probe.
                    if (GetConsoleWindow() != IntPtr.Zero)
                        return IntPtr.Zero;

                    if (!AttachConsole((uint)process.Id))
                    {
                        attachError = Marshal.GetLastWin32Error();
                        return IntPtr.Zero;
                    }

                    try
                    {
                        IntPtr consoleWindow = GetConsoleWindow();
                        if (consoleWindow == IntPtr.Zero || !IsWindow(consoleWindow))
                            return IntPtr.Zero;

                        windowClass = GetWindowClassName(consoleWindow);
                        return consoleWindow;
                    }
                    finally
                    {
                        FreeConsole();
                    }
                }
                catch
                {
                    attachError = Marshal.GetLastWin32Error();
                    return IntPtr.Zero;
                }
            }
        }

        private static IntPtr ResolveToggleWindow(
            Process process,
            out string source,
            out string windowClass,
            out int attachError)
        {
            source = string.Empty;
            windowClass = string.Empty;
            attachError = 0;

            if (process == null)
                return IntPtr.Zero;

            try
            {
                process.Refresh();
                IntPtr mainWindow = process.MainWindowHandle;
                if (mainWindow != IntPtr.Zero && IsWindow(mainWindow))
                {
                    string mainWindowClass = GetWindowClassName(mainWindow);
                    if (IsWindowVisible(mainWindow) ||
                        string.Equals(mainWindowClass, "ConsoleWindowClass", StringComparison.OrdinalIgnoreCase))
                    {
                        source = "Process.MainWindowHandle after Refresh";
                        windowClass = mainWindowClass;
                        return mainWindow;
                    }
                }
            }
            catch
            {
                // Continue with explicit window discovery.
            }

            IntPtr processWindow = FindTopLevelWindowForProcess(process);
            if (processWindow != IntPtr.Zero && IsWindow(processWindow))
            {
                source = "EnumWindows by Arma PID";
                windowClass = GetWindowClassName(processWindow);
                return processWindow;
            }

            string consoleClass;
            IntPtr consoleWindow = GetAttachedConsoleWindow(process, out consoleClass, out attachError);
            if (consoleWindow == IntPtr.Zero)
                return IntPtr.Zero;

            source = "AttachConsole/GetConsoleWindow";
            windowClass = consoleClass;
            return consoleWindow;
        }

        private static bool IsSafeToggleTarget(IntPtr hWnd, string windowClass)
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
                return false;

            // A PseudoConsoleWindow is not necessarily the visible terminal window. Registering or
            // hiding a terminal host can affect unrelated sessions, so do not treat it as a native
            // Toggle Console target.
            return !string.Equals(windowClass, "PseudoConsoleWindow", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetShowConsoleState(object metadata, out bool showConsole)
        {
            showConsole = false;
            if (metadata == null)
                return false;

            try
            {
                Type metadataType = metadata.GetType();
                FieldInfo field = metadataType.GetField(
                    "ShowConsole",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (field != null && field.FieldType == typeof(bool))
                {
                    showConsole = (bool)field.GetValue(metadata);
                    return true;
                }

                PropertyInfo property = metadataType.GetProperty(
                    "ShowConsole",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (property != null && property.PropertyType == typeof(bool) && property.CanRead)
                {
                    showConsole = (bool)property.GetValue(metadata, null);
                    return true;
                }
            }
            catch
            {
                // Upstream WindowsGSM versions may not expose ShowConsole. Handle sync still works.
            }

            return false;
        }

        private string GetToggleConsoleDiagnosticPath()
        {
            return Path.Combine(
                ServerPath.GetServersCache(_serverData.ServerID),
                "arma3-toggle-console.log");
        }

        private void ResetToggleConsoleDiagnostic(Process process)
        {
            try
            {
                string path = GetToggleConsoleDiagnosticPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(
                    path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                    " [0.1.2] Toggle Console monitor started for PID " + process.Id +
                    "; WindowsGSM " + WindowsGSM.MainWindow.WGSM_VERSION + Environment.NewLine);
            }
            catch
            {
                // Diagnostics must never interfere with server startup.
            }
        }

        private void WriteToggleConsoleDiagnostic(string message)
        {
            try
            {
                string path = GetToggleConsoleDiagnosticPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.AppendAllText(
                    path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + Environment.NewLine);
            }
            catch
            {
                // Diagnostics must never interfere with server operation.
            }
        }

        private void SaveWindowsGsmConsoleHandle(IntPtr consoleWindow)
        {
            try
            {
                string cachePath = ServerPath.GetServersCache(_serverData.ServerID);
                Directory.CreateDirectory(cachePath);
                File.WriteAllText(Path.Combine(cachePath, "windowsIntPtr"), consoleWindow.ToString());
            }
            catch
            {
                // The in-memory handle is enough for the current WindowsGSM session.
            }
        }

        private async Task MonitorNativeConsoleHandle(Process process, ServerConsole serverConsole)
        {
            int serverId;
            if (!int.TryParse(Convert.ToString(_serverData.ServerID), out serverId))
                return;

            ResetToggleConsoleDiagnostic(process);

            IntPtr resolvedWindow = IntPtr.Zero;
            string resolvedSource = string.Empty;
            string resolvedClass = string.Empty;
            int lastAttachError = 0;
            DateTime nextResolveAt = DateTime.MinValue;
            DateTime unresolvedNoticeAt = DateTime.UtcNow.AddSeconds(15);
            bool unresolvedNoticeShown = false;
            bool showConsoleCapabilityLogged = false;
            bool showConsoleUnavailableLogged = false;
            bool? lastAppliedShowConsole = null;

            while (true)
            {
                try
                {
                    if (process.HasExited)
                    {
                        WriteToggleConsoleDiagnostic("Arma process exited; Toggle Console monitor stopped.");
                        return;
                    }
                }
                catch
                {
                    WriteToggleConsoleDiagnostic("Arma process state became unavailable; Toggle Console monitor stopped.");
                    return;
                }

                if (resolvedWindow == IntPtr.Zero ||
                    !IsWindow(resolvedWindow) ||
                    DateTime.UtcNow >= nextResolveAt)
                {
                    string source;
                    string windowClass;
                    int attachError;
                    IntPtr candidate = ResolveToggleWindow(process, out source, out windowClass, out attachError);
                    lastAttachError = attachError;
                    nextResolveAt = DateTime.UtcNow.AddSeconds(5);

                    if (candidate != IntPtr.Zero && !IsSafeToggleTarget(candidate, windowClass))
                    {
                        WriteToggleConsoleDiagnostic(
                            "Rejected HWND 0x" + candidate.ToInt64().ToString("X") +
                            " via " + source + " [" + windowClass + "] because it is not a safe native toggle target.");
                        candidate = IntPtr.Zero;
                    }

                    if (candidate != IntPtr.Zero)
                    {
                        if (resolvedWindow != candidate)
                        {
                            WriteToggleConsoleDiagnostic(
                                "Resolved HWND 0x" + candidate.ToInt64().ToString("X") +
                                " via " + source +
                                (string.IsNullOrWhiteSpace(windowClass) ? string.Empty : " [" + windowClass + "]") + ".");
                            lastAppliedShowConsole = null;
                        }

                        resolvedWindow = candidate;
                        resolvedSource = source;
                        resolvedClass = windowClass;
                        unresolvedNoticeShown = false;
                        unresolvedNoticeAt = DateTime.UtcNow.AddSeconds(15);
                    }
                    else if (resolvedWindow != IntPtr.Zero && !IsWindow(resolvedWindow))
                    {
                        WriteToggleConsoleDiagnostic("Previously resolved Toggle Console HWND became invalid.");
                        resolvedWindow = IntPtr.Zero;
                        resolvedSource = string.Empty;
                        resolvedClass = string.Empty;
                        lastAppliedShowConsole = null;
                    }
                }

                bool matchingProcessRegistered = false;
                if (WindowsGSM.MainWindow._serverMetadata.ContainsKey(serverId))
                {
                    try
                    {
                        var metadata = WindowsGSM.MainWindow._serverMetadata[serverId];
                        Process trackedProcess = metadata.Process;
                        matchingProcessRegistered = trackedProcess != null && trackedProcess.Id == process.Id;

                        if (matchingProcessRegistered && resolvedWindow != IntPtr.Zero && IsWindow(resolvedWindow))
                        {
                            if (metadata.MainWindow != resolvedWindow)
                            {
                                IntPtr previousWindow = metadata.MainWindow;
                                metadata.MainWindow = resolvedWindow;
                                SaveWindowsGsmConsoleHandle(resolvedWindow);

                                WriteToggleConsoleDiagnostic(
                                    "WindowsGSM MainWindow updated from 0x" +
                                    previousWindow.ToInt64().ToString("X") + " to 0x" +
                                    resolvedWindow.ToInt64().ToString("X") +
                                    " via " + resolvedSource +
                                    (string.IsNullOrWhiteSpace(resolvedClass) ? string.Empty : " [" + resolvedClass + "]") + ".");

                                AddEmbeddedConsoleLine(
                                    serverConsole,
                                    "[WindowsGSM] Toggle Console window registered via " + resolvedSource +
                                    " (HWND 0x" + resolvedWindow.ToInt64().ToString("X") +
                                    (string.IsNullOrWhiteSpace(resolvedClass) ? string.Empty : ", " + resolvedClass) + ").");
                            }

                            bool desiredShowConsole;
                            if (TryGetShowConsoleState(metadata, out desiredShowConsole))
                            {
                                if (!showConsoleCapabilityLogged)
                                {
                                    WriteToggleConsoleDiagnostic(
                                        "Detected WindowsGSM ShowConsole state support; direct visibility synchronization enabled.");
                                    showConsoleCapabilityLogged = true;
                                }

                                bool currentlyVisible = IsWindowVisible(resolvedWindow);
                                if (currentlyVisible != desiredShowConsole ||
                                    !lastAppliedShowConsole.HasValue ||
                                    lastAppliedShowConsole.Value != desiredShowConsole)
                                {
                                    ShowWindow(resolvedWindow, desiredShowConsole ? SW_SHOWNORMAL : SW_HIDE);
                                    lastAppliedShowConsole = desiredShowConsole;

                                    WriteToggleConsoleDiagnostic(
                                        "Applied ShowConsole=" + desiredShowConsole +
                                        " directly to HWND 0x" + resolvedWindow.ToInt64().ToString("X") + ".");
                                }
                            }
                            else if (!showConsoleUnavailableLogged)
                            {
                                WriteToggleConsoleDiagnostic(
                                    "WindowsGSM does not expose ShowConsole; using handle synchronization only.");
                                showConsoleUnavailableLogged = true;
                            }
                        }
                    }
                    catch
                    {
                        // WindowsGSM may be updating its metadata at the same time. Retry below.
                    }
                }

                if (resolvedWindow == IntPtr.Zero &&
                    DateTime.UtcNow >= unresolvedNoticeAt &&
                    !unresolvedNoticeShown)
                {
                    string detail = lastAttachError == 0
                        ? "No usable native window was found."
                        : "AttachConsole failed with Win32 error " + lastAttachError + ".";

                    WriteToggleConsoleDiagnostic(
                        detail + " WindowsGSM process registered=" + matchingProcessRegistered + ".");
                    AddEmbeddedConsoleLine(
                        serverConsole,
                        "[WindowsGSM] Toggle Console could not resolve a usable native Arma window. See arma3-toggle-console.log in this server's cache folder.");
                    unresolvedNoticeShown = true;
                }

                await Task.Delay(250);
            }
        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            Error = null;
            string exePath = ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            if (!File.Exists(exePath))
            {
                Error = StartPath + " not found (" + exePath + ")";
                return null;
            }

            var param = new StringBuilder();
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerPort) ? string.Empty : $" -port={_serverData.ServerPort}");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerName) ? string.Empty : $" -name=\"{_serverData.ServerName}\"");
            param.Append(string.IsNullOrWhiteSpace(_serverData.ServerParam) ? string.Empty : $" {_serverData.ServerParam}");

            string commandLine = param.ToString();
            try
            {
                var workshop = await ARMA3WorkshopUpdater.Update(ServerPath.GetServersServerFiles(_serverData.ServerID),
                    ServerPath.GetServersConfigs(_serverData.ServerID), ServerPath.GetBin("steamcmd"), "107410");
                commandLine = ARMA3WorkshopUpdater.MergeArguments(commandLine, workshop);
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null;
            }
            string rptDirectory = null;
            Dictionary<string, long> rptSnapshot = null;
            ServerConsole serverConsole = null;
            bool rptConsoleEnabled = AllowsEmbedConsole && !HasStartSwitch(commandLine, "noLogs");

            if (AllowsEmbedConsole)
                serverConsole = new ServerConsole(_serverData.ServerID);

            if (rptConsoleEnabled)
            {
                try
                {
                    rptDirectory = GetRptDirectory(commandLine);
                    rptSnapshot = SnapshotRptFiles(rptDirectory);
                }
                catch
                {
                    rptConsoleEnabled = false;
                }
            }

            var p = new Process
            {
                StartInfo =
                {
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false,
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = exePath,
                    Arguments = commandLine
                },
                EnableRaisingEvents = true
            };

            try
            {
                p.Start();

#pragma warning disable 4014
                Task.Run(() => MonitorNativeConsoleHandle(p, serverConsole));
#pragma warning restore 4014

                if (AllowsEmbedConsole)
                {
                    if (HasStartSwitch(commandLine, "noLogs"))
                    {
                        AddEmbeddedConsoleLine(
                            serverConsole,
                            "[WindowsGSM] Embedded console cannot mirror Arma output because -noLogs disables the RPT log.");
                    }
                    else if (!rptConsoleEnabled || string.IsNullOrWhiteSpace(rptDirectory))
                    {
                        AddEmbeddedConsoleLine(
                            serverConsole,
                            "[WindowsGSM] Embedded console could not determine the Arma RPT directory.");
                    }
                    else
                    {
#pragma warning disable 4014
                        Task.Run(() => FollowRptLog(p, rptDirectory, rptSnapshot, serverConsole));
#pragma warning restore 4014
                    }
                }

                return p;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null;
            }
        }

        private bool RequestConsoleClose(Process p)
        {
            try
            {
                p.Refresh();
                if (p.MainWindowHandle != IntPtr.Zero && p.CloseMainWindow())
                    return true;

                string windowClass;
                int attachError;
                IntPtr consoleWindow = GetAttachedConsoleWindow(p, out windowClass, out attachError);

                // Do not post WM_CLOSE to a terminal-host/pseudoconsole window. In that case the
                // existing process-termination fallback is safer than closing somebody's terminal.
                if (consoleWindow == IntPtr.Zero ||
                    string.Equals(windowClass, "PseudoConsoleWindow", StringComparison.OrdinalIgnoreCase))
                    return false;

                return PostMessage(consoleWindow, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
            catch
            {
                return false;
            }
        }

        // - Stop server function
        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                if (p == null)
                    return;

                try
                {
                    if (p.HasExited)
                        return;

                    if (RequestConsoleClose(p) && p.WaitForExit(20000))
                        return;
                }
                catch
                {
                    // Fall back to process termination below.
                }

                try
                {
                    if (!p.HasExited)
                        p.Kill();
                }
                catch
                {
                    // Process may have exited between the checks.
                }
            });
        }
    }
}

namespace WindowsGSM.Plugins
{
    internal static class ARMA3WorkshopUpdater
    {
        internal sealed class Item
        {
            internal string Id, Folder, Mode;
        }

        internal static List<Item> Read(string path)
        {
            var items = new List<Item>();
            if (!File.Exists(path)) return items;
            foreach (string raw in File.ReadLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//")) continue;
                string[] parts = line.Split('|').Select(p => p.Trim()).ToArray();
                ulong id;
                if (parts.Length != 3 || !Regex.IsMatch(parts[0], @"\A[1-9][0-9]*\z")
                    || !ulong.TryParse(parts[0], out id)
                    || !Regex.IsMatch(parts[1], @"\A@[A-Za-z0-9_-]+\z")
                    || (parts[2] != "mod" && parts[2] != "servermod"))
                    throw new InvalidOperationException("Invalid Workshop entry. Use itemId|@Folder|mod or itemId|@Folder|servermod.");
                if (items.Any(i => i.Id == parts[0] || string.Equals(i.Folder, parts[1], StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Duplicate Workshop ID or folder: " + parts[0]);
                items.Add(new Item { Id = parts[0], Folder = parts[1], Mode = parts[2] });
            }
            return items;
        }

        internal static string MergeArguments(string arguments, List<Item> items)
        {
            if (items.Count == 0) return arguments;
            var tokens = Regex.Matches(arguments ?? "", "(?:\"[^\"]*\"|[^\\s\"]+)+").Cast<Match>().Select(m => m.Value).ToList();
            foreach (string mode in new[] { "mod", "servermod" })
            {
                var added = items.Where(i => i.Mode == mode).Select(i => i.Folder).ToList();
                if (added.Count == 0) continue;
                var folders = new List<string>();
                var keep = new List<string>();
                foreach (string token in tokens)
                {
                    string clean = token.Replace("\"", "");
                    if (clean.StartsWith("-" + mode + "=", StringComparison.OrdinalIgnoreCase))
                        folders.AddRange(clean.Substring(mode.Length + 2).Split(';').Where(p => !string.IsNullOrWhiteSpace(p)));
                    else keep.Add(token);
                }
                folders.AddRange(added);
                keep.Add("\"-" + mode + "=" + string.Join(";", folders.Distinct(StringComparer.OrdinalIgnoreCase)) + "\"");
                tokens = keep;
            }
            return string.Join(" ", tokens);
        }

        internal static string Parameters(string root, string appId, List<Item> items)
        {
            string parameters = WindowsGSM.Installer.SteamCMD.GetParameter(root, appId, false, false);
            string suffix = " +app_update " + appId + " +quit";
            if (parameters == null)
                throw new InvalidOperationException("Workshop login is unavailable. Check Steam credentials and the Windows account used to encrypt them.");
            if (!parameters.EndsWith(suffix, StringComparison.Ordinal))
                throw new InvalidOperationException("This WGSM host has an unsupported SteamCMD parameter format.");
            return "+@ShutdownOnFailedCommand 1 +@NoPromptForPassword 1 " + parameters.Substring(0, parameters.Length - suffix.Length)
                + string.Concat(items.Select(i => " +workshop_download_item " + appId + " " + i.Id + " validate")) + " +quit";
        }

        internal static async Task<List<Item>> Update(string serverRoot, string configRoot, string steamRoot, string appId,
            Func<string, string, Task<Tuple<int, string>>> runner = null)
        {
            var items = Read(Path.Combine(configRoot, "WorkshopMods.cfg"));
            string state = Path.Combine(configRoot, "workshop-state-" + appId);
            string pending = Path.Combine(state, "pending.txt");
            if (File.Exists(pending))
                throw new InvalidOperationException("An interrupted Workshop deployment needs recovery. See " + pending);
            if (items.Count == 0) return items;
            if (appId != "221100" && appId != "107410") throw new InvalidOperationException("Unsupported Workshop application.");
            NoLinks(serverRoot); NoLinks(configRoot); NoLinks(steamRoot);
            string exe = Path.Combine(steamRoot, "steamcmd.exe");
            if (!File.Exists(exe)) throw new FileNotFoundException("SteamCMD is missing. Install or update the server first.", exe);
            Directory.CreateDirectory(state);
            string cache = Path.Combine(state, "download");
            Directory.CreateDirectory(cache);
            NoLinks(state); NoLinks(cache);
            NoLinks(Path.Combine(steamRoot, "wgsm-workshop.lock"));
            NoLinks(Path.Combine(state, "update.lock"));
            // Shared across both plugins and WGSM instances using this SteamCMD directory.
            using (var cacheLock = new FileStream(Path.Combine(steamRoot, "wgsm-workshop.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            using (var serverLock = new FileStream(Path.Combine(state, "update.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                string parameters = Parameters(cache, appId, items);
                var result = await (runner == null ? Run(exe, parameters) : runner(exe, parameters));
                if (result.Item1 != 0) throw new InvalidOperationException("SteamCMD Workshop update failed. Check account access, Steam Guard and network connectivity.");
                var sources = new Dictionary<string, string>();
                foreach (var item in items)
                {
                    // Exit code zero also occurs on failed Workshop commands. Require an item-specific receipt.
                    if (!Regex.IsMatch(result.Item2, @"Success\.\s+Downloaded item " + item.Id + @"\s+to\s+" , RegexOptions.IgnoreCase))
                        throw new InvalidOperationException("SteamCMD did not confirm Workshop item " + item.Id + ". Server start was cancelled.");
                    var paths = new[] { cache, steamRoot }.Select(root => Path.Combine(root, "steamapps", "workshop", "content", appId, item.Id));
                    string source = paths.FirstOrDefault(path => Directory.Exists(path)
                        && result.Item2.IndexOf(path, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (source == null) throw new DirectoryNotFoundException("Confirmed Workshop content not found for item " + item.Id + ".");
                    sources.Add(item.Id, source);
                }
                Deploy(serverRoot, state, appId, items, sources);
            }
            return items;
        }

        private static async Task<Tuple<int, string>> Run(string exe, string parameters)
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo(exe, parameters)
                {
                    WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true
                };
                process.Start();
                process.StandardInput.Close();
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                bool exited = await Task.Run(() => process.WaitForExit(30 * 60 * 1000));
                if (!exited)
                {
                    process.Kill();
                    await Task.Run(() => process.WaitForExit());
                    throw new TimeoutException("Workshop download timed out after 30 minutes. Server start was cancelled.");
                }
                // Keep Steam output in memory. Login details must not be copied into the server log.
                return Tuple.Create(process.ExitCode, await stdout + "\n" + await stderr);
            }
        }

        private static void NoLinks(string path)
        {
            for (string current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Workshop paths must not contain links or junctions: " + current);
        }

        private static void CopyTree(string source, string destination)
        {
            NoLinks(source); NoLinks(destination);
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source))
            {
                NoLinks(file);
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), false);
            }
            foreach (string directory in Directory.GetDirectories(source))
                CopyTree(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }

        private static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        // Back up every replaced folder/key. A failed batch is rolled back before starting the game.
        internal static void Deploy(string root, string state, string appId, List<Item> items, Dictionary<string, string> sources)
        {
            NoLinks(root); NoLinks(state);
            string pending = Path.Combine(state, "pending.txt");
            if (File.Exists(pending)) throw new IOException("Workshop recovery is pending: " + pending);
            Directory.CreateDirectory(state);
            string transaction = Path.Combine(root, ".wgsm-workshop-backups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
            string stage = Path.Combine(transaction, "prepared"), backup = Path.Combine(transaction, "previous");
            NoLinks(transaction);
            Directory.CreateDirectory(stage); Directory.CreateDirectory(backup);
            var changes = new List<Tuple<string, string>>();
            string manifest = Path.Combine(state, "keys.txt");
            NoLinks(manifest); NoLinks(pending);
            if (Directory.Exists(manifest) || Directory.Exists(pending)) throw new IOException("Workshop state path is a directory.");
            var oldKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(manifest))
                foreach (string line in File.ReadAllLines(manifest))
                {
                    string[] parts = line.Split('|');
                    if (parts.Length != 2 || !Regex.IsMatch(parts[0], @"\Awgsm_" + appId + @"_[1-9][0-9]*_[A-Za-z0-9_.-]+\.bikey\z")
                        || !Regex.IsMatch(parts[1], @"\A[0-9a-f]{64}\z") || oldKeys.ContainsKey(parts[0]))
                        throw new IOException("Invalid Workshop key inventory: " + manifest);
                    oldKeys.Add(parts[0], parts[1]);
                }
            var newKeys = new Dictionary<string, string>(oldKeys, StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                string source = sources[item.Id];
                string target = Path.Combine(root, item.Folder);
                NoLinks(target);
                if (File.Exists(target)) throw new IOException("Workshop folder is a file: " + target);
                string prepared = Path.Combine(stage, item.Folder);
                CopyTree(source, prepared);
                if (!Directory.EnumerateFiles(prepared, "*.pbo", SearchOption.AllDirectories).Any())
                    throw new IOException("Workshop item " + item.Id + " has no PBO content. Collections and non-server items are not supported.");
                changes.Add(Tuple.Create(target, prepared));
                string prefix = "wgsm_" + appId + "_" + item.Id + "_";
                var itemKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string key in Directory.GetFiles(prepared, "*.bikey", SearchOption.AllDirectories))
                {
                    string name = prefix + Path.GetFileName(key);
                    if (!Regex.IsMatch(name, @"\Awgsm_" + appId + @"_[1-9][0-9]*_[A-Za-z0-9_.-]+\.bikey\z"))
                        throw new IOException("Unsupported Workshop key filename: " + Path.GetFileName(key));
                    if (itemKeys.ContainsKey(name) && Hash(itemKeys[name]) != Hash(key))
                        throw new IOException("Conflicting Workshop keys: " + name);
                    itemKeys[name] = key;
                }
                foreach (string name in oldKeys.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Union(itemKeys.Keys, StringComparer.OrdinalIgnoreCase))
                {
                    string keyTarget = Path.Combine(root, "keys", name);
                    NoLinks(keyTarget);
                    if (Directory.Exists(keyTarget)) throw new IOException("Workshop key path is a directory: " + name);
                    if (File.Exists(keyTarget) && (!oldKeys.ContainsKey(name) || Hash(keyTarget) != oldKeys[name]))
                        throw new IOException("Workshop key was changed or is not owned by WGSM: " + name);
                    string keyPrepared = null;
                    if (itemKeys.ContainsKey(name))
                    {
                        keyPrepared = Path.Combine(stage, name);
                        File.Copy(itemKeys[name], keyPrepared);
                        newKeys[name] = Hash(keyPrepared);
                    }
                    else newKeys.Remove(name);
                    changes.Add(Tuple.Create(keyTarget, keyPrepared));
                }
            }
            string inventory = Path.Combine(stage, "keys.txt");
            File.WriteAllLines(inventory, newKeys.OrderBy(p => p.Key).Select(p => p.Key + "|" + p.Value), new UTF8Encoding(false));
            changes.Add(Tuple.Create(manifest, inventory));
            File.WriteAllText(pending, "Workshop deployment interrupted. Backup/prepared files: " + transaction + "\n"
                + string.Join("\n", changes.Select(c => c.Item1)) + "\n", new UTF8Encoding(false));
            var moved = new List<Tuple<string, string, bool>>();
            try
            {
                foreach (var change in changes)
                {
                    NoLinks(change.Item1);
                    string saved = Path.Combine(backup, moved.Count.ToString());
                    bool existed = File.Exists(change.Item1) || Directory.Exists(change.Item1);
                    if (existed) Move(change.Item1, saved);
                    moved.Add(Tuple.Create(change.Item1, saved, existed));
                    if (change.Item2 != null)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(change.Item1));
                        Move(change.Item2, change.Item1);
                    }
                }
                File.Delete(pending);
            }
            catch
            {
                bool restored = true;
                foreach (var change in moved.AsEnumerable().Reverse())
                {
                    try
                    {
                        if (Directory.Exists(change.Item1)) Directory.Delete(change.Item1, true);
                        else if (File.Exists(change.Item1)) File.Delete(change.Item1);
                        if (change.Item3) Move(change.Item2, change.Item1);
                    }
                    catch { restored = false; }
                }
                if (restored) File.Delete(pending);
                throw new IOException("Workshop activation failed. " + (restored ? "Previous files were restored." : "Recovery required: " + pending));
            }
        }

        private static void Move(string source, string destination)
        {
            if (Directory.Exists(source)) Directory.Move(source, destination);
            else File.Move(source, destination);
        }
    }
}
