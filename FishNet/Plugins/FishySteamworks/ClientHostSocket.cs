#if !FISHYSTEAMWORKS
using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Transporting;
using FishNet.Utility.Performance;
using FishySteamworks.Server;
using Steamworks;
using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace FishySteamworks.Client
{
    /// <summary>
    /// Creates a fake client connection to interact with the ServerSocket when acting as host.
    /// </summary>
    public class ClientHostSocket : CommonSocket
    {
        #region Private.
        /// <summary>
        /// Socket for the server.
        /// </summary>
        private ServerSocket _server;
        /// <summary>
        /// Incomimg data.
        /// </summary>
        private ConcurrentQueue<LocalPacket> _incoming = new ConcurrentQueue<LocalPacket>();
        #endregion

        /// <summary>
        /// Starts the client connection.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="channelsCount"></param>
        /// <param name="pollTime"></param>
        internal bool StartConnection(ServerSocket serverSocket)
        {
            _server = serverSocket;
            _server.SetClientHostSocket(this);
            if (_server.GetLocalConnectionState() != LocalConnectionStates.Started)
                return false;

            //Immediately set as connected since no real connection is made.
            SetLocalConnectionState(LocalConnectionStates.Starting, false);
            SetLocalConnectionState(LocalConnectionStates.Started, false);

            return true;
        }

        /// <summary>
        /// Sets a new connection state.
        /// </summary>
        protected override void SetLocalConnectionState(LocalConnectionStates connectionState, bool server)
        {
            base.SetLocalConnectionState(connectionState, server);
            if (connectionState == LocalConnectionStates.Started)
                _server.OnLocalClientState(true);
            else
                _server.OnLocalClientState(false);
        }

        /// <summary>
        /// Stops the local socket.
        /// </summary>
        internal bool StopConnection()
        {
            if (base.GetLocalConnectionState() == LocalConnectionStates.Stopped || base.GetLocalConnectionState() == LocalConnectionStates.Stopping)
                return false;

            //Immediately set stopped since no real connection exists.
            SetLocalConnectionState(LocalConnectionStates.Stopping, false);
            SetLocalConnectionState(LocalConnectionStates.Stopped, false);
            _server.SetClientHostSocket(null);

            return true;
        }

        /// <summary>
        /// Iterations data received.
        /// </summary>
        internal void IterateIncoming()
        {
            if (base.GetLocalConnectionState() != LocalConnectionStates.Started)
                return;

            while (_incoming.TryDequeue(out LocalPacket packet))
            {
                ArraySegment<byte> segment = new ArraySegment<byte>(packet.Data, 0, packet.Length);
                base.Transport.HandleClientReceivedDataArgs(new ClientReceivedDataArgs(segment, (Channel)packet.Channel));
                ByteArrayPool.Store(packet.Data);
            }
        }

        /// <summary>
        /// Called when the server sends the local client data.
        /// </summary>
        internal void ReceivedFromLocalServer(LocalPacket packet)
        {
            _incoming.Enqueue(packet);
        }

        /// <summary>
        /// Queues data to be sent to server.
        /// </summary>
        internal void SendToServer(byte channelId, ArraySegment<byte> segment)
        {
            if (base.GetLocalConnectionState() != LocalConnectionStates.Started)
                return;
            if (_server.GetLocalConnectionState() != LocalConnectionStates.Started)
                return;

            LocalPacket packet = new LocalPacket(segment, channelId);
            _server.ReceivedFromLocalClient(packet);
        }



    }
}
#endif // !DISABLESTEAMWORKS