#if !FISHYSTEAMWORKS
using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Transporting;
using Steamworks;
using System;
using System.IO;
using UnityEngine;

namespace FishySteamworks
{
    public class FishySteamworks : Transport
    {
        ~FishySteamworks()
        {
            Shutdown();
        }

        #region Public.
        /// <summary>
        /// The SteamId for the local user after connecting to or starting the server. This is populated automatically.
        /// </summary>
        [System.NonSerialized]
        public ulong LocalUserSteamID = 0;
        #endregion

        #region Serialized.
        /// <summary>
        /// Steam application Id.
        /// </summary>
        [Tooltip("Steam application Id.")]
        [SerializeField]
        private ulong _steamAppID = 480;
        /// <summary>
        /// Address server should bind to.
        /// </summary>
        [Tooltip("Address server should bind to.")]
        [SerializeField]
        private string _serverBindAddress = string.Empty;
        /// <summary>
        /// Port to use.
        /// </summary>
        [Tooltip("Port to use.")]
        [SerializeField]
        private ushort _port = 7770;
        /// <summary>
        /// Maximum number of players which may be connected at once.
        /// </summary>
        [Tooltip("Maximum number of players which may be connected at once.")]
        [Range(1, ushort.MaxValue)]
        [SerializeField]
        private ushort _maximumClients = 9001;
        /// <summary>
        /// True if using peer to peer socket.
        /// </summary>
        [Tooltip("True if using peer to peer socket.")]
        [SerializeField]
        private bool _peerToPeer = false;
        /// <summary>
        /// Address client should connect to.
        /// </summary>
        [Tooltip("Address client should connect to.")]
        [SerializeField]
        private string _clientAddress = string.Empty;
        #endregion

        #region Private. 
        /// <summary>
        /// MTUs for each channel.
        /// </summary>
        private int[] _mtus;
        /// <summary>
        /// Client when acting as client only.
        /// </summary>
        private Client.ClientSocket _client;
        /// <summary>
        /// Client when acting as host.
        /// </summary>
        private Client.ClientHostSocket _clientHost;
        /// <summary>
        /// Server for the transport.
        /// </summary>
        private Server.ServerSocket _server;
        #endregion

        #region Const.
        /// <summary>
        /// Id to use for client when acting as host.
        /// </summary>
        internal const int CLIENT_HOST_ID = short.MaxValue;
        #endregion

