using System.Net;			// IPEndPoint
using System.Net.Sockets;	// TcpClient
using System;               // BitConverter
using System.Collections.Generic; // Queue

namespace NetworkConnections {

	public enum ConnectionStatus { Connecting, Connected, Disconnecting, Disconnected }

	/// <summary>
	/// A user friendly wrapper around a TCP client, that handles message boundaries, doesn't block, and
	/// catches most exceptions. 
	/// </summary>
	public class TcpNetworkConnection {
		public int LocalPort {
			get {
				if (localPort < 0 && socket.Client.LocalEndPoint != null) {
					localPort = ((IPEndPoint)(socket.Client.LocalEndPoint)).Port;
				}
				return localPort;
			}
		}
		public IPEndPoint Remote {
			get {
				if (remote==null && socket.Connected && socket.Client.RemoteEndPoint!=null) {
					remote = (IPEndPoint)socket.Client.RemoteEndPoint;
				}
				return remote;
			}
		}

		public ConnectionStatus Status { get; private set; } = ConnectionStatus.Connecting;

		readonly TcpClient socket;

		// Internal packet reading state:
		Queue<byte[]> incoming = new Queue<byte[]>();
		bool _isReadingPacket = false;
		int _nextPacketLength;

		//heartbeat
        DateTime lastHeardFrom = DateTime.UtcNow;
        DateTime lastHeartbeatSent = DateTime.UtcNow;

        // Cached + lazily initialized socket properties (to prevent exceptions):
        int localPort = -1;
		IPEndPoint remote=null;

		 
		/// <summary>
		/// Use this constructor to open a connection to a remote listener (=server). 
		/// </summary>
		/// <param name="remoteIPstring">Remote (server) IP address</param>
		/// <param name="remotePort">Remote (server) port</param>
		/// <param name="asynchronous">If true, will be initialized asynchronous (non-blocking), and start in Connecting state</param>
		/// <param name="fast">If true, disables Nagle's algorithm (=more responsive, but more small packets)</param>
		public TcpNetworkConnection(string remoteIPstring, int remotePort, bool asynchronous = false, bool fast = true) {
			Status = ConnectionStatus.Connecting;

			socket = new TcpClient();
			socket.NoDelay = fast;

            // Enable TCP KeepAlive
            socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);


            if (asynchronous) {
				socket.BeginConnect(remoteIPstring, remotePort, new AsyncCallback(ConnectionCallback), this);
			} else {
				try {
					socket.Connect(remoteIPstring, remotePort);
					ProcessConnectionResult();
				} catch (Exception error) {
					ConnectionLog.WriteLine("Exception during connection attempt: " + error.Message);
					Status = ConnectionStatus.Disconnected;
				}
			}
		}

		/// <summary>
		/// Use this constructor when accepting a TcpClient from a listener.
		/// </summary>
		/// <param name="client">TcpClient accepted from listener</param>
		public TcpNetworkConnection(TcpClient client) {
			socket = client;

            // Enable TCP KeepAlive
            socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            if (client.Connected) {
				Status = ConnectionStatus.Connected;
				if (client.Client.LocalEndPoint != null) {
					localPort = ((IPEndPoint)socket.Client.LocalEndPoint).Port;
					remote = (IPEndPoint)socket.Client.RemoteEndPoint;
				}
			} else {
				Status = ConnectionStatus.Disconnected;
			}
		}

		void ConnectionCallback(IAsyncResult result) {
			ConnectionLog.WriteLine(2, "Connection callback. Completed: " + result.IsCompleted);
			try {
				socket.EndConnect(result); // according to the documentation of BeginConnect, this is necessary somehow
				ProcessConnectionResult();
			} catch (Exception error) {
				ConnectionLog.WriteLine("Exception during connection callback: " + error.Message);
				Status = ConnectionStatus.Disconnected;
			}
		}

		void ProcessConnectionResult() {
			if (socket.Connected) {
				ConnectionLog.WriteLine("Connection successful");
				Status = ConnectionStatus.Connected;
				if (socket.Client.LocalEndPoint != null) {
					localPort = ((IPEndPoint)socket.Client.LocalEndPoint).Port;
					remote = (IPEndPoint)socket.Client.RemoteEndPoint;
				}
			} else {
				ConnectionLog.WriteLine("Failed to connect");
				Status = ConnectionStatus.Disconnected;
			}
		}

