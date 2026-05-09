using System;
using TMPro;
using UnityEngine;

public class SwitchOrientation : MonoBehaviour
{
    private TMP_Text textDisplay;
    private bool horizontal = true;
    public event Action<bool> switchOrientation;


    void Start()
    {
        textDisplay = transform.GetComponentInChildren<TMP_Text>();
    }

    
    public void Switch()
    {
        horizontal = !horizontal;
        switchOrientation.Invoke(horizontal);
        if (horizontal)
        {
            textDisplay.text = "Horizontal";
        }
        else
        {
            textDisplay.text = "Vertical";
        }
    }
}
