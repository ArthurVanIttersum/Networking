using NetworkConnections;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.UI;

public class ServerModel
{
    //data structures
    List<Board<BoatData.Boats>> BoatBoards = new() { new(), new() };
    List<Board<TriedData.Tried>> TriedBoards = new() { new(), new() };//usefull for fatal detection and game over detection
    List<BoatList> boatLists = new() { new(), new() };//caching boat locations in a more structured format than the board

    //data sources
    public Server server;
    public BoatData boatData;


    //local values
    
    public int currentPlayer = 0;
    public int otherPlayer = 1;
    public List<IPEndPoint> readyPlayers = new();
    public int gamestate = 0;




    //S->C events
    
    public event Action<string> MessagePlacementValid;
    public event Action<string> MessageBoatRemoved;
    public event Action<string, int> MessagePlayerReady;
    public event Action<string, int> MessageAllPlayersReady;

    public event Action<int, int, int> AttackHit;
    public event Action<int, int, int> AttackMiss;
    public event Action<int, int, int, BoatData.Boats, bool> AttackFatal;
    public event Action<string, int> GameOver;



    //state0

    public void PlaceBoat(int column, int row, IPEndPoint origin, BoatData.Boats type, bool horizontal)//place whole boat
    {
        if (!IsStateRight(0)) return;

        int id = server.GetPlayerIDFromEP(origin);

        List<Coordinate> coordinates = boatLists[0].GenerateCoordinateList(column, row, boatData.boatSizes[(int)type], horizontal);
        

        bool isValid = true;
        foreach (Coordinate coord in coordinates)
        {
            if (!BoatBoards[id].IsInBounds(coord.row, coord.column))
            {
                server.MessageBadBoat("The boat would end up outside the grid", origin);
                return;
            }
            if (BoatBoards[id].SampleGrid(coord.row, coord.column) != BoatData.Boats.Empty) isValid = false;
        }
        if (!isValid)
        {
            Debug.Log("Client tries to place bad boat");
            server.MessageBadBoat("The boat location is blocked by another boat", origin);
            return; // no message?
        }
        boatLists[id].boats.Add(new Boat(type, row, column, coordinates, horizontal));
        foreach (Coordinate coord in coordinates)//execution
        {
            BoatBoards[id].WriteToGrid(coord.row, coord.column, type);
        }

        server.MessagePlacementValid(type, row, column, origin, horizontal);
    }

    public void RemoveBoat(int column, int row, IPEndPoint origin)//remove whole boat
    {
        if (!IsStateRight(0)) return;

        int id = server.GetPlayerIDFromEP(origin);

        Boat boat = boatLists[id].FindBoat(row, column);
        //todo: add test
        foreach (var coord in boat.coordinates)
        {
            BoatBoards[id].WriteToGrid(coord.row, coord.column, BoatData.Boats.Empty);
        }
        boatLists[id].boats.Remove(boat);

        server.MessageBoatRemoved(row, column, origin);
    }

    public void ReadyToMoveOn(IPEndPoint origin)
    {
        if (!IsStateRight(0)) return;

        if (readyPlayers.Count == 2) return;//quick sanity check

        int id = server.GetPlayerIDFromEP(origin);
        if (!boatLists[id].IsValidSet()) return;//player does not have a valid set of boats
        
        readyPlayers.Add(origin);

        if (readyPlayers.Count == 1)
        {
            server.MessagePlayerReady("playersReady:", readyPlayers.Count);
        }

        if (readyPlayers.Count == 2)
        {
            server.MessageAllPlayersReady("All players are ready", 0);
            gamestate = 1;
        }

    }

    //state1
    public void ShootMissile(int column, int row, IPEndPoint origin)
    {
        // TODO: What happens if a client sends this message during the wrong phase?
        if (!IsStateRight(1)) return;

        int id = server.GetPlayerIDFromEP(origin);
        int otherid = 1 - id;

        if (id != currentPlayer) return;//ignore messages from the wrong player


        //check board data
        if (!BoatBoards[otherid].IsInBounds(row, column)) return;
        BoatData.Boats boatData = BoatBoards[otherid].SampleGrid(row, column);

        //send message back
        if (boatData == BoatData.Boats.Empty)
        {
            server.AttackMiss(row, column, currentPlayer);
        }
        else//not a miss
        {
            TriedBoards[1 - id].WriteToGrid(row, column, TriedData.Tried.Hit);
            Boat boat = boatLists[otherid].FindBoat(row, column);
            bool isFatal = true;
            foreach (Coordinate coord in boat.coordinates)
            {
                if (!TriedBoards[otherid].IsInBounds(coord.row, coord.column)) return;
                if (TriedBoards[otherid].SampleGrid(coord.row, coord.column) == TriedData.Tried.Empty)
                { isFatal = false; break; }
            }
            
            if (isFatal)
            {
                foreach (Coordinate coord in boat.coordinates)
                {
                    TriedBoards[otherid].WriteToGrid(coord.row, coord.column, TriedData.Tried.SunkenBoat);
                }
                boat.sunk = true;
                server.AttackFatal(boat.row, boat.column, currentPlayer, boat.boatType, boat.horizontal);
                if (boatLists[otherid].IsAllSunk())
                {
                    server.GameOver("game over", id);
                    server.EndGame();
                }
            }
            else
            {
                server.AttackHit(row, column, currentPlayer);
            }
            
        }

        //switch current player
        currentPlayer = 1 - currentPlayer;//alternate between players
        otherPlayer = 1 - currentPlayer;//the opposite
    }

    public bool IsStateRight(int gameState)
    {
        return (gameState == gamestate);
    }

    public void DestroyModel()
    {
        //remove any remaining events

        
        MessagePlacementValid = null;
        MessageBoatRemoved = null;
        MessagePlayerReady = null;
        MessageAllPlayersReady = null;

        AttackHit = null;
        AttackMiss = null;
        AttackFatal = null;
        GameOver = null;
    }
}
