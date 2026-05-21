using NetworkConnections;
using OSCTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

//using UnityEditor.VersionControl;
using UnityEngine;


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

    //runtime variables


    // Views subscribe here, on any client:
    //S->C
    
    public event Action<BoatData.Boats, int, int, bool> MessagePlacementValid;
    public event Action<string> MessageBadBoat;
    public event Action<int, int> MessageBoatRemoved;
    public event Action<string, int> MessagePlayerReady;
    public event Action<string, int> MessageAllPlayersReady;

    public event Action<int, int, int> AttackHit;
    public event Action<int, int, int> AttackMiss;
    public event Action<int, int, int, BoatData.Boats, bool> AttackFatal;
    public event Action<string, int> GameOver;

    [HideInInspector] public LocalModel localModel;
    [HideInInspector] public DisplayText textDisplay;

    [HideInInspector] public bool isConnected = false;
    [HideInInspector] public bool GameNotRunning = true;

    [HideInInspector] public SwitchButton orientation;
    [HideInInspector] public SwitchButton connectionStatus;
    [HideInInspector] public SwitchButton gameActive;

    void Start()
    {
        List<SwitchButton> buttons = FindObjectsByType<SwitchButton>(FindObjectsSortMode.None).ToList();

        orientation = buttons.Find(b => b.type == "Orientation");
        connectionStatus = buttons.Find(b => b.type == "Connection");
        gameActive = buttons.Find(b => b.type == "GameRunning");

        orientation.switchButton += UpdateOrientation;
        connectionStatus.switchButton += UpdateConnection;
        gameActive.switchButton += UpdateGameStatus;

        textDisplay = FindFirstObjectByType<DisplayText>();
    }

    /// <summary>
    /// Called from NetworkConnection callback (connection.Update), when a packet arrives:
    /// </summary>
    void HandlePacket(byte[] packet, IPEndPoint remote)
    {
        // Ignore heartbeat packets (1 byte = 0)
        if (packet.Length == 1 && packet[0] == 0)
            return;

        OSCMessageIn mess = new OSCMessageIn(packet);
        Debug.Log("Message arrives on client: " + mess);
        dispatcher.HandlePacket(packet, remote);
    }


    void Update()
    {
        if (!isConnected) return;
        
        // Check for incoming packets, and deal with them:
        while (connection.Available() > 0)
        {
            HandlePacket(connection.GetPacket(), connection.Remote);
        }
        
        ConnectionStatus status = connection.Status;
        if (status == ConnectionStatus.Disconnected)
        {
            textDisplay.UpdateDisplay("You disconnected from the server");
            GameNotRunning = true;
            gameActive.UpdateText(GameNotRunning);
            isConnected = false;
            connectionStatus.UpdateText(isConnected);
        }
    }

    public void UpdateOrientation()
    {
        if (localModel == null) return;
        if (!isConnected) return;
        if (GameNotRunning) return;
        localModel.horizontal = !localModel.horizontal;
        orientation.UpdateText(localModel.horizontal);
    }

    public void UpdateConnection()
    {
        if (!isConnected)
        {
            DisposeModel();//close any game that might still be running localy
            Connect();
        }
        else
        {
            DisConnect();
            GameNotRunning = true;
            gameActive.UpdateText(GameNotRunning);
        }
    }

    public void UpdateGameStatus()
    {
        if (GameNotRunning)//new game
        {
            DisposeModel();
            if (isConnected)
            {
                NewGameRequest();
            }
            else
            {
                Connect();
            }
        }
        else//resign
        {
            if (!isConnected) return;
            
            ResignRequest();
        }
    }

    private void Connect()
    {
        TcpClient client = new TcpClient();
        client.Connect(new IPEndPoint(ServerIP, 50007));
        connection = new TcpNetworkConnection(client);
        // TODO: error handling

        Debug.Log("Starting client, connecting to " + ServerIP);

        // Initialize the dispatcher and callbacks for incoming OSC messages:
        dispatcher = new OSCDispatcher();
        dispatcher.ShowIncomingMessages = true;
        AddListeners();
        isConnected = true;
        connectionStatus.UpdateText(isConnected);
    }

    public void DisConnect()
    {
        connection?.Close();
        connection = null;
        isConnected = false;
        connectionStatus.UpdateText(isConnected);

    }

    private void InitializeModel()
    {
        Debug.Log("initializing model");
        localModel = new LocalModel();
        localModel.client = this;

        localModel.boatData = FindFirstObjectByType<BoatData>();
        localModel.triedData = FindFirstObjectByType<TriedData>();

        List<DisplayGrid> displays = FindObjectsByType<DisplayGrid>(FindObjectsSortMode.None).ToList();
        localModel.displayBoats = displays.Find(b => b.type == "Boats");
        localModel.displayTried = displays.Find(b => b.type == "Tried");
        localModel.displayOpponentTried = displays.Find(b => b.type == "Lost");

        localModel.textDisplay = textDisplay;

        List<SwitchButton> buttons = FindObjectsByType<SwitchButton>(FindObjectsSortMode.None).ToList();

        localModel.Initialize();

        GameNotRunning = false;
        gameActive.UpdateText(GameNotRunning);
    }

    public void DisposeModel()
    {
        if (localModel == null) return;
        localModel.Dispose();
        localModel = null;
        GameNotRunning = true;
        gameActive.UpdateText(GameNotRunning);
    }

    void AddListeners()
    {
        //S->C receiving from server
        dispatcher.AddListener("/PlayerInfo", PrivateMessageRpc, OSCUtil.INT);
        dispatcher.AddListener("/MessagePlacementValid", MessagePlacementValidRpc, OSCUtil.INT, OSCUtil.INT, OSCUtil.INT, OSCUtil.BOOL);
        dispatcher.AddListener("/MessageBadBoat", MessageBadBoatRpc, OSCUtil.STRING);
        dispatcher.AddListener("/MessageBoatRemoved", MessageBoatRemovedRpc, OSCUtil.INT, OSCUtil.INT);
        dispatcher.AddListener("/MessagePlayerReady", MessagePlayerReadyRpc, OSCUtil.STRING, OSCUtil.INT);
        dispatcher.AddListener("/MessageAllPlayersReady", MessageAllPlayersReadyRpc, OSCUtil.STRING, OSCUtil.INT);

        dispatcher.AddListener("/AttackHit", AttackHitRpc, OSCUtil.INT, OSCUtil.INT, OSCUtil.INT);
        dispatcher.AddListener("/AttackMiss", AttackMissRpc, OSCUtil.INT, OSCUtil.INT, OSCUtil.INT);
        dispatcher.AddListener("/AttackFatal", AttackFatalRpc, OSCUtil.INT, OSCUtil.INT, OSCUtil.INT, OSCUtil.INT, OSCUtil.BOOL);
        dispatcher.AddListener("/GameOver", GameOverRpc, OSCUtil.STRING, OSCUtil.INT);
        dispatcher.AddListener("/ServerFull", ServerFullRpc);
        dispatcher.AddListener("/PressedStartGame", PressedStartGameRpc);
    }

    // ----- Incoming RPCs (events are triggered, and View classes subscribe): S->C

    //state1

    

    public void PrivateMessageRpc(OSCMessageIn message, IPEndPoint remote)
    {
        int PlayerID = message.ReadInt();
        if (localModel != null) return;
        
        InitializeModel();
        localModel.clientID = PlayerID;
        
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
        localModel.textDisplay.UpdateDisplay(text + numberOfPlayersReady);//use action maybe
        
    }

    public void MessageAllPlayersReadyRpc(OSCMessageIn message, IPEndPoint remote)//implement later
    {
        string text = message.ReadString();
        int IdOfCurrentPlayer = message.ReadInt();

        MessageAllPlayersReady?.Invoke(text, IdOfCurrentPlayer);
        
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

    //any state
    public void GameOverRpc(OSCMessageIn message, IPEndPoint remote)
    {
        string text = message.ReadString();
        int playerID = message.ReadInt();
        
        
        GameOver?.Invoke(text, playerID);
        
    }

    public void ServerFullRpc(OSCMessageIn message, IPEndPoint remote)
    {
        Debug.Log("Server is full!");
        textDisplay.UpdateDisplay("Server is full!");
        connection?.Close();
        GameNotRunning = true;
        gameActive.UpdateText(GameNotRunning);
        isConnected = false;
        connectionStatus.UpdateText(isConnected);

    }

    public void PressedStartGameRpc(OSCMessageIn message, IPEndPoint remote)
    {
        Debug.Log("Waiting for both players to start the game");
        textDisplay.UpdateDisplay("Waiting for both players to start the game");
        

    }

    // ----- Outgoing RPCs (called from Controller): C->S

    //state1
    public void NewGameRequest()//after a game has ended and a player wants to play again
    {
        if (!GameNotRunning) return;
        OSCMessageOut message = new OSCMessageOut("/NewGame");
        connection.Send(message.GetBytes());
    }


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
        if (!isConnected) return;
        if (GameNotRunning) return;
        OSCMessageOut message = new OSCMessageOut("/ReadyToMoveOn");
        connection.Send(message.GetBytes());
    }



    //state2
    public void AttackLocationRequest(int row, int column)
    {
        OSCMessageOut message = new OSCMessageOut("/AttackLocation").AddInt(row).AddInt(column);
        connection.Send(message.GetBytes());
    }


    //any state
    public void ResignRequest()
    {
        if (GameNotRunning) return;
        OSCMessageOut message = new OSCMessageOut("/Resign");
        connection.Send(message.GetBytes());
    }

    

}
