# Changelog

## 0.1.3 - 2026-10-22

- Removed plugin firewall cleanup and its startup block. Firewall management now follows WindowsGSM's per-server Auto Firewall setting.
- Updated setup and firewall notes for WindowsGSM builds with Auto Firewall.

## 0.1.2 — 2026-09-10

- Fixed WindowsGSM **Toggle Console** handling for Arma 3 Dedicated Server.
- Added refreshed native window-handle detection instead of relying on an early cached `Process.MainWindowHandle` value.
- Added top-level window discovery with `EnumWindows` / `GetWindowThreadProcessId` for windows owned by the running Arma process.
- Prefers `ConsoleWindowClass` and otherwise only accepts a visible process-owned fallback window.
- Added `AttachConsole` / `GetConsoleWindow` as an additional console-specific fallback.
- Rejects `PseudoConsoleWindow` as a native Toggle Console target.
- Continuously monitors the resolved native window for the lifetime of the Arma process so stale or replaced handles can be repaired.
- Synchronizes the resolved HWND with WindowsGSM `ServerMetadata.MainWindow` and the server's `windowsIntPtr` cache.
- Added support for Raziel WindowsGSM's persistent `ShowConsole` state and applies the requested show/hide state directly to the resolved native Arma window.
- Uses reflection for `ShowConsole` to retain compatibility with WindowsGSM builds that do not expose that field.
- Added `arma3-toggle-console.log` in the WindowsGSM server cache folder for native window diagnostics.
- Keeps the read-only RPT-based Embedded Console implementation introduced in `0.1.1`.
- Keeps graceful-stop handling conservative: a normal process-window close is attempted first, a classic console window may receive `WM_CLOSE`, and pseudo-terminal windows are not deliberately closed.
- Updated documentation for Raziel7893/WindowsGSM `v1.25.1.22`.
- Runtime-tested successfully with Raziel7893/WindowsGSM `v1.25.1.22` and a real Arma 3 Dedicated Server. Toggle Console show/hide and normal server operation passed.

## 0.1.1 — 2026-09-10

- Fixed the embedded console on Windows. Arma 3 Dedicated Server does not reliably expose its normal server log through redirected `stdout` / `stderr`.
- Replaced the non-working standard-stream redirect with a read-only live mirror of the active Arma `.rpt` file.
- Uses WindowsGSM's runtime `AllowsEmbedConsole` state instead of reading a stale configuration value inside `Start()`.
- Resolves relative, absolute and quoted `-profiles=` paths and falls back to `%LOCALAPPDATA%\Arma 3` when no profiles path is configured.
- Searches the configured profile root and direct profile subfolders for Arma RPT files.
- Takes an RPT snapshot before startup so quick restarts do not deliberately attach to an unchanged log from the previous process.
- Detects `-noLogs` and reports that the embedded console has no RPT source without blocking normal server startup.
- Keeps the native Arma process unredirected so WindowsGSM can retain normal window handling and the graceful-close fallback.
- Rechecked the existing `arma3server_x64.exe`, SteamCMD, firewall-cleanup and `100`-port increment behavior.
- Updated README and installation documentation to describe the RPT-based console implementation.
- Runtime-tested the RPT-based embedded console with WindowsGSM `v1.25.1.21`.

## 0.1.0 — 2026-09-10

- Created the MeFriendos build based on WindowsGSM.ARMA3 by BattlefieldDuck.
- Uses the 64-bit `arma3server_x64.exe` dedicated-server executable.
- Added an initial read-only embedded-console implementation using Arma 3 stdout and stderr. This approach was replaced in `0.1.1` because it does not reliably provide Arma server output on Windows.
- Keeps the native server console available so WindowsGSM can request a normal window close before falling back to process termination.
- Replaced immediate-only process termination with graceful-close-first shutdown behavior.
- Removes WindowsGSM's automatic firewall application exception for the exact Arma 3 server executable before launch.
- Verifies that the automatic exception is gone before allowing the server process to start.
- Keeps manually configured port rules unchanged and does not open game ports automatically.
- Changed the WindowsGSM port allocation increment to `100` for safer multi-instance layouts.
- Added installation, security, port, troubleshooting and testing documentation.
