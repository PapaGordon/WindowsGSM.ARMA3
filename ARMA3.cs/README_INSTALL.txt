WindowsGSM.ARMA3 0.1.5
======================

Runs an Arma 3 dedicated server (arma3server_x64.exe) with WindowsGSM.

If you use MeFriendos WindowsGSM, you don't need this plugin.
Arma 3 is already built in there.

Install
-------
1. Copy this whole ARMA3.cs folder into the "plugins" folder of WindowsGSM.
2. Click "Reload Plugins" or restart WindowsGSM.
3. Pick "Arma 3 Dedicated Server [ARMA3.cs]" and click Install.
   WindowsGSM needs a saved Steam account for this.
4. Set up server.cfg, your profile and your mods.
5. Open the UDP ports 2302 to 2306 in your firewall and on your router.
6. Start the server.

Good to know
------------
- Default start parameters: -profiles=ArmaHosts -config=server.cfg
- The Server Name in WindowsGSM is Arma's profile name. The name in the
  server browser comes from hostname= in server.cfg, the player limit
  from maxPlayers= in server.cfg.
- Embed Console shows the server log (RPT). It doesn't work with -noLogs.
- Toggle Console shows or hides Arma's own window. If it doesn't work, see
  servers\<server ID>\cache\arma3-toggle-console.log
- Workshop mods can be updated automatically before every start.
  See WORKSHOP.md.
- The plugin never changes server.cfg, profiles, missions or your mods
  outside the Workshop list.

Full guide: https://github.com/PapaGordon/WindowsGSM.ARMA3

Based on WindowsGSM.ARMA3 by BattlefieldDuck. MeFriendos build by PapaGordon.
https://mefriendos.de