		void Update() {
			if (!socket.Connected) {
				Status = ConnectionStatus.Disconnected;
				ConnectionLog.WriteLine("NetworkConnection.Update: socket closed by remote");
				return;
			}
            // Heartbeat timeout
            if ((DateTime.UtcNow - lastHeardFrom).TotalSeconds > 15)
            {
                Status = ConnectionStatus.Disconnected;
                ConnectionLog.WriteLine("Heartbeat timeout: remote not responding");
                return;
            }
            // HEARTBEAT SENDER
            if ((DateTime.UtcNow - lastHeartbeatSent).TotalSeconds > 5)
            {
                Send(new byte[] { 0 }); // heartbeat
                lastHeartbeatSent = DateTime.UtcNow;
            }
            try {
				while (Status == ConnectionStatus.Connected && socket.Available > 0) {
					NetworkStream stream = socket.GetStream();

					if (_isReadingPacket) { // we have read the header of a packet, and are currently waiting for the full body to arrive
						if (socket.Available >= _nextPacketLength) {
							byte[] data = new byte[_nextPacketLength];

                            int bytesRead = stream.Read(data, 0, _nextPacketLength);
                            if (bytesRead == 0)//test when connection ends
                            {
                                Status = ConnectionStatus.Disconnected;
                                return;
                            }
                            lastHeardFrom = DateTime.UtcNow;//update time since update

                            _isReadingPacket = false;
							incoming.Enqueue(data);
						} else {
							return; // wait for the rest of the packet to arrive
						}
					} else {
						if (socket.Available >= 4) { // read the header
							byte[] data = new byte[4];

                            int bytesRead = stream.Read(data, 0, 4);
                            if (bytesRead == 0)
                            {
                                Status = ConnectionStatus.Disconnected;
                                return;
                            }
                            lastHeardFrom = DateTime.UtcNow;

                            _nextPacketLength = BitConverter.ToInt32(data, 0);
							_isReadingPacket = true;
							ConnectionLog.WriteLine(2, "Incoming packet of length {0}", _nextPacketLength);
						} else {
							return; // wait for the rest of the header to arrive
						}
					}
				}
			} catch (Exception error) { // An exception here is very unlikely, but just to be sure
				Status = ConnectionStatus.Disconnected;
				ConnectionLog.WriteLine("NetworkConnection.Update: Exception: " + error.Message);
			}
		}

		/// <summary>
		/// Send a packet to the remote end point. 
		/// Only works when the status is Connected.
		/// </summary>
		public void Send(byte[] packet) {
			if (Status != ConnectionStatus.Connected) {
				ConnectionLog.WriteLine("NetworkConnection.Send: skip, since status = " + Status);
				return;
			}
			try {
				NetworkStream stream = socket.GetStream();
				stream.WriteTimeout = 1;
				if (stream.CanWrite) {
					stream.Write(BitConverter.GetBytes(packet.Length), 0, 4);
					stream.Write(packet, 0, packet.Length);
				} else {
					ConnectionLog.WriteLine("Error: cannot send, because cannot write to network stream");
				}
			} catch (Exception error) {
				ConnectionLog.WriteLine("NetworkConnection.Send: " + error.Message);
				Close();
			}
		}

		/// <summary>
		/// Returns the number of available packets. 
		/// If non-zero, call GetPacket to retrieve the next incoming packet.
		/// </summary>
		public int Available() {
			if (Status != ConnectionStatus.Connected) return 0;
			Update();
			return incoming.Count;
		}

		/// <summary>
		/// If a packet is available, this returns the first available packet.
		/// Otherwise, returns null.
		/// Use Available first to check whether a packet is available.
		/// </summary>
		public byte[] GetPacket() {
			if (Status != ConnectionStatus.Connected) return null;
			if (Available()>0) {
				byte[] first = incoming.Dequeue();
				return first;
			}
			return null;
		}

		/// <summary>
		/// Call this when done, to clean up resources.
		/// </summary>
		public void Close() {
			Status = ConnectionStatus.Disconnected;
			socket.Close();
		}
	}
}
