using NetworkConnections;
using OSCTools;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
//using UnityEditor.VersionControl;
using UnityEngine;
//using UnityEngine.tvOS;
using System.Collections.Generic;


/// <summary>
/// The client is the class that lets game code (Controller and View classes) communicate with 
/// the server, and handles network connections.
/// </summary>
public class Client : MonoBehaviour
{
    // ----- General client things:
    public IPAddress ServerIP = IPAddress.Loopback;
    TcpNetworkConnection connection;
    OSCDispatcher dispatcher;

    

    // Views subscribe here, on any client:
    //S->C
    public event Action<string, int> MessageWelcomePlayer;
    public event Action<BoatData.Boats, int, int, bool> MessagePlacementValid;
    public event Action<string> MessageBadBoat;
    public event Action<int, int> MessageBoatRemoved;
    public event Action<string, int> MessagePlayerReady;
    public event Action<string, int> MessageAllPlayersReady;

    public event Action<int, int, int> AttackHit;
    public event Action<int, int, int> AttackMiss;
    public event Action<int, int, int, BoatData.Boats, bool> AttackFatal;
    public event Action<string> GameOver;

    public LocalModel localModel;

    

    void Start()
    {
        TcpClient client = new TcpClient();
        client.Connect(new IPEndPoint(ServerIP, 50007));
        connection = new TcpNetworkConnection(client);
        // TODO: error handling

        Debug.Log("Starting client, connecting to " + ServerIP);

        // Initialize the dispatcher and callbacks for incoming OSC messages:
        dispatcher = new OSCDispatcher();
        dispatcher.ShowIncomingMessages = true;
        Initialize();
    }

    /// <summary>
    /// Called from NetworkConnection callback (connection.Update), when a packet arrives:
    /// </summary>
    void HandlePacket(byte[] packet, IPEndPoint remote)
    {
        OSCMessageIn mess = new OSCMessageIn(packet);
        Debug.Log("Message arrives on client: " + mess);
        dispatcher.HandlePacket(packet, remote);
    }

    void Update()
    {
        // Check for incoming packets, and deal with them:
        while (connection.Available() > 0)
        {
            HandlePacket(connection.GetPacket(), connection.Remote);
        }
        // TODO: disconnect handling
    }

    void Initialize()
    {
        localModel = new LocalModel();
        
        localModel.boatData = FindFirstObjectByType<BoatData>();
        localModel.triedData = FindFirstObjectByType<TriedData>();
        List<DisplayGrid> displays = FindObjectsByType<DisplayGrid>(FindObjectsSortMode.None).ToList();

        localModel.displayBoats = displays.Find(d => d.type == "Boats");
        localModel.displayTried = displays.Find(d => d.type == "Tried");
        localModel.displayOpponentTried = displays.Find(d => d.type == "Lost");

        localModel.textDisplay = FindFirstObjectByType<DisplayText>();
        FindFirstObjectByType<SwitchOrientation>().switchOrientation += localModel.SwitchOrientation;
        localModel.Initialize(this);

        //S->C receiving from server
        dispatcher.AddListener("/MessageWelcomePlayer", MessageWelcomePlayerRpc, OSCUtil.STRING, OSCUtil.INT);
        dispatcher.AddListener("/PlayerInfo", PrivateMessageRpc, OSCUtil.INT);
        dispatcher.AddListener("/MessagePlacementValid", MessagePlacementValidRpc, OSCUtil.INT, OSCUtil.INT, OSCUtil.INT, OSCUtil.BOOL);
        dispatcher.AddListener("/MessageBadBoat", MessageBadBoatRpc, OSCUtil.STRING);
        dispatcher.AddListener("/MessageBoatRemoved", MessageBoatRemovedRpc, OSCUtil.INT, OSCUtil.INT);
        dispatcher.AddListener("/MessagePlayerReady", MessagePlayerReadyRpc, OSCUtil.STRING, OSCUtil.INT);
        dispatcher.AddListener("/MessageAllPlayersReady", MessageAllPlayersReadyRpc, OSCUtil.STRING, OSCUtil.INT);

        dispatcher.AddListener("/AttackHit", AttackHitRpc, OSCUtil.INT, OSCUtil.INT, OSCUtil.INT);
        dispatcher.AddListener("/AttackMiss", AttackMissRpc, OSCUtil.INT, OSCUtil.INT, OSCUtil.INT);
        dispatcher.AddListener("/AttackFatal", AttackFatalRpc, OSCUtil.INT, OSCUtil.INT, OSCUtil.INT, OSCUtil.INT, OSCUtil.BOOL);
        dispatcher.AddListener("/GameOver", GameOverRpc, OSCUtil.STRING);
    }

    // ----- Incoming RPCs (events are triggered, and View classes subscribe): S->C

