# FishySteamworks
A Steamworks implementation for Fish-Networking.

Thank you [Heathen Engineering](https://github.com/sponsors/heathen-engineering) for your support.


## Dependencies

Fish-Networking https://github.com/FirstGearGames/FishNet

.Net 4.5x

These projects need to be installed and working before you can use this transport.
1. [SteamWorks.NET](https://github.com/rlabrecque/Steamworks.NET) FishySteamworks relies on Steamworks.NET to communicate with the Steamworks API(https://partner.steamgames.com/doc/sdk). [Installation guide](https://kb.heathenengineering.com/assets/steamworks/installation#install-steamworks.net) by Heathen Engineering: https://kb.heathenengineering.com/assets/steamworks/installation



## Setting Up

1. Add FishySteamworks component to your NetworkManager object. Either remove other transports or add TransportManager and specify which transport to use.

2. Enter your Steam App Id in the added FishySteamworks component. For testing you may use 480 (Space Wars) as the App Id.

3. Choose Peer to Peer to connect using the Steam relay.

4. You may need to add SteamManager to your NetworkManager object. Some frameworks such as Heathen Engineerings' use their own version of SteamManager. Please consult their discord here https://discord.gg/SGd4vkRdSe for more information on using their assets.
If you need a SteamManager to get started import FishNet\Plugins\FishySteamworks\SteamManager.unitypackage.



## Host
You may host as server only, or client and server in a single executable.



## Client
Clients connect to one-another by using the host's steamId64. You can get this information within your Steam application.
To do so open Steam, go to the View menu, Settings, Interface. Ensure 'Display web address bars when available' is checked.
Next view your profile within Steam. Your steamId64 is the large number at the end of your profile URL.

Connecting to a host is easy as putting the steamId64 in the 'Client Address' field of FishySteamworks and starting the client. You may instead start client by calling ClientManager.StartConnection(steamId64);



## Testing Two Builds Locally
Steam has limitations which prevent you from connecting to yourself locally over two builds. To do so, you must have two steam Ids, on two separate devices.
You however may run as server and client in a single executable. If you need to test using two builds(or editors) on a single device you will have to use the default transport.
