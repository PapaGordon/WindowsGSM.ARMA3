<p align="center">
  <img src="ARMA3.cs/ARMA3.png" alt="Arma 3" width="160">
</p>

<h1 align="center">WindowsGSM.ARMA3</h1>

<p align="center">
  MeFriendos build for running an Arma 3 Dedicated Server with WindowsGSM.
</p>

<p align="center">
  <a href="https://github.com/Raziel7893/WindowsGSM/releases/tag/v1.25.2.1"><img src="https://img.shields.io/badge/WindowsGSM-Raziel%20v1.25.2.1-38CDD4" alt="Raziel WindowsGSM v1.25.2.1"></a>
  <a href="https://github.com/PapaGordon/WindowsGSM.ARMA3-MeFriendos/releases/tag/v0.1.2"><img src="https://img.shields.io/badge/version-0.1.2-9EFF99" alt="Version 0.1.2"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="MIT License"></a>
</p>

This plugin installs, updates and runs the Arma 3 Dedicated Server through SteamCMD. The MeFriendos build keeps the familiar WindowsGSM workflow while adding 64-bit server startup, a safer firewall policy, a read-only RPT-backed embedded console and reliable native **Toggle Console** handling.

## Features

- Installs and updates the official Arma 3 Dedicated Server through SteamCMD.
- Starts `arma3server_x64.exe`.
- Mirrors the active Arma 3 `.rpt` log into the WindowsGSM embedded console.
- Keeps Embedded Console and native Toggle Console as separate features.
- Resolves the native Arma window through multiple Win32 fallback paths.
- Monitors the resolved native window for the lifetime of the running Arma process.
- Synchronizes the resolved HWND with WindowsGSM's `ServerMetadata.MainWindow` and `windowsIntPtr` cache.
- Integrates with Raziel WindowsGSM's persistent `ShowConsole` state so Toggle Console can show and hide the correct window.
- Uses reflection for `ShowConsole`, retaining a compatibility path for WindowsGSM builds without that field.
- Writes Toggle Console diagnostics to `arma3-toggle-console.log` in the server cache folder.
- Tries a normal console-window close before falling back to process termination.
- Removes WindowsGSM's automatic application-wide firewall exception before the server starts listening.
- Leaves targeted manual firewall rules unchanged.
- Uses a `100`-port WindowsGSM instance increment for safer multi-instance layouts.
- Supports custom startup parameters for mods, server mods, profiles and custom configs.

## Quick overview

| Setting | Value |
| --- | --- |
| Plugin version | `0.1.2` |
| Tested WindowsGSM build | `Raziel7893/WindowsGSM v1.25.2.1` |
| SteamCMD App ID | `233780` |
| Start executable | `arma3server_x64.exe` |
| Default game port | `2302/UDP` |
| Default query port | `2303/UDP` |
| Default WindowsGSM port increment | `100` |
| Installation login | Steam account |
| Default parameters | `-profiles=ArmaHosts -config=server.cfg` |
| Firewall ports | Manual configuration only |
| Embedded console | Read-only RPT mirror |
| Toggle Console | Native Arma / classic console-host window |

## Requirements

- Raziel7893/WindowsGSM `v1.25.2.1` or a compatible WindowsGSM build
- Windows x64
- Administrator rights for WindowsGSM when the firewall safety check is enabled
- A Steam account usable by SteamCMD for installation and updates
- .NET 8 Desktop Runtime when using Raziel WindowsGSM v1.25.2.1

The Arma 3 Dedicated Server package uses Steam App ID `233780`.

## Installation

