
using System.Collections.Generic;

public class BoatList//list of all boats a player has
{
    public List<Boat> boats = new();

    public Boat FindBoat(int row, int column)//find a boat at a certain position
    {
        foreach (Boat boat in boats)
        {
            foreach (Coordinate coordinates in boat.coordinates)
            {
                if (coordinates.row == row && coordinates.column == column)
                {
                    return boat;
                }
            }
        }
        return null;
    }
    public bool IsAllSunk()//test if all boats are sunk
    {
        foreach (var boat in boats)
        {
            if (!boat.sunk) return false;
        }
        return true;
    }

    public bool IsValidSet(out string error)
    {
        if (boats.Count != 5)//quick check if the number of boats is right
        {
            error = "Number of boats is incorrect.";
            return false;
        }
        for (int i = 1; i < 6; i++)//loop through the boat types
        {
            BoatData.Boats boatType = (BoatData.Boats)i;
            Boat boatFound = boats.Find(t => t.boatType == boatType);
            if (boatFound == null)
            {
                error = "Not all boat types are present.";
                return false;
            }
        }

        error = "";
        return true;//if no problem found return true
    }

    public List<Coordinate> GenerateCoordinateList(int column, int row, int size, bool horizontal)//to reduce code duplication
    {
        List<Coordinate> coordinates = new();
        for (int i = 0; i < size; i++)//validation
        {
            if (horizontal)
            {
                coordinates.Add(new Coordinate(row, column + i));
            }
            else
            {
                coordinates.Add(new Coordinate(row + i, column));
            }
        }
        return coordinates;
    }
}

public class Boat//datastructure for a boat
{
    public Boat(BoatData.Boats boatType, int row, int column, List<Coordinate> coordinates, bool horizontal)
    {
        this.boatType = boatType;
        this.row = row;
        this.column = column;
        this.coordinates = coordinates;
        this.horizontal = horizontal;
    }

    public BoatData.Boats boatType;
    public int row;
    public int column;
    public List<Coordinate> coordinates;
    public bool sunk = false;
    public bool horizontal;
}
public class Coordinate//datastructure for a simple coordinate
{
    public Coordinate(int row, int column)
    {
        this.row = row;
        this.column = column;
    }
    public int row;
    public int column;
}
