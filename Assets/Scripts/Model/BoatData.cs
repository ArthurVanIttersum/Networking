using UnityEngine;

public class BoatData : MonoBehaviour
{
    public Sprite[] boatsSprites;
    public int[] boatSizes;

    public Sprite BoatsToSprite(Boats value)
    {
        return boatsSprites[(int)value];
    }

    public enum Boats
    {
        Empty,
        Carrier,
        Battleship,
        Destroyer,
        Submarine,
        PatrolBoat
    }
}
