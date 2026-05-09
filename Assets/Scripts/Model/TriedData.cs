using UnityEngine;
public class TriedData : MonoBehaviour
{
    public Sprite[] triedSprites;
    
    public Sprite TriedToSprite(Tried value)
    {
        return triedSprites[(int)value];
    }

    public enum Tried
    {
        Empty,
        Miss,
        Hit,
        SunkenBoat
    }
}