    //state1

    public void PrivateMessageRpc(OSCMessageIn message, IPEndPoint remote)
    {
        int PlayerID = message.ReadInt();

        localModel.clientID = PlayerID;
    }

    public void MessageWelcomePlayerRpc(OSCMessageIn message, IPEndPoint remote)
    {
        string text = message.ReadString();
        int playerID = message.ReadInt();
        

        MessageWelcomePlayer?.Invoke(text, playerID);
    }

    public void MessagePlacementValidRpc(OSCMessageIn message, IPEndPoint remote)
    {
        Debug.Log("placement valid is getting called");
        BoatData.Boats boatType = (BoatData.Boats)message.ReadInt();
        int row = message.ReadInt();
        int column = message.ReadInt();
        bool horizontal = message.ReadBool();

        MessagePlacementValid?.Invoke(boatType, row, column, horizontal);
    }

    public void MessageBadBoatRpc(OSCMessageIn message, IPEndPoint remote)
    {
        string text = message.ReadString();
        MessageBadBoat?.Invoke(text);
        localModel.textDisplay.UpdateDisplay(text);//use action maybe
    }

    public void MessageBoatRemovedRpc(OSCMessageIn message, IPEndPoint remote)
    {
        int row = message.ReadInt();
        int column = message.ReadInt();
        
        MessageBoatRemoved?.Invoke(row, column);
    }

    public void MessagePlayerReadyRpc(OSCMessageIn message, IPEndPoint remote)//implement later
    {
        string text = message.ReadString();
        int numberOfPlayersReady = message.ReadInt();

        MessagePlayerReady?.Invoke(text, numberOfPlayersReady);
        localModel.textDisplay.UpdateDisplay("this many players are ready:" + numberOfPlayersReady);//use action maybe
        Debug.Log("messagePlayerReadyRpc is getting activated. now do something");
    }

    public void MessageAllPlayersReadyRpc(OSCMessageIn message, IPEndPoint remote)//implement later
    {
        string text = message.ReadString();
        int IdOfCurrentPlayer = message.ReadInt();

        MessageAllPlayersReady?.Invoke(text, IdOfCurrentPlayer);
        localModel.gamestate = 1;
        localModel.textDisplay.UpdateDisplay("all players ready");
    }

    //state2
    public void AttackHitRpc(OSCMessageIn message, IPEndPoint remote)
    {
        int row = message.ReadInt();
        int column = message.ReadInt();
        int origin = message.ReadInt();

        AttackHit?.Invoke(row, column, origin);
    }

    public void AttackMissRpc(OSCMessageIn message, IPEndPoint remote)
    {
        int row = message.ReadInt();
        int column = message.ReadInt();
        int origin = message.ReadInt();

        AttackMiss?.Invoke(row, column, origin);
    }

    public void AttackFatalRpc(OSCMessageIn message, IPEndPoint remote)
    {
        int row = message.ReadInt();
        int column = message.ReadInt();
        int origin = message.ReadInt();
        BoatData.Boats type = (BoatData.Boats)message.ReadInt();
        bool horizontal = message.ReadBool();
        AttackFatal?.Invoke(row, column, origin, type, horizontal);
    }

    public void GameOverRpc(OSCMessageIn message, IPEndPoint remote)
    {
        string text = message.ReadString();
        localModel.textDisplay.UpdateDisplay(text);
        GameOver?.Invoke(text);
    }

    // ----- Outgoing RPCs (called from Controller): C->S

    //state1

    
    //public void JoinGameRequest() //JoinGameRequest is not a real request that is sent, it already exists in start
    //{
    //    OSCMessageOut message = new OSCMessageOut("/JoinGame");
    //    connection.Send(message.GetBytes());
    //}

    public void ChooseBoatLocationRequest(BoatData.Boats boatType, int row, int column, bool horizontal)
    {
        OSCMessageOut message = new OSCMessageOut("/ChooseBoatLocation").AddInt((int)boatType).AddInt(row).AddInt(column).AddBool(horizontal);
        connection.Send(message.GetBytes());
    }

    public void RemoveBoatRequest(int row, int column)
    {
        OSCMessageOut message = new OSCMessageOut("/RemoveBoat").AddInt(row).AddInt(column);
        connection.Send(message.GetBytes());
    }

    public void ReadyToMoveOnRequest()
    {
        OSCMessageOut message = new OSCMessageOut("/ReadyToMoveOn");
        connection.Send(message.GetBytes());
    }



    //state2
    public void AttackLocationRequest(int row, int column)
    {
        OSCMessageOut message = new OSCMessageOut("/AttackLocation").AddInt(row).AddInt(column);
        connection.Send(message.GetBytes());
    }
}
