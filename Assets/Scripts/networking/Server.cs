using NetworkConnections;
using OSCTools;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

//using UnityEditor.MemoryProfiler;
//using UnityEditor.VersionControl;
using UnityEngine;

/// <summary>
/// The Server is the class that manages network connections with all clients, and 
/// communicates with the game code (Model classes).
/// </summary>
public class Server : MonoBehaviour
{
    // ----- General server code:
    TcpListener listener;
    List<TcpNetworkConnection> connections;
    OSCDispatcher dispatcher;
    TcpNetworkConnection CurrentPacketSource;
    /// ------ TicTacToe Server code:
    /// 
    ServerModel model;
    
    public Dictionary<TcpNetworkConnection, int> playerIDs = new Dictionary<TcpNetworkConnection, int>();
    public List<int> playerIDsReady = new();

    void Start()
    {
        // This server starts with a listener:
        int port = 50007;
        Debug.Log("Starting server at " + port);
        listener = new TcpListener(IPAddress.Any, port);
        listener.Start();

        connections = new List<TcpNetworkConnection>();

        // Initialize the dispatcher and callbacks for incoming OSC messages:
        dispatcher = new OSCDispatcher();
        dispatcher.ShowIncomingMessages = true;
        OSCLog.logging = true;
        
        listeners();
    }

    void Update()
    {
        AcceptNewConnections();
        UpdateConnections();
        CleanupConnections();
    }

    void AcceptNewConnections()
    {
        if (listener.Pending())
        {
            TcpClient client = listener.AcceptTcpClient();
            TcpNetworkConnection connection = new TcpNetworkConnection(client);
            connections.Add(connection);
            Debug.Log("Server: Adding new connection from " + connection.Remote);
            ClientJoined(connection);
        }
    }
    void ClientJoined(TcpNetworkConnection newClient)
    {
        if (playerIDs.Count < 2)
        {
            // We had fewer than 2 players, so this new client will be a player.
            int newID = 0;
            if (playerIDs.Count == 0)
            {
                newID = playerIDs.Count;
            }
            else if (playerIDs.Count == 1)
            {
                newID = 1 - playerIDs.First().Value;
            }
            playerIDs[newClient] = newID;
            


            Debug.Log($"Registering new player: {newClient.Remote} = player {playerIDs[newClient]}");

            AddPlayerReady(newClient.Remote);

        }
        else
        {
            newClient.Send(new OSCMessageOut("/ServerFull").GetBytes());
            newClient.Close();
            Debug.Log("Sorry - already have two players");
            
        }
    }

    void AddPlayerReady(IPEndPoint remote)
    {
        Debug.Log("a player is ready");
        
        if (model != null) return;
        int id = GetPlayerIDFromEP(remote);
        if (playerIDsReady.Contains(id)) return;

        playerIDsReady.Add(id);
        TestEnoughPlayersReady();
    }

    void TestEnoughPlayersReady()
    {
        Debug.Log("testing player count");
        if (playerIDsReady.Count == 2)
        {
            Debug.Log(playerIDsReady.Count);
            Debug.Log(playerIDsReady[0]);
            Debug.Log(playerIDsReady[1]);
            playerIDsReady.Clear();
            StartGame();
        }
        else
        {
            Debug.Log(playerIDsReady.Count);
            Debug.Log(playerIDsReady[0]);
        }
    }

    void StartGame()
    {
        Debug.Log("Server: starting game");
        foreach (var pid in playerIDs.Keys)
        {
            SendPrivateInformationCommand(playerIDs[pid], pid);
        }
        InitializeModel();
    }


    void UpdateConnections()
    {
        foreach (TcpNetworkConnection conn in connections)
        {
            // The connection will call HandlePacket when a packet is available:
            while (conn.Available() > 0)
            {
                CurrentPacketSource = conn;
                HandlePacket(conn.GetPacket(), conn.Remote);
            }
        }
    }

    public TcpNetworkConnection getFromEndPoint(IPEndPoint destination)//helper function
    {
        return connections.Find(a => a.Remote == destination);
    }

    public int GetPlayerIDFromEP(IPEndPoint destination)//helper function
    {
        return playerIDs[getFromEndPoint(destination)];
    }

    void HandlePacket(byte[] packet, IPEndPoint remote)
    {
        if (packet.Length == 1 && packet[0] == 0) return;// ignore heartbeat
        
        OSCMessageIn mess = new OSCMessageIn(packet);
        Debug.Log("Message arrives on server: " + mess);

        dispatcher.HandlePacket(packet, remote);
    }

    void CleanupConnections()
    {
        bool anyDisconnected = false;

        for (int i = connections.Count - 1; i >= 0; i--)
        {
            var conn = connections[i];
            if (conn.Status == ConnectionStatus.Disconnected)
            {
                Debug.Log("Removing disconnected client: " + conn.Remote);

                playerIDs.Remove(conn);

                connections.RemoveAt(i);
                anyDisconnected = true;
            }
        }

        if (!anyDisconnected) return;

        // Remaining player wins
        foreach (TcpNetworkConnection conn in connections)
        {
            GameOver("opponent disconnected", GetPlayerIDFromEP(conn.Remote));
        }

        EndGame();
    }

    public void EndGame()
    {
        if (model == null) return;
        
        model.DestroyModel();
        model = null;
    }


