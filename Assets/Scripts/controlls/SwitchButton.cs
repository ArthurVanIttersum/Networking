using System;
using TMPro;
using UnityEngine;

public class SwitchButton : MonoBehaviour
{
    private TMP_Text textDisplay;
    
    public event Action switchButton;
    public string[] text = new string[2];
    public string type;

    void Start()
    {
        textDisplay = transform.GetComponentInChildren<TMP_Text>();
    }

    
    public void Switch()
    {
        switchButton?.Invoke();
    }

    public void UpdateText(bool yes)
    {
        if (yes)
        {
            textDisplay.text = text[0];
        }
        else
        {
            textDisplay.text = text[1];
        }
    }
}
