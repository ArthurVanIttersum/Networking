using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
using static TriedData;
using static UnityEngine.Rendering.DebugUI;
public class LocalModel
{
    //data structures
    public Board<BoatData.Boats> boardBoats = new();
    public Board<TriedData.Tried> boardTried = new();
    public Board<TriedData.Tried> boardOpponentTried = new();
    public BoatList boatList = new();//local caching own boat locations in a more structured format than the board

    //data sources
    public BoatData boatData;
    public TriedData triedData;
    public Client client;

    //views
    public DisplayGrid displayTried;
    public DisplayGrid displayBoats;
    public DisplayGrid displayOpponentTried;
    public DisplayText textDisplay;
    public SwitchOrientation orientation;

    //local values
    public int clientID;
    public BoatData.Boats currentlySelectedBoatType;
    public int gamestate = 0;
    public bool horizontal = true;

    public void SwitchOrientation(bool horizontal)
    {
        this.horizontal = horizontal;
    }

    public void Initialize(Client theClient)
    {
        client = theClient;
        client.AttackMiss += AttackMiss;
        client.AttackHit += AttackHit;
        client.AttackFatal += AttackFatal;
        client.MessagePlacementValid += BoatPlacementValid;
        client.MessageBoatRemoved += RemoveBoat;
        client.GameOver += GameOver;
        client.MessageAllPlayersReady += AllPlayersReady;


        orientation.switchOrientation += SwitchOrientation;


        displayTried.UpdateDisplay(boardTried, triedData.TriedToSprite);
        displayBoats.UpdateDisplay(boardBoats, boatData.BoatsToSprite);
        displayOpponentTried.UpdateDisplay(boardOpponentTried, triedData.TriedToSprite);
    }


    //state2
    public void AttackMiss(int row, int column, int origin)
    {
        textDisplay.UpdateDisplay("you attack something");
        if (origin == clientID)
        {
            textDisplay.UpdateDisplay("your attack missed");

            boardTried.WriteToGrid(row, column, TriedData.Tried.Miss);
            displayTried.UpdateDisplay(boardTried, triedData.TriedToSprite);
        }
        else
        {
            textDisplay.UpdateDisplay("opponent's attack missed");

            boardOpponentTried.WriteToGrid(row, column, TriedData.Tried.Miss);
            displayOpponentTried.UpdateDisplay(boardOpponentTried, triedData.TriedToSprite);
        }
    }
    public void AttackHit(int row, int column, int origin)
    {
        textDisplay.UpdateDisplay("you attack something");
        if (origin == clientID)
        {
            textDisplay.UpdateDisplay("your attack hit something");

            boardTried.WriteToGrid(row, column, TriedData.Tried.Hit);
            displayTried.UpdateDisplay(boardTried, triedData.TriedToSprite);
        }
        else
        {
            textDisplay.UpdateDisplay("opponent's attack hit something");

            boardOpponentTried.WriteToGrid(row, column, TriedData.Tried.Hit);
            displayOpponentTried.UpdateDisplay(boardOpponentTried, triedData.TriedToSprite);
        }

    }

    public void AttackFatal(int row, int column, int origin, BoatData.Boats type, bool horizontal)
    {
        textDisplay.UpdateDisplay("attack was fatal");

        if (origin == clientID)
        {
            Debug.Log("wow fatal yay you");
            textDisplay.UpdateDisplay("your attack was fatal");

            List<Coordinate> coordinates = boatList.GenerateCoordinateList(column, row, boatData.boatSizes[(int)type], horizontal);

            foreach (var coord in coordinates)
            {
                boardTried.WriteToGrid(coord.row, coord.column, TriedData.Tried.SunkenBoat);
            }
            displayTried.UpdateDisplay(boardTried, triedData.TriedToSprite);
        }
        else
        {
            Debug.Log("wow fatal yay other");
            textDisplay.UpdateDisplay("opponent's attack was fatal");

            Boat boat = boatList.FindBoat(row, column);

            foreach (var coord in boat.coordinates)
            {
                boardOpponentTried.WriteToGrid(coord.row, coord.column, TriedData.Tried.SunkenBoat);
            }

            displayOpponentTried.UpdateDisplay(boardOpponentTried, triedData.TriedToSprite);

        }
    }


    //state1
    public void BoatPlacementValid(BoatData.Boats boatType, int row, int column, bool horizontal)
    {
        textDisplay.UpdateDisplay("placing boat");
        List<Coordinate> coordinates = boatList.GenerateCoordinateList(column, row, boatData.boatSizes[(int)boatType], horizontal);

        boatList.boats.Add(new Boat(boatType, row, column, coordinates, horizontal));
        foreach (Coordinate coord in coordinates)//execution
        {
            boardBoats.WriteToGrid(coord.row, coord.column, boatType);
        }

        displayBoats.UpdateDisplay(boardBoats, boatData.BoatsToSprite);
        displayBoats.UpdateBoatSize(boatList);
    }

    public void RemoveBoat(int row, int column)
    {
        textDisplay.UpdateDisplay("removing boat");
        Boat boat = boatList.FindBoat(row, column);
        foreach (var coord in boat.coordinates)
        {
            boardBoats.WriteToGrid(coord.row, coord.column, BoatData.Boats.Empty);
        }
        boatList.boats.Remove(boat);

        displayBoats.UpdateDisplay(boardBoats, boatData.BoatsToSprite);
        displayBoats.UpdateBoatSize(boatList);
    }

    public void AllPlayersReady(string text, int playerID)
    {
        gamestate = 1;
        if (playerID == clientID)
        {
            textDisplay.UpdateDisplay(text + " You can shoot first");
        }
        else
        {
            textDisplay.UpdateDisplay(text + " Your opponent can shoot first");
        }

    }

    //any state

    public void GameOver(string text, int playerID)
    {
        if (playerID == clientID)
        {
            textDisplay.UpdateDisplay(text + " you won");
        }
        else
        {
            textDisplay.UpdateDisplay(text + " you lost");
        }
        client.DisposeModel();
    }

    public void Dispose()
    {
        //reset displays
        displayTried.UpdateDisplay(new Board<Tried>(), triedData.TriedToSprite);
        displayBoats.UpdateBoatSize(new BoatList());
        displayBoats.UpdateDisplay(new Board<BoatData.Boats>(), boatData.BoatsToSprite);
        displayOpponentTried.UpdateDisplay(new Board<Tried>(), triedData.TriedToSprite);
        textDisplay.UpdateDisplay("DefaultText");
        

        //clear connections
        if (client != null)
        {
            client.AttackMiss -= AttackMiss;
            client.AttackHit -= AttackHit;
            client.AttackFatal -= AttackFatal;
            client.MessagePlacementValid -= BoatPlacementValid;
            client.MessageBoatRemoved -= RemoveBoat;
            client.GameOver -= GameOver;
            client.MessageAllPlayersReady -= AllPlayersReady;
        }

        
        orientation.switchOrientation -= SwitchOrientation;

        client = null;
    }

}