        public override void Initialize(NetworkManager networkManager, int transportIndex)
        {
            base.Initialize(networkManager, transportIndex);

            _client = new Client.ClientSocket();
            _clientHost = new Client.ClientHostSocket();
            _server = new Server.ServerSocket();

            CreateChannelData();
            WriteSteamAppId();
            _client.Initialize(this);
            _clientHost.Initialize(this);
            _server.Initialize(this);
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void Update()
        {
            _clientHost.CheckSetStarted();
        }

        #region Setup.
        /// <summary>
        /// Creates ChannelData for the transport.
        /// </summary>
        private void CreateChannelData()
        {
            _mtus = new int[2]
            {
                1048576,
                1200
            };
        }
        /// <summary>
        /// Writes SteamAppId to file.
        /// </summary>
        private void WriteSteamAppId()
        {
            string fileName = "steam_appid.txt";
            string appIdText = _steamAppID.ToString();
            try
            {
                if (File.Exists(fileName))
                {
                    string content = File.ReadAllText(fileName);
                    if (content != appIdText)
                    {
                        File.WriteAllText(fileName, appIdText);
                        Debug.Log($"SteamId has been updated from {content} to {appIdText} within {fileName}.");
                    }
                }
                else
                {
                    File.WriteAllText(fileName, appIdText);
                    Debug.Log($"SteamId {appIdText} has been set within {fileName}.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"There was an exception when trying to write {appIdText} to {fileName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Tries to initialize steam network access.
        /// </summary>
        private bool InitializeRelayNetworkAccess()
        {
            try
            {
#if UNITY_SERVER
            SteamGameServerNetworkingUtils.InitRelayNetworkAccess();
#else
                SteamNetworkingUtils.InitRelayNetworkAccess();
                if (IsNetworkAccessAvailable())
                    LocalUserSteamID = SteamUser.GetSteamID().m_SteamID;
#endif
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns if network access is available.
        /// </summary>
        public bool IsNetworkAccessAvailable()
        {
            try
            {
#if UNITY_SERVER
                InteropHelp.TestIfAvailableGameServer();
#else
                InteropHelp.TestIfAvailableClient();
#endif
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region ConnectionStates.
        /// <summary>
        /// Gets the IP address of a remote connection Id.
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns></returns>
        public override string GetConnectionAddress(int connectionId)
        {
            return _server.GetConnectionAddress(connectionId);
        }
        /// <summary>
        /// Called when a connection state changes for the local client.
        /// </summary>
        public override event Action<ClientConnectionStateArgs> OnClientConnectionState;
        /// <summary>
        /// Called when a connection state changes for the local server.
        /// </summary>
        public override event Action<ServerConnectionStateArgs> OnServerConnectionState;
        /// <summary>
        /// Called when a connection state changes for a remote client.
        /// </summary>
        public override event Action<RemoteConnectionStateArgs> OnRemoteConnectionState;
        /// <summary>
        /// Gets the current local ConnectionState.
        /// </summary>
        /// <param name="server">True if getting ConnectionState for the server.</param>
        public override LocalConnectionState GetConnectionState(bool server)
        {
            if (server)
                return _server.GetLocalConnectionState();
            else
                return _client.GetLocalConnectionState();
        }
        /// <summary>
        /// Gets the current ConnectionState of a remote client on the server.
        /// </summary>
        /// <param name="connectionId">ConnectionId to get ConnectionState for.</param>
        public override RemoteConnectionState GetConnectionState(int connectionId)
        {
            return _server.GetConnectionState(connectionId);
        }
        /// <summary>
        /// Handles a ConnectionStateArgs for the local client.
        /// </summary>
        /// <param name="connectionStateArgs"></param>
        public override void HandleClientConnectionState(ClientConnectionStateArgs connectionStateArgs)
        {
            OnClientConnectionState?.Invoke(connectionStateArgs);
        }
        /// <summary>
        /// Handles a ConnectionStateArgs for the local server.
        /// </summary>
        /// <param name="connectionStateArgs"></param>
        public override void HandleServerConnectionState(ServerConnectionStateArgs connectionStateArgs)
        {
            OnServerConnectionState?.Invoke(connectionStateArgs);
        }
        /// <summary>
        /// Handles a ConnectionStateArgs for a remote client.
        /// </summary>
        /// <param name="connectionStateArgs"></param>
        public override void HandleRemoteConnectionState(RemoteConnectionStateArgs connectionStateArgs)
        {
            OnRemoteConnectionState?.Invoke(connectionStateArgs);
        }
        #endregion

        #region Iterating.
        /// <summary>
        /// Processes data received by the socket.
        /// </summary>
        /// <param name="server">True to process data received on the server.</param>
        public override void IterateIncoming(bool server)
        {
            if (server)
            {
                _server.IterateIncoming();
            }
            else
            {
                _client.IterateIncoming();
                _clientHost.IterateIncoming();
            }
        }

        /// <summary>
        /// Processes data to be sent by the socket.
        /// </summary>
        /// <param name="server">True to process data received on the server.</param>
        public override void IterateOutgoing(bool server)
        {
            if (server)
                _server.IterateOutgoing();
            else
                _client.IterateOutgoing();
        }
        #endregion

        #region ReceivedData.
        /// <summary>
        /// Called when client receives data.
        /// </summary>
        public override event Action<ClientReceivedDataArgs> OnClientReceivedData;
        /// <summary>
        /// Handles a ClientReceivedDataArgs.
        /// </summary>
        /// <param name="receivedDataArgs"></param>
        public override void HandleClientReceivedDataArgs(ClientReceivedDataArgs receivedDataArgs)
        {
            OnClientReceivedData?.Invoke(receivedDataArgs);
        }
        /// <summary>
        /// Called when server receives data.
        /// </summary>
        public override event Action<ServerReceivedDataArgs> OnServerReceivedData;
        /// <summary>
        /// Handles a ClientReceivedDataArgs.
        /// </summary>
        /// <param name="receivedDataArgs"></param>
        public override void HandleServerReceivedDataArgs(ServerReceivedDataArgs receivedDataArgs)
        {
            OnServerReceivedData?.Invoke(receivedDataArgs);
        }
        #endregion

        #region Sending.
        /// <summary>
        /// Sends to the server or all clients.
        /// </summary>
        /// <param name="channelId">Channel to use.</param>
        /// /// <param name="segment">Data to send.</param>
        public override void SendToServer(byte channelId, ArraySegment<byte> segment)
        {
            _client.SendToServer(channelId, segment);
            _clientHost.SendToServer(channelId, segment);
        }
        /// <summary>
        /// Sends data to a client.
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="segment"></param>
        /// <param name="connectionId"></param>
        public override void SendToClient(byte channelId, ArraySegment<byte> segment, int connectionId)
        {
            //uint tick = BitConverter.ToUInt32(segment.Array, 0);
            //ushort val = BitConverter.ToUInt16(segment.Array, 4);
            //Debug.Log(tick + ",  " + val);
            _server.SendToClient(channelId, segment, connectionId);
        }
        #endregion

        #region Configuration.
        /// <summary>
        /// Returns the maximum number of clients allowed to connect to the server. If the transport does not support this method the value -1 is returned.
        /// </summary>
        /// <returns></returns>
        public override int GetMaximumClients()
        {
            return _server.GetMaximumClients();
        }
        /// <summary>
        /// Sets maximum number of clients allowed to connect to the server. If applied at runtime and clients exceed this value existing clients will stay connected but new clients may not connect.
        /// </summary>
        /// <param name="value"></param>
        public override void SetMaximumClients(int value)
        {
            _server.SetMaximumClients(value);
        }
        /// <summary>
        /// Sets which address the client will connect to.
        /// </summary>
        /// <param name="address"></param>
        public override void SetClientAddress(string address)
        {
            _clientAddress = address;
        }
        /// <summary>
        /// Sets which address the server will bind to.
        /// </summary>
        /// <param name="address"></param>
        public override void SetServerBindAddress(string address, IPAddressType addressType)
        {
            _serverBindAddress = address;
        }
        /// <summary>
        /// Sets which port to use.
        /// </summary>
        /// <param name="port"></param>
        public override void SetPort(ushort port)
        {
            _port = port;
        }
        #endregion

        #region Start and stop.
        /// <summary>
        /// Starts the local server or client using configured settings.
        /// </summary>
        /// <param name="server">True to start server.</param>
        public override bool StartConnection(bool server)
        {
            if (server)
                return StartServer();
            else
                return StartClient(_clientAddress);
        }

        /// <summary>
        /// Stops the local server or client.
        /// </summary>
        /// <param name="server">True to stop server.</param>
        public override bool StopConnection(bool server)
        {
            if (server)
                return StopServer();
            else
                return StopClient();
        }

        /// <summary>
        /// Stops a remote client from the server, disconnecting the client.
        /// </summary>
        /// <param name="connectionId">ConnectionId of the client to disconnect.</param>
        /// <param name="immediately">True to abrutly stp the client socket without waiting socket thread.</param>
        public override bool StopConnection(int connectionId, bool immediately)
        {
            return StopClient(connectionId, immediately);
        }

        /// <summary>
        /// Stops both client and server.
        /// </summary>
        public override void Shutdown()
        {
            //Stops client then server connections.
            StopConnection(false);
            StopConnection(true);
        }

        #region Privates.
        /// <summary>
        /// Starts server.
        /// </summary>
        /// <returns>True if there were no blocks. A true response does not promise a socket will or has connected.</returns>
        private bool StartServer()
        {
            if (!InitializeRelayNetworkAccess())
            {
                if (NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError($"RelayNetworkAccess could not be initialized.");
                return false;
            }
            if (!IsNetworkAccessAvailable())
            {
                if (NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError("Server network access is not available.");
                return false;
            }
            _server.ResetInvalidSocket();
            if (_server.GetLocalConnectionState() != LocalConnectionState.Stopped)
            {
                if (NetworkManager.CanLog(LoggingType.Error))
                    Debug.LogError("Server is already running.");
                return false;
            }

            bool clientRunning = (_client.GetLocalConnectionState() != LocalConnectionState.Stopped);
            /* If remote _client is running then stop it
             * and start the client host variant. */
            if (clientRunning)
                _client.StopConnection();

            bool result = _server.StartConnection(_serverBindAddress, _port, _maximumClients, _peerToPeer);
            //If need to restart client.
            if (result && clientRunning)
                StartConnection(false);

            return result;
        }

        /// <summary>
        /// Stops server.
        /// </summary>
        private bool StopServer()
        {
            if (_server != null)
                return _server.StopConnection();

            return false;
        }

        /// <summary>
        /// Starts the client.
        /// </summary>
        /// <param name="address"></param>
        /// <returns>True if there were no blocks. A true response does not promise a socket will or has connected.</returns>
        private bool StartClient(string address)
        {
            //If not acting as a host.
            if (_server.GetLocalConnectionState() == LocalConnectionState.Stopped)
            {
                if (_client.GetLocalConnectionState() != LocalConnectionState.Stopped)
                {
                    if (NetworkManager.CanLog(LoggingType.Error))
                        Debug.LogError("Client is already running.");
                    return false;
                }
                //Stop client host if running.
                if (_clientHost.GetLocalConnectionState() != LocalConnectionState.Stopped)
                    _clientHost.StopConnection();
                //Initialize.
                if (!InitializeRelayNetworkAccess())
                {
                    if (NetworkManager.CanLog(LoggingType.Error))
                        Debug.LogError($"RelayNetworkAccess could not be initialized.");
                    return false;
                }
                if (!IsNetworkAccessAvailable())
                {
                    if (NetworkManager.CanLog(LoggingType.Error))
                        Debug.LogError("Client network access is not available.");
                    return false;
                }

                //SetUserSteamID();
                _client.StartConnection(address, _port, _peerToPeer);
            }
            //Acting as host.
            else
            {
                _clientHost.StartConnection(_server);
            }

            return true;
        }

        /// <summary>
        /// Stops the client.
        /// </summary>
        private bool StopClient()
        {
            bool result = false;
            if (_client != null)
                result |= _client.StopConnection();
            if (_clientHost != null)
                result |= _clientHost.StopConnection();
            return result;
        }

        /// <summary>
        /// Stops a remote client on the server.
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="immediately">True to abrutly stp the client socket without waiting socket thread.</param>
        private bool StopClient(int connectionId, bool immediately)
        {
            return _server.StopConnection(connectionId);
        }
        #endregion
        #endregion

        #region Channels.
        /// <summary>
        /// Gets the MTU for a channel. This should take header size into consideration.
        /// For example, if MTU is 1200 and a packet header for this channel is 10 in size, this method should return 1190.
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public override int GetMTU(byte channel)
        {
            if (channel >= _mtus.Length)
            {
                Debug.LogError($"Channel {channel} is out of bounds.");
                return 0;
            }

            return _mtus[channel];
        }
        #endregion

    }
}
#endif // !DISABLESTEAMWORKS