1. Download the latest release.
2. Extract the complete `ARMA3.cs` folder into `<WindowsGSM>\plugins\`.
3. Click **Reload Plugins** or restart WindowsGSM.
4. Add **Arma 3 Dedicated Server** in WindowsGSM.
5. Enter the Steam credentials requested by WindowsGSM and click **Install**.
6. Configure `server.cfg`, your profile and any `-mod` / `-serverMod` parameters.
7. Create only the required manual firewall rules.
8. Start the server.

## Updating Arma 3

1. Stop the server.
2. Back up `server.cfg`, profiles, missions and locally managed server files.
3. Click **Update** in WindowsGSM.
4. Start the server and review the RPT / embedded-console output.

SteamCMD updates the dedicated-server files. The plugin does not intentionally rewrite your mission, Altis Life configuration, mod list, `server.cfg` or profile files.

## Console behavior

WindowsGSM exposes two separate console-related features for this plugin:

- **Embed Console** shows read-only Arma server output inside WindowsGSM by following the active `.rpt` file.
- **Toggle Console** shows or hides the native Arma/classic console-host window selected through WindowsGSM's stored HWND.

### Embedded console

Arma 3 Dedicated Server on Windows does not provide dependable server output through redirected `stdout` / `stderr`. The plugin therefore leaves the Arma process unredirected and follows the active `.rpt` log instead.

When **Embed Console** is enabled, the plugin:

- resolves relative, absolute and quoted `-profiles=` paths;
- falls back to `%LOCALAPPDATA%\Arma 3` when no `-profiles=` parameter is configured;
- watches the profile root and direct profile subfolders for the active Arma RPT file;
- mirrors newly written RPT lines into the WindowsGSM console;
- avoids deliberately attaching to an unchanged RPT left behind by a previous run;
- stops following the file when the associated Arma process exits;
- reports a clear message if `-noLogs` disables the RPT source.

This console is intentionally **read-only**. Use supported in-game `#` admin commands or BattlEye RCon for administration.

### Native Toggle Console

Arma's native console window can become available after the server process starts, which may leave WindowsGSM with a missing or stale window handle. Version `0.1.2` resolves and keeps that handle synchronized.

The implementation:

1. Calls `Process.Refresh()` before reading `Process.MainWindowHandle`.
2. Rejects unsuitable cached helper-window handles.
3. Enumerates top-level windows owned by the Arma PID when necessary.
4. Prefers `ConsoleWindowClass`; otherwise only a visible process-owned fallback window is accepted.
5. Uses `AttachConsole()` / `GetConsoleWindow()` as an additional console-specific fallback.
6. Rejects `PseudoConsoleWindow` as an unsafe native Toggle Console target.
7. Writes a valid target into the matching WindowsGSM `ServerMetadata.MainWindow` and `windowsIntPtr` cache.
8. Continues monitoring while the Arma process is alive so a stale or replaced HWND can be repaired.
9. Detects Raziel WindowsGSM's `ShowConsole` state through reflection.
10. Applies `ShowNormal` or `Hide` to the correctly resolved HWND when Toggle Console changes that state.

The Toggle Console implementation has been tested successfully with **Raziel7893/WindowsGSM v1.25.2.1** and a real Arma 3 Dedicated Server.

### Toggle Console diagnostics

The monitor creates:

```text
<WindowsGSM>\servers\<server-id>\cache\arma3-toggle-console.log
```

The log records the Arma PID, WindowsGSM version, window-discovery path, HWND changes, `ShowConsole` synchronization and `AttachConsole` errors.

## Profiles and server name

For compatibility with the original plugin, the WindowsGSM **Server Name** is passed to Arma as the `-name` parameter. In Arma, `-name` selects the **profile name**; it is not the public browser hostname.

Set the public server name in `server.cfg`, for example:

```cpp
hostname = "MeFriendos Altis Life";
```

The default `-profiles=ArmaHosts` parameter keeps profile/log data below the server installation. You can replace it with another relative or absolute path in WindowsGSM's additional parameters when required.

If you use `-noLogs`, Arma does not create the RPT source required for the embedded console mirror. This does not disable native Toggle Console handling.

## Security: automatic port opening is disabled

WindowsGSM normally creates an application firewall exception for the configured start executable before calling a plugin's `Start()` method. This build removes the automatic exception for this server's exact `arma3server_x64.exe` path through the Windows Firewall API before launching the server and verifies that the exception is gone.

The plugin does **not** create game-port rules and does not remove manually configured port-specific rules.

For the default Arma 3 port layout:

