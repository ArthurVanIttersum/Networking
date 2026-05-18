
using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

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

    public int currentPlayerID = 0;
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
            return;
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
        if (!BoatBoards[id].IsInBounds(row, column)) return; //check if the coordinate is in the bounds of the grid
        Boat boat = boatLists[id].FindBoat(row, column);

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
        string message;
        if (!boatLists[id].IsValidSet(out message))//player does not have a valid set of boats
        {
            server.MessageBadBoat(message, origin);
            return;
        }

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
        if (!IsStateRight(1)) return;//check if the game is in the right state

        int id = server.GetPlayerIDFromEP(origin);
        int otherid = 1 - id;//ids are either 0 or 1. 1-0=1, 1-1=0.

        if (id != currentPlayerID) return;//ignore messages from the wrong player
        if (!BoatBoards[otherid].IsInBounds(row, column)) return; //check if the attack is valid
        if (TriedBoards[otherid].SampleGrid(row, column) != TriedData.Tried.Empty) return;//check if the coordinate has been attacked already



        BoatData.Boats boatData = BoatBoards[otherid].SampleGrid(row, column);

        //send message back

        if (boatData == BoatData.Boats.Empty)//if it's a miss
        {
            TriedBoards[otherid].WriteToGrid(row, column, TriedData.Tried.Miss);
            server.AttackMiss(row, column, currentPlayerID);
        }
        else//if it's a hit
        {
            TriedBoards[otherid].WriteToGrid(row, column, TriedData.Tried.Hit);
            Boat boat = boatLists[otherid].FindBoat(row, column);

            //test if the attack is fatal
            bool isFatal = true;
            foreach (Coordinate coord in boat.coordinates)
            {
                if (!TriedBoards[otherid].IsInBounds(coord.row, coord.column)) return;
                if (TriedBoards[otherid].SampleGrid(coord.row, coord.column) == TriedData.Tried.Empty)
                { isFatal = false; break; }
            }

            if (isFatal)//if it's fatal
            {
                foreach (Coordinate coord in boat.coordinates)
                {
                    TriedBoards[otherid].WriteToGrid(coord.row, coord.column, TriedData.Tried.SunkenBoat);
                }
                boat.sunk = true;
                server.AttackFatal(boat.row, boat.column, currentPlayerID, boat.boatType, boat.horizontal);
                if (boatLists[otherid].IsAllSunk())
                {
                    server.GameOver("game over", id);
                    server.EndGame();
                }
            }
            else//if it's not fatal
            {
                server.AttackHit(row, column, currentPlayerID);
            }
        }

        //switch current player
        currentPlayerID = otherid;
    }

    public bool IsStateRight(int gameState)//helper function
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
