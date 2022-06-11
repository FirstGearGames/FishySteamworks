#if !FISHYSTEAMWORKS
using FishNet.Transporting;
using FishNet.Utility.Performance;
using FishySteamworks.Server;
using System;
using System.Collections.Generic;

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
        private Queue<LocalPacket> _incoming = new Queue<LocalPacket>();
        #endregion

        /// <summary>
        /// Checks to set localCLient started.
        /// </summary>
        internal void CheckSetStarted()
        {
            //Check to set as started.
            if (_server != null && base.GetLocalConnectionState() == LocalConnectionState.Starting)
            {
                if (_server.GetLocalConnectionState() == LocalConnectionState.Started)
                    SetLocalConnectionState(LocalConnectionState.Started, false);
            }
        }


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
            if (_server.GetLocalConnectionState() != LocalConnectionState.Started)
                return false;

            SetLocalConnectionState(LocalConnectionState.Starting, false);
            return true;
        }

        /// <summary>
        /// Sets a new connection state.
        /// </summary>
        protected override void SetLocalConnectionState(LocalConnectionState connectionState, bool server)
        {
            base.SetLocalConnectionState(connectionState, server);
            if (connectionState == LocalConnectionState.Started)
                _server.OnClientHostState(true);
            else
                _server.OnClientHostState(false);
        }

        /// <summary>
        /// Stops the local socket.
        /// </summary>
        internal bool StopConnection()
        {
            if (base.GetLocalConnectionState() == LocalConnectionState.Stopped || base.GetLocalConnectionState() == LocalConnectionState.Stopping)
                return false;

            base.ClearQueue(_incoming);
            //Immediately set stopped since no real connection exists.
            SetLocalConnectionState(LocalConnectionState.Stopping, false);
            SetLocalConnectionState(LocalConnectionState.Stopped, false);
            _server.SetClientHostSocket(null);

            return true;
        }

        /// <summary>
        /// Iterations data received.
        /// </summary>
        internal void IterateIncoming()
        {
            if (base.GetLocalConnectionState() != LocalConnectionState.Started)
                return;

            while (_incoming.Count > 0)
            {
                LocalPacket packet = _incoming.Dequeue();
                ArraySegment<byte> segment = new ArraySegment<byte>(packet.Data, 0, packet.Length);
                base.Transport.HandleClientReceivedDataArgs(new ClientReceivedDataArgs(segment, (Channel)packet.Channel, Transport.Index));
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
            if (base.GetLocalConnectionState() != LocalConnectionState.Started)
                return;
            if (_server.GetLocalConnectionState() != LocalConnectionState.Started)
                return;

            LocalPacket packet = new LocalPacket(segment, channelId);
            _server.ReceivedFromClientHost(packet);
        }



    }
}
#endif // !DISABLESTEAMWORKS