| Port | Protocol | Purpose |
| --- | --- | --- |
| `2302` | UDP | Game traffic / VON |
| `2303` | UDP | Steam query (`+1`) |
| `2304` | UDP | Steam master (`+2`) |
| `2305` | UDP | VON allocation (`+3`) |
| `2306` | UDP | BattlEye (`+4`) |

When the game port changes, the related ports move with the same offsets. The plugin uses a `100`-port WindowsGSM increment, so subsequent default instances become `2402`, `2502`, and so on.

## Recommended server.cfg hardening

The plugin deliberately does not overwrite `server.cfg`. For a normal public modded server, review at least:

```cpp
BattlEye = 1;
verifySignatures = 2;
allowedFilePatching = 0;
upnp = 0;
```

`allowedFilePatching = 1` can be required for certain Headless Client setups, so do not change it blindly on an existing server.

## Stop behavior

The stop action refreshes the process and first tries the normal process-window close path. If no usable process window is exposed, the plugin may resolve a classic console window and post `WM_CLOSE` to it. A `PseudoConsoleWindow` is deliberately not closed by this fallback. The plugin waits up to 20 seconds after a successful normal close request before falling back to process termination.

## Mods and Altis Life

Use WindowsGSM's additional startup parameters for your existing setup, for example:

```text
-mod=@CBA_A3;@YourClientMod -serverMod=@YourServerMod
```

The plugin does not scan, download, reorder or modify Arma mods. Existing Altis Life and custom server-side setups remain under administrator control.

## Troubleshooting

### Toggle Console does nothing

1. Confirm WindowsGSM loaded plugin version `0.1.2` or newer.
2. Fully restart the Arma server after replacing or reloading the plugin.
3. Wait until WindowsGSM reports the server as started.
4. Try **Toggle Console** twice: show, then hide.
5. Inspect `<WindowsGSM>\servers\<server-id>\cache\arma3-toggle-console.log`.

Useful log entries include `Resolved HWND`, `Detected WindowsGSM ShowConsole state support`, `Applied ShowConsole=True` and `Applied ShowConsole=False`.

### Embedded console stays on "Waiting for Arma 3 RPT output"

Check the effective `-profiles=` parameter and verify that Arma writes an `.rpt` file there or in a direct profile subfolder. Also make sure `-noLogs` is not enabled.

### Players cannot connect

Verify the complete UDP port group derived from the configured game port, router/NAT forwarding and any provider firewall. With the default port, check `2302-2306/UDP`.

### The server appears with the wrong public name

Change `hostname` in `server.cfg`. WindowsGSM's Server Name is used as the Arma profile name for compatibility and does not control the public hostname.

## Compatibility

Version `0.1.2` has been tested with:

- **WindowsGSM:** Raziel7893 v1.25.2.1
- **Arma 3:** `arma3server_x64.exe`
- **Embedded Console:** RPT mirror
- **Toggle Console:** show and hide

## Project links

- [Latest release](https://github.com/PapaGordon/WindowsGSM.ARMA3-MeFriendos/releases/latest)
- [Source](https://github.com/PapaGordon/WindowsGSM.ARMA3-MeFriendos)
- [Raziel7893/WindowsGSM v1.25.2.1](https://github.com/Raziel7893/WindowsGSM/releases/tag/v1.25.2.1)
- [Original plugin](https://github.com/BattlefieldDuck/WindowsGSM.ARMA3)
- [64-bit fork reference](https://github.com/MildlyInterested/WindowsGSM.ARMA3)
- [Original WindowsGSM project](https://github.com/WindowsGSM/WindowsGSM)
- [Arma 3 Dedicated Server documentation](https://community.bohemia.net/wiki/Arma_3:_Dedicated_Server)
- [MeFriendos](https://mefriendos.de)

This is an independent community plugin. It is not affiliated with or endorsed by Bohemia Interactive or WindowsGSM.

## License

Maintained by Brandon Hanke (PapaGordon), owner of MeFriendos.de.

Based on the original MIT-licensed WindowsGSM.ARMA3 plugin. The original copyright and MIT license notice are retained in [LICENSE](LICENSE).

You can use, modify and redistribute this plugin, including commercially. Keep the copyright and license notices with any copies or substantial portions of the code.
