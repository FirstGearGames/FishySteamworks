#if !FISHYSTEAMWORKS
using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Transporting;
using FishySteamworks.Client;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishySteamworks.Server
{
    public class ServerSocket : CommonSocket
    {
        #region Types.
        public struct ConnectionChange
        {
            public int ConnectionId;
            public HSteamNetConnection SteamConnection;
            public CSteamID SteamId;

            public bool IsConnect => SteamId.IsValid();

            public ConnectionChange(int id)
            {
                ConnectionId = id;
                SteamId = CSteamID.Nil;
                SteamConnection = default;
            }

            public ConnectionChange(int id, HSteamNetConnection steamConnection, CSteamID steamId)
            {
                ConnectionId = id;
                SteamConnection = steamConnection;
                SteamId = steamId;
            }
        }
        #endregion

        #region Public.
        /// <summary>
        /// Gets the current ConnectionState of a remote client on the server.
        /// </summary>
        /// <param name="connectionId">ConnectionId to get ConnectionState for.</param>
        internal RemoteConnectionState GetConnectionState(int connectionId)
        {
            //Remote clients can only have Started or Stopped states since we cannot know in between.
            if (_steamConnections.Second.ContainsKey(connectionId))
                return RemoteConnectionState.Started;
            else
                return RemoteConnectionState.Stopped;
        }
        #endregion

        #region Private.
        /// <summary>
        /// SteamConnections for ConnectionIds.
        /// </summary>
        private BidirectionalDictionary<HSteamNetConnection, int> _steamConnections = new BidirectionalDictionary<HSteamNetConnection, int>();
        /// <summary>
        /// SteamIds for ConnectionIds.
        /// </summary>
        private BidirectionalDictionary<CSteamID, int> _steamIds = new BidirectionalDictionary<CSteamID, int>();
        /// <summary>
        /// Maximum number of remote connections.
        /// </summary>
        private int _maximumClients;
        /// <summary>
        /// Next Id to use for a connection.
        /// </summary>
        private int _nextConnectionId;
        /// <summary>
        /// Socket for the connection.
        /// </summary>
        private HSteamListenSocket _socket = new HSteamListenSocket(0);
        /// <summary>
        /// Packets received from local client.
        /// </summary>
        private Queue<LocalPacket> _clientHostIncoming = new Queue<LocalPacket>();
        /// <summary>
        /// Contains state of the client host. True is started, false is stopped.
        /// </summary>
        private bool _clientHostStarted = false;
        /// <summary>
        /// Called when a remote connection state changes.
        /// </summary>
        private Steamworks.Callback<SteamNetConnectionStatusChangedCallback_t> _onRemoteConnectionStateCallback;
        /// <summary>
        /// ConnectionIds which can be reused.
        /// </summary>
        private Queue<int> _cachedConnectionIds = new Queue<int>();
        /// <summary>
        /// Socket for client host. Will be null if not being used.
        /// </summary>
        private ClientHostSocket _clientHost;
        /// <summary>
        /// True if currently iterating steamConnections.
        /// </summary>
        private bool _iteratingConnections;
        /// <summary>
        /// Connection changes during iteration.
        /// </summary>
        private List<ConnectionChange> _pendingConnectionChanges = new();
        #endregion

        /// <summary>
        /// Resets the socket if invalid.
        /// </summary>
        internal void ResetInvalidSocket()
        {
            /* Force connection state to stopped if listener is invalid.
             * Not sure if steam may change this internally so better
             * safe than sorry and check before trying to connect
             * rather than being stuck in the incorrect state. */
            if (_socket == HSteamListenSocket.Invalid)
                base.SetLocalConnectionState(LocalConnectionState.Stopped, true);
        }

        /// <summary>
        /// Starts the server.
        /// </summary>
        internal bool StartConnection(string address, ushort port, int maximumClients, bool peerToPeer)
        {
            try
            {
                if (_onRemoteConnectionStateCallback == null)
                {
#if UNITY_SERVER
                    _onRemoteConnectionStateCallback = Callback<SteamNetConnectionStatusChangedCallback_t>.CreateGameServer(OnRemoteConnectionState);
#else
                    _onRemoteConnectionStateCallback = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnRemoteConnectionState);
#endif
                }

                base.PeerToPeer = peerToPeer;

                //If address is required then make sure it can be parsed.
                byte[] ip = (!peerToPeer) ? base.GetIPBytes(address) : null;

                base.PeerToPeer = peerToPeer;
                SetMaximumClients(maximumClients);
                _nextConnectionId = 0;
                _cachedConnectionIds.Clear();
                _iteratingConnections = false;

                base.SetLocalConnectionState(LocalConnectionState.Starting, true);
                SteamNetworkingConfigValue_t[] options = new SteamNetworkingConfigValue_t[] { };

                if (base.PeerToPeer)
                {
#if UNITY_SERVER
                    _socket = SteamGameServerNetworkingSockets.CreateListenSocketP2P(0, options.Length, options);
#else
                    _socket = SteamNetworkingSockets.CreateListenSocketP2P(0, options.Length, options);
#endif
                }
                else
                {
                    SteamNetworkingIPAddr addr = new SteamNetworkingIPAddr();
                    addr.Clear();
                    if (ip != null)
                        addr.SetIPv6(ip, port);
#if UNITY_SERVER
                    _socket = SteamGameServerNetworkingSockets.CreateListenSocketIP(ref addr, 0, options);
#else
                    _socket = SteamNetworkingSockets.CreateListenSocketIP(ref addr, 0, options);
#endif
                }
            }
            catch
            {
                base.SetLocalConnectionState(LocalConnectionState.Stopped, true);
                return false;
            }

            if (_socket == HSteamListenSocket.Invalid)
            {
                base.SetLocalConnectionState(LocalConnectionState.Stopped, true);
                return false;
            }
            else
            {
                base.SetLocalConnectionState(LocalConnectionState.Started, true);
                return true;
            }
        }


        /// <summary>
        /// Stops the local socket.
        /// </summary>
        internal bool StopConnection()
        {
            /* Try to close the socket before exiting early
             * We never want to leave sockets open. */
            if (_socket != HSteamListenSocket.Invalid)
            {
#if UNITY_SERVER
                SteamGameServerNetworkingSockets.CloseListenSocket(_socket);
#else
                SteamNetworkingSockets.CloseListenSocket(_socket);
#endif
                if (_onRemoteConnectionStateCallback != null)
                {
                    _onRemoteConnectionStateCallback.Dispose();
                    _onRemoteConnectionStateCallback = null;
                }

                _socket = HSteamListenSocket.Invalid;
            }

            _pendingConnectionChanges.Clear();
            if (base.GetLocalConnectionState() == LocalConnectionState.Stopped)
                return false;

            base.SetLocalConnectionState(LocalConnectionState.Stopping, true);
            base.SetLocalConnectionState(LocalConnectionState.Stopped, true);

            return true;
        }

        /// <summary>
        /// Stops a remote client from the server, disconnecting the client.
        /// </summary>
        /// <param name="connectionId">ConnectionId of the client to disconnect.</param>
        internal bool StopConnection(int connectionId)
        {
            if (connectionId == FishySteamworks.CLIENT_HOST_ID)
            {
                if (_clientHost != null)
                {
                    _clientHost.StopConnection();
                    return true;
                }
                else
                {
                    return false;
                }
            }
            //Remote client.
            else
            {
                if (_steamConnections.Second.TryGetValue(connectionId, out HSteamNetConnection steamConn))
                {
                    return StopConnection(connectionId, steamConn);
                }
                else
                {
                    base.Transport.NetworkManager.LogError($"Steam connection not found for connectionId {connectionId}.");
                    return false;
                }
            }
        }

        /// <summary>
        /// Stops a remote client from the server, disconnecting the client.
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="socket"></param>
        private bool StopConnection(int connectionId, HSteamNetConnection socket)
        {
#if UNITY_SERVER
            SteamGameServerNetworkingSockets.CloseConnection(socket, 0, string.Empty, false);
#else
            SteamNetworkingSockets.CloseConnection(socket, 0, string.Empty, false);
#endif
            if (!_iteratingConnections)
                RemoveConnection(connectionId);
            else
                _pendingConnectionChanges.Add(new ConnectionChange(connectionId));

            return true;
        }

        /// <summary>
        /// Called when a remote connection state changes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OnRemoteConnectionState(SteamNetConnectionStatusChangedCallback_t args)
        {
            ulong clientSteamID = args.m_info.m_identityRemote.GetSteamID64();
            if (args.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting)
            {
                if (_steamConnections.Count >= GetMaximumClients())
                {
                    base.Transport.NetworkManager.Log($"Incoming connection {clientSteamID} was rejected because would exceed the maximum connection count.");
#if UNITY_SERVER
                    SteamGameServerNetworkingSockets.CloseConnection(args.m_hConn, 0, "Max Connection Count", false);
#else
                    SteamNetworkingSockets.CloseConnection(args.m_hConn, 0, "Max Connection Count", false);
#endif
                    return;
                }

#if UNITY_SERVER
                EResult res = SteamGameServerNetworkingSockets.AcceptConnection(args.m_hConn);
#else
                EResult res = SteamNetworkingSockets.AcceptConnection(args.m_hConn);
#endif
                if (res == EResult.k_EResultOK)
                    base.Transport.NetworkManager.Log($"Accepting connection {clientSteamID}");
                else
                    base.Transport.NetworkManager.Log($"Connection {clientSteamID} could not be accepted: {res.ToString()}");
            }
            else if (args.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
            {
                int connectionId = (_cachedConnectionIds.Count > 0) ? _cachedConnectionIds.Dequeue() : _nextConnectionId++;
                if (!_iteratingConnections)
                    AddConnection(connectionId, args.m_hConn, args.m_info.m_identityRemote.GetSteamID());
                else
                    _pendingConnectionChanges.Add(new ConnectionChange(connectionId, args.m_hConn, args.m_info.m_identityRemote.GetSteamID()));
            }
            else if (args.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer || args.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally)
            {
                if (_steamConnections.TryGetValue(args.m_hConn, out int connId))
                {
                    StopConnection(connId, args.m_hConn);
                }
            }
            else
            {
                base.Transport.NetworkManager.Log($"Connection {clientSteamID} state changed: {args.m_info.m_eState.ToString()}");
            }
        }

        /// <summary>
        /// Adds a connection and invokes addition.
        /// </summary>
        private void AddConnection(int connectionId, HSteamNetConnection steamConnection, CSteamID steamId)
        {
            _steamConnections.Add(steamConnection, connectionId);
            _steamIds.Add(steamId, connectionId);
            base.Transport.NetworkManager.Log($"Client with SteamID {steamId.m_SteamID} connected. Assigning connection id {connectionId}");
            base.Transport.HandleRemoteConnectionState(new RemoteConnectionStateArgs(RemoteConnectionState.Started, connectionId, Transport.Index));
        }

        /// <summary>
        /// Removes a connection and invokes removal.
        /// </summary>
        private void RemoveConnection(int connectionId)
        {
            _steamConnections.Remove(connectionId);
            _steamIds.Remove(connectionId);

            base.Transport.NetworkManager.Log($"Client with ConnectionID {connectionId} disconnected.");
            base.Transport.HandleRemoteConnectionState(new RemoteConnectionStateArgs(RemoteConnectionState.Stopped, connectionId, Transport.Index));
            _cachedConnectionIds.Enqueue(connectionId);
        }

        /// <summary>
        /// Allows for Outgoing queue to be iterated.
        /// </summary>
        internal void IterateOutgoing()
        {
            if (base.GetLocalConnectionState() != LocalConnectionState.Started)
                return;

            _iteratingConnections = true;
            foreach (HSteamNetConnection conn in _steamConnections.FirstTypes)
            {
#if UNITY_SERVER
                SteamGameServerNetworkingSockets.FlushMessagesOnConnection(conn);
#else
                SteamNetworkingSockets.FlushMessagesOnConnection(conn);
#endif
            }

            _iteratingConnections = false;

            ProcessPendingConnectionChanges();
        }

        /// <summary>
        /// Iterates the Incoming queue.
        /// </summary>
        /// <param name="transport"></param>
        internal void IterateIncoming()
        {
            //Stopped or trying to stop.
            if (base.GetLocalConnectionState() == LocalConnectionState.Stopped || base.GetLocalConnectionState() == LocalConnectionState.Stopping)
                return;

            _iteratingConnections = true;

            //Iterate local client packets first.
            while (_clientHostIncoming.Count > 0)
            {
                LocalPacket packet = _clientHostIncoming.Dequeue();
                ArraySegment<byte> segment = new ArraySegment<byte>(packet.Data, 0, packet.Length);
                base.Transport.HandleServerReceivedDataArgs(new ServerReceivedDataArgs(segment, (Channel)packet.Channel, FishySteamworks.CLIENT_HOST_ID, Transport.Index));
            }

            foreach (KeyValuePair<HSteamNetConnection, int> item in _steamConnections.First)
            {
                HSteamNetConnection steamNetConn = item.Key;
                int connectionId = item.Value;

                int messageCount;
#if UNITY_SERVER
                messageCount = SteamGameServerNetworkingSockets.ReceiveMessagesOnConnection(steamNetConn, base.MessagePointers, MAX_MESSAGES);
#else
                messageCount = SteamNetworkingSockets.ReceiveMessagesOnConnection(steamNetConn, base.MessagePointers, MAX_MESSAGES);
#endif
                if (messageCount > 0)
                {
                    for (int i = 0; i < messageCount; i++)
                    {
                        base.GetMessage(base.MessagePointers[i], InboundBuffer, out ArraySegment<byte> segment, out byte channel);
                        base.Transport.HandleServerReceivedDataArgs(new ServerReceivedDataArgs(segment, (Channel)channel, connectionId, Transport.Index));
                    }
                }
            }

            _iteratingConnections = false;

            ProcessPendingConnectionChanges();
        }

        /// <summary>
        /// Iterates connection changes.
        /// </summary>
        private void ProcessPendingConnectionChanges()
        {
            foreach (ConnectionChange cc in _pendingConnectionChanges)
            {
                if (cc.IsConnect)
                    AddConnection(cc.ConnectionId, cc.SteamConnection, cc.SteamId);
                else
                    RemoveConnection(cc.ConnectionId);
            }

            _pendingConnectionChanges.Clear();
        }

        /// <summary>
        /// Sends data to a client.
        /// </summary>
        internal void SendToClient(byte channelId, ArraySegment<byte> segment, int connectionId)
        {
            if (base.GetLocalConnectionState() != LocalConnectionState.Started)
                return;

            //Check if sending local client first, send and exit if so.
            if (connectionId == FishySteamworks.CLIENT_HOST_ID)
            {
                if (_clientHost != null)
                {
                    LocalPacket packet = new LocalPacket(segment, channelId);
                    _clientHost.ReceivedFromLocalServer(packet);
                }

                return;
            }

            if (_steamConnections.TryGetValue(connectionId, out HSteamNetConnection steamConn))
            {
                EResult res = base.Send(steamConn, segment, channelId);

                if (res == EResult.k_EResultNoConnection || res == EResult.k_EResultInvalidParam)
                {
                    base.Transport.NetworkManager.Log($"Connection to {connectionId} was lost.");
                    StopConnection(connectionId, steamConn);
                }
                else if (res != EResult.k_EResultOK)
                {
                    base.Transport.NetworkManager.LogError($"Could not send: {res.ToString()}");
                }
            }
            else
            {
                base.Transport.NetworkManager.LogError($"ConnectionId {connectionId} does not exist, data will not be sent.");
            }
        }

        /// <summary>
        /// Gets the address of a remote connection Id.
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns></returns>
        internal string GetConnectionAddress(int connectionId)
        {
            if (_steamIds.TryGetValue(connectionId, out CSteamID steamId))
            {
                return steamId.ToString();
            }
            else
            {
                base.Transport.NetworkManager.LogError($"ConnectionId {connectionId} is invalid; address cannot be returned.");
                return string.Empty;
            }
        }

        /// <summary>
        /// Sets maximum number of clients allowed to connect to the server. If applied at runtime and clients exceed this value existing clients will stay connected but new clients may not connect.
        /// </summary>
        internal void SetMaximumClients(int value)
        {
            _maximumClients = Math.Min(value, FishySteamworks.CLIENT_HOST_ID - 1);
        }

        /// <summary>
        /// Returns maximum number of allowed clients.
        /// </summary>
        /// <returns></returns>
        internal int GetMaximumClients()
        {
            return _maximumClients;
        }

        #region ClientHost (local client).
        /// <summary>
        /// Sets ClientHost value.
        /// </summary>
        /// <param name="socket"></param>
        internal void SetClientHostSocket(ClientHostSocket socket)
        {
            _clientHost = socket;
        }

        /// <summary>
        /// Called when the local client state changes.
        /// </summary>
        internal void OnClientHostState(bool started)
        {
            FishySteamworks fs = (FishySteamworks)base.Transport;
            CSteamID steamId = new CSteamID(fs.LocalUserSteamID);

            //If not started but was previously flush incoming from local client.
            if (!started && _clientHostStarted)
            {
                base.ClearQueue(_clientHostIncoming);
                base.Transport.HandleRemoteConnectionState(new RemoteConnectionStateArgs(RemoteConnectionState.Stopped, FishySteamworks.CLIENT_HOST_ID, Transport.Index));
                _steamIds.Remove(steamId);
            }
            //If started.
            else if (started)
            {
                _steamIds[steamId] = FishySteamworks.CLIENT_HOST_ID;
                base.Transport.HandleRemoteConnectionState(new RemoteConnectionStateArgs(RemoteConnectionState.Started, FishySteamworks.CLIENT_HOST_ID, Transport.Index));
            }

            _clientHostStarted = started;
        }

        /// <summary>
        /// Queues a received packet from the local client.
        /// </summary>
        internal void ReceivedFromClientHost(LocalPacket packet)
        {
            if (!_clientHostStarted)
                return;

            _clientHostIncoming.Enqueue(packet);
        }
        #endregion
    }
}
#endif // !DISABLESTEAMWORKS