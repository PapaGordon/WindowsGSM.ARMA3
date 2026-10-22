VERSION 0.1.3

WindowsGSM.ARMA3 - MeFriendos build
====================================

Purpose
-------
WindowsGSM plugin for Arma 3 Dedicated Server using arma3server_x64.exe.

Primary WindowsGSM target
-------------------------
MeFriendos currently uses:
Raziel7893/WindowsGSM v1.25.2.1
https://github.com/Raziel7893/WindowsGSM/releases/tag/v1.25.2.1

Raziel v1.25.2.1 targets .NET 8 on Windows and requires the .NET 8 Desktop Runtime according to that WindowsGSM release.

Installation
------------
1. Download the latest plugin release.
2. Copy the complete ARMA3.cs folder into the WindowsGSM plugins folder.
3. Reload plugins or restart WindowsGSM.
4. Add "Arma 3 Dedicated Server" in WindowsGSM.
5. Install/update through SteamCMD with the Steam account requested by WindowsGSM.
6. Configure server.cfg, profiles, missions and mod startup parameters.
7. Configure only the required Arma UDP ports manually in Windows Firewall/router/provider firewall.
8. Start the server.

Default parameters
------------------
-profiles=ArmaHosts -config=server.cfg

WindowsGSM Server Name is passed to Arma as -name for compatibility with the original plugin.
In Arma, -name selects the profile name. The public server browser name must be set with hostname= in server.cfg.

Console behavior
----------------
WindowsGSM has two separate console features for this plugin:

1. Embed Console
   Read-only Arma output mirrored from the active .rpt file into WindowsGSM.

2. Toggle Console
   Shows or hides the native Arma/classic console-host window selected through WindowsGSM's stored HWND.

Embedded Console
----------------
Arma 3 Dedicated Server on Windows does not reliably provide its normal server log through redirected stdout/stderr. The plugin leaves the Arma process unredirected and mirrors the active .rpt log into the WindowsGSM console.

The plugin resolves -profiles= values with relative, absolute and quoted paths. If -profiles= is not configured, it falls back to %LOCALAPPDATA%\Arma 3. It checks the profile root and direct profile subfolders for the active Arma RPT file.

On a quick restart, the plugin snapshots existing RPT files before launch so it does not deliberately attach to an unchanged log from the previous process.

If -noLogs is enabled, the server can still start but there is no RPT source for the embedded console. WindowsGSM displays a clear message in that case.

Standard input is intentionally not redirected. Use in-game # admin commands or RCon for administration.

Toggle Console fix in 0.1.2
---------------------------
Version 0.1.2 improves native Toggle Console handling for Raziel7893/WindowsGSM v1.25.2.1.

The plugin:
- calls Process.Refresh() before trusting Process.MainWindowHandle;
- explicitly searches top-level windows belonging to the Arma PID;
- prefers ConsoleWindowClass and otherwise only accepts a visible process-owned fallback window;
- uses AttachConsole/GetConsoleWindow as an additional console fallback;
- rejects PseudoConsoleWindow as an unsafe native toggle target;
- writes a valid result to WindowsGSM ServerMetadata.MainWindow and windowsIntPtr;
- continues monitoring while the Arma process is alive;
- detects Raziel's ShowConsole state through reflection;
- directly applies ShowNormal/Hide to the correctly resolved HWND when ShowConsole changes;
- creates arma3-toggle-console.log in the server cache directory for diagnostics.

The Toggle Console implementation has been tested successfully with Raziel7893/WindowsGSM v1.25.2.1 and a real Arma 3 Dedicated Server.

Diagnostic file
---------------
For troubleshooting, check:

<WindowsGSM>\servers\<server-id>\cache\arma3-toggle-console.log

Useful entries include:
- Toggle Console monitor started for PID ...; WindowsGSM ...
- Resolved HWND 0x... via ...
- WindowsGSM MainWindow updated from 0x... to 0x...
- Detected WindowsGSM ShowConsole state support
- Applied ShowConsole=True directly to HWND 0x...
- Applied ShowConsole=False directly to HWND 0x...

If no usable target is found, the log also records AttachConsole errors where available.

Stop behavior
-------------
The plugin refreshes the process and first tries the normal process-window close path.
If that is unavailable, a classic console window may receive WM_CLOSE.
PseudoConsoleWindow/terminal-host windows are deliberately not closed by this fallback.
The plugin waits up to 20 seconds after a successful close request before falling back to terminating the process.

Firewall behavior
-----------------
Firewall management follows WindowsGSM's per-server Auto Firewall setting.
The plugin does not create, remove or inspect firewall rules and does not block startup for a plugin firewall check.

Use a WindowsGSM build with Auto Firewall, such as https://github.com/PapaGordon/WindowsGSM.
For manual port rules, turn Auto Firewall off for this server in WindowsGSM and configure the required ports yourself. Router forwarding remains separate.

Default incoming ports
----------------------
2302/UDP - game traffic / VON
2303/UDP - Steam query
2304/UDP - Steam master
2305/UDP - VON allocation
2306/UDP - BattlEye

The ports move together when the base game port changes.
For multiple Arma instances, this plugin uses a WindowsGSM PortIncrements value of 100: 2302, 2402, 2502, etc.

Recommended server.cfg security review
--------------------------------------
BattlEye = 1;
verifySignatures = 2;
allowedFilePatching = 0;
upnp = 0;

Do not blindly change allowedFilePatching on a server that uses Headless Clients.
Use strong private administration passwords and mission/mod-specific BattlEye filters where appropriate.

Compatibility
-------------
- Plugin version: 0.1.3
Runtime checks below were performed with plugin 0.1.2.
- WindowsGSM: Raziel7893/WindowsGSM v1.25.2.1
- Arma process: arma3server_x64.exe
- Embedded Console: RPT mirror
- Toggle Console: show and hide tested successfully

Important
---------
- The plugin does not rewrite server.cfg or Arma3Profile files.
- The plugin does not manage your Altis Life mission or mod list.
- The plugin does not open game ports automatically.
- Existing custom startup parameters are passed through unchanged.
- Embedded Console is read-only.
- Toggle Console is separate from Embedded Console.
- -noLogs disables the RPT source used by Embedded Console, not native Toggle Console handling.
- Back up server configuration, profiles and missions before major updates.

Credits
-------
Based on WindowsGSM.ARMA3 by BattlefieldDuck.
64-bit implementation compared with the MildlyInterested fork.
Primary MeFriendos WindowsGSM environment: Raziel7893/WindowsGSM v1.25.2.1.
MeFriendos build by PapaGordon / MeFriendos.

https://github.com/PapaGordon/WindowsGSM.ARMA3-MeFriendos
https://github.com/PapaGordon/WindowsGSM.ARMA3-MeFriendos/releases/tag/v0.1.2
https://github.com/Raziel7893/WindowsGSM/releases/tag/v1.25.2.1
https://mefriendos.de
