<p align="center">
  <img src="ARMA3.cs/ARMA3.png" alt="Arma 3" width="160">
</p>

<h1 align="center">WindowsGSM.ARMA3</h1>

<p align="center">
  Run an Arma 3 dedicated server with WindowsGSM.
</p>

<p align="center">
  <a href="https://github.com/PapaGordon/WindowsGSM/releases/latest"><img src="https://img.shields.io/badge/WindowsGSM-MeFriendos%20latest-38CDD4" alt="Latest MeFriendos WindowsGSM"></a>
  <a href="CHANGELOG.md"><img src="https://img.shields.io/badge/version-0.1.5-9EFF99" alt="Version 0.1.5"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="MIT License"></a>
</p>

This plugin installs and updates the Arma 3 dedicated server through SteamCMD and runs the 64-bit server from WindowsGSM. It shows the server log in WindowsGSM, makes **Toggle Console** work reliably and can keep a list of Workshop mods up to date. It's based on the original [WindowsGSM.ARMA3](https://github.com/BattlefieldDuck/WindowsGSM.ARMA3) by BattlefieldDuck.

**Using our [MeFriendos WindowsGSM](https://github.com/PapaGordon/WindowsGSM)?** Then you don't need this plugin. Arma 3 is built in there, with the same features. Just pick **Arma 3 Dedicated Server** under **Install**. This plugin is for other WindowsGSM builds.

## What it does

- Installs and updates the Arma 3 server (Steam app `233780`) with your Steam account.
- Starts `arma3server_x64.exe`.
- Shows the server log (RPT) in **Embed Console**.
- Finds Arma's own console window, so **Toggle Console** shows and hides the right window.
- Can keep a list of Workshop mods up to date before every start. [Workshop mods](WORKSHOP.md)
- Asks Arma to close cleanly before WindowsGSM ends the process.
- Leaves your `server.cfg`, profiles, missions and start parameters alone.

## What you need

- 64-bit Windows and a WindowsGSM build with the `SteamCMDAgent` plugin API. We test with the current [MeFriendos WindowsGSM](https://github.com/PapaGordon/WindowsGSM/releases/latest) (v1.25.4.9).
- A Steam account saved in WindowsGSM that can download the Arma 3 server.

The plugin signs in through WindowsGSM's own SteamCMD login. Encrypted Steam passwords (`steamPassEncrypted`) need MeFriendos WindowsGSM v1.25.4.2 or newer, and WindowsGSM has to run as the Windows user that encrypted the password. [Steam account and password](https://github.com/PapaGordon/WindowsGSM/blob/main/docs/operations/steam-credentials.md) Other builds only read the plain `steamPass` field. Updating this plugin can't change that.

## Install

1. Download the ZIP from the [latest release](https://github.com/PapaGordon/WindowsGSM.ARMA3/releases/latest).
2. Copy the whole `ARMA3.cs` folder into `<WindowsGSM>\plugins\`.
3. Click **Reload Plugins** or restart WindowsGSM.
4. Pick **Arma 3 Dedicated Server [ARMA3.cs]** and click **Install**.
5. Set up `server.cfg`, your profile and your mods (see below).
6. Open the ports in the Windows firewall and on your router (see [Ports and firewall](#ports-and-firewall)).
7. Start the server.

## Settings

| Setting | Default | Notes |
| --- | --- | --- |
| Game port | `2302` (UDP) | Passed to Arma as `-port`. The other ports follow from it. |
| Query port | `2303` (UDP) | Always game port + 1 in Arma. WindowsGSM uses it to show the server status. |
| Server Name | | Passed as `-name`. In Arma that's the **profile** name, not the name in the server browser. |
| Start parameters | `-profiles=ArmaHosts -config=server.cfg` | Add your mods and other Arma parameters here. |

**Max Players** and **Map** in WindowsGSM are not passed to Arma. Set the player limit, missions and the public server name in `server.cfg`:

```cpp
hostname = "MeFriendos Altis Life";
maxPlayers = 64;
```

`-profiles=ArmaHosts` keeps profiles and logs inside the server folder. You can point it somewhere else, with a relative or absolute path. Each new Arma server in WindowsGSM gets ports 100 higher than the previous one (`2302`, `2402`, `2502`, ...).

## Mods

Add the mod folders to the start parameters, separated by semicolons:

```text
-profiles=ArmaHosts -config=server.cfg -mod=@CBA_A3;@YourClientMod -serverMod=@YourServerMod
```

You can install the mods yourself or let the plugin download them from the Workshop before every start. Mods from the Workshop list are added to `-mod` and `-serverMod` automatically. [Workshop mods](WORKSHOP.md)

## Updating the server

1. Stop the server.
2. Back up `server.cfg`, profiles, missions and anything else you changed in the server folder.
3. Click **Update** in WindowsGSM.
4. Start the server and check the log.

SteamCMD only updates the Arma server files. The plugin doesn't touch your missions, Altis Life setup, mods, `server.cfg` or profiles.

## Console

WindowsGSM offers two separate things here:

- **Embed Console** shows Arma's output inside WindowsGSM. Arma doesn't send its output to WindowsGSM directly, so the plugin follows the current RPT log in your profile folder instead. Without `-profiles` it looks in `%LOCALAPPDATA%\Arma 3`. Older logs from the previous run are skipped. The console only shows output, it doesn't take commands. Use in-game `#` admin commands or BattlEye RCon to manage the server.
- **Toggle Console** shows or hides Arma's own console window.

With `-noLogs`, Arma writes no RPT log, so **Embed Console** stays empty. **Toggle Console** still works.

**Stop** first asks Arma's window to close and waits up to 20 seconds. Only after that does WindowsGSM end the process.

## Ports and firewall

The plugin doesn't create or remove firewall rules. Use WindowsGSM's **Auto Firewall** if your build has it, or open the ports yourself. Router port forwarding is always up to you.

With the default game port, Arma uses these UDP ports:

| Port | Used for |
| --- | --- |
| `2302` | Game and voice |
| `2303` | Steam query |
| `2304` | Steam master server |
| `2305` | Voice |
| `2306` | BattlEye |

If you change the game port, the others move with it.

## Recommended server.cfg settings

The plugin never changes `server.cfg`. For a public server with mods, check at least these:

```cpp
BattlEye = 1;
verifySignatures = 2;
allowedFilePatching = 0;
upnp = 0;
```

Some headless client setups need `allowedFilePatching = 1`, so don't change that on a running server without checking first. Use strong admin passwords.

## Troubleshooting

**Toggle Console does nothing.** Restart the Arma server completely after updating or reloading the plugin, wait until WindowsGSM shows it as started and try again. The plugin writes what it found to `servers\<server ID>\cache\arma3-toggle-console.log`. Look for lines like `Resolved HWND` and `Applied ShowConsole=True`.

**Embed Console keeps waiting for RPT output.** Check `-profiles` and make sure Arma writes an `.rpt` file there or in a folder directly below it. Remove `-noLogs`.

**The server has the wrong name in the browser.** Change `hostname` in `server.cfg`. The Server Name in WindowsGSM is only the profile name.

**Players can't connect.** Check all five UDP ports from the game port on (by default `2302` to `2306`), your router forwarding and any firewall at your hosting provider.

**A Workshop update is missing.** Look for a `[Notice]` line in the WindowsGSM log. [Workshop mods](WORKSHOP.md#if-something-goes-wrong)

## How Toggle Console finds the window

Arma's console window often appears only after the process has started, so the handle WindowsGSM saved at start can be missing or wrong. While the server runs, the plugin keeps looking for the right window: it refreshes the process, checks the windows that belong to the Arma process (preferring a real console window), and tries `AttachConsole` as a last resort. Windows Terminal's `PseudoConsoleWindow` is never used. The handle it finds is saved where WindowsGSM expects it, and the plugin follows WindowsGSM's show/hide state. Builds without that state still work.

## Links

- [Releases](https://github.com/PapaGordon/WindowsGSM.ARMA3/releases)
- [MeFriendos WindowsGSM](https://github.com/PapaGordon/WindowsGSM)
- [Original plugin by BattlefieldDuck](https://github.com/BattlefieldDuck/WindowsGSM.ARMA3)
- [64-bit fork by MildlyInterested](https://github.com/MildlyInterested/WindowsGSM.ARMA3), which we compared against
- [Arma 3 dedicated server documentation](https://community.bohemia.net/wiki/Arma_3:_Dedicated_Server)
- [MeFriendos](https://mefriendos.de)

This is a community plugin. It's not affiliated with or endorsed by Bohemia Interactive or the WindowsGSM project.

## License

Based on the original MIT-licensed WindowsGSM.ARMA3 plugin. The original copyright notice stays in [LICENSE](LICENSE). You can use, change and share the plugin, also commercially, as long as the copyright and license notice stay with it.