    void InitializeModel()
    {
        model = new ServerModel();
        model.server = this;
        model.boatData = FindFirstObjectByType<BoatData>();

        
        model.MessagePlayerReady += MessagePlayerReady;
        model.MessageAllPlayersReady += MessageAllPlayersReady;

        model.AttackHit += AttackHit;
        model.AttackMiss += AttackMiss;
        model.AttackFatal += AttackFatal;
        model.GameOver += GameOver;
    }

    void listeners()
    {


        // Subscribe listeners for incoming messages:
        // The (optional) list of parameter types (OSCUtil.INT) lets the dispatcher filter
        //  messages that do not satisfy the expected signature (=parameter list):
        dispatcher.AddListener("/NewGame", NewGameRpc);
        dispatcher.AddListener("/Resign", ResignRpc);

        dispatcher.AddListener("/ChooseBoatLocation", ChooseBoatLocationRpc);
        dispatcher.AddListener("/RemoveBoat", RemoveBoatRpc);
        dispatcher.AddListener("/ReadyToMoveOn", ReadyToMoveOnRpc);

        dispatcher.AddListener("/AttackLocation", AttackLocationRpc);
    }

    // ----- Handle incoming RPCs (called by dispatcher): C->S


    //any state

    public void NewGameRpc(OSCMessageIn message, IPEndPoint remote)
    {
        
        if (model != null) return;
        
        AddPlayerReady(remote);
    }

    public void ResignRpc(OSCMessageIn message, IPEndPoint remote)
    {
        int id = GetPlayerIDFromEP(remote);
        int otherid = 1 - id;
        GameOver("player " + id + " resigned", otherid);
        EndGame();
    }

    //state 1
    public void ChooseBoatLocationRpc(OSCMessageIn message, IPEndPoint remote)
    {
        BoatData.Boats boatType = (BoatData.Boats)message.ReadInt();
        int row = message.ReadInt();
        int column = message.ReadInt();
        bool horizontal = message.ReadBool();
        
        model?.PlaceBoat(column, row, remote, boatType, horizontal);
    }

    public void RemoveBoatRpc(OSCMessageIn message, IPEndPoint remote)
    {
        int row = message.ReadInt();
        int column = message.ReadInt();
        
        model?.RemoveBoat(column, row, remote);
    }

    public void ReadyToMoveOnRpc(OSCMessageIn message, IPEndPoint remote)
    {

        model?.ReadyToMoveOn(remote);
    }

    //state 2

    public void AttackLocationRpc(OSCMessageIn message, IPEndPoint remote)
    {
        int row = message.ReadInt();
        int column = message.ReadInt();

        model?.ShootMissile(column, row, remote);
    }

    

    // ----- Outgoing RPCs: S->C
    // This RPC is called when a client joins who is a player:
    void SendPrivateInformationCommand(int playerID, TcpNetworkConnection connection)
    {
        OSCMessageOut message = new OSCMessageOut("/PlayerInfo").AddInt(playerID);
        connection.Send(message.GetBytes()); // private message
    }
    
    
    void Broadcast(byte[] packet)
    {
        foreach (var conn in connections)
        {
            conn.Send(packet);
        }
    }
    //S->C state 1

    public void MessagePlacementValid(BoatData.Boats boatType, int row, int column, IPEndPoint destination, bool horizontal)
    {
        OSCMessageOut message = new OSCMessageOut("/MessagePlacementValid").AddInt((int)boatType).AddInt(row).AddInt(column).AddBool(horizontal);
        
        getFromEndPoint(destination).Send(message.GetBytes());

    }
    public void MessageBadBoat(string text, IPEndPoint destination)
    {
        OSCMessageOut message = new OSCMessageOut("/MessageBadBoat").AddString(text);

        getFromEndPoint(destination).Send(message.GetBytes());

    }
    public void MessageBoatRemoved(int row, int column, IPEndPoint destination)
    {
        OSCMessageOut message = new OSCMessageOut("/MessageBoatRemoved").AddInt(row).AddInt(column);
        
        getFromEndPoint(destination).Send(message.GetBytes());
    }

    public void MessagePlayerReady(string text, int numberReady)
    {
        OSCMessageOut message = new OSCMessageOut("/MessagePlayerReady").AddString(text).AddInt(numberReady);
        Broadcast(message.GetBytes());
    }

    public void MessageAllPlayersReady(string text, int playerID)
    {
        OSCMessageOut message = new OSCMessageOut("/MessageAllPlayersReady").AddString(text).AddInt(playerID);
        Broadcast(message.GetBytes());
    }

    //state 2

    public void AttackHit(int row, int column, int origin)
    {

        OSCMessageOut message = new OSCMessageOut("/AttackHit").AddInt(row).AddInt(column).AddInt(origin);
        Broadcast(message.GetBytes());
    }

    public void AttackMiss(int row, int column, int origin)
    {
        OSCMessageOut message = new OSCMessageOut("/AttackMiss").AddInt(row).AddInt(column).AddInt(origin);
        Broadcast(message.GetBytes());
    }
    public void AttackFatal(int row, int column, int origin, BoatData.Boats type, bool horizontal)
    {
        OSCMessageOut message = new OSCMessageOut("/AttackFatal").AddInt(row).AddInt(column).AddInt(origin).AddInt((int)type).AddBool(horizontal);
        Broadcast(message.GetBytes());
    }
    public void GameOver(string text, int winner)
    {
        OSCMessageOut message = new OSCMessageOut("/GameOver").AddString(text).AddInt(winner);
        Broadcast(message.GetBytes());
    }
}
