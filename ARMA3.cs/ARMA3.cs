// Made by PapaGordon
using System;
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
            version = "0.1.3",
            url = "https://github.com/PapaGordon/WindowsGSM.ARMA3-MeFriendos",
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
