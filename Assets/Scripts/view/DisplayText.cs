using TMPro;
using UnityEngine;

public class DisplayText : MonoBehaviour
{
    TMP_Text textDisplay;
    
    void Start()
    {
        textDisplay = transform.GetComponent<TMP_Text>();
    }

    public void UpdateDisplay(string newText)
    {
        textDisplay.text = newText;
    }

}
