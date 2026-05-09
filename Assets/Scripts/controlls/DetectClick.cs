using System;
using UnityEngine.UI;
using UnityEngine;

public class DetectClick : MonoBehaviour
{
    public string type;
    [SerializeField] private int columnSize;
    [SerializeField] private int rowSize;

    private void OnEnable()
    {
        Click[] clickDetectors = transform.GetComponentsInChildren<Click>();
        for (int i = 0; i < clickDetectors.Length; i++)
        {
            clickDetectors[i].column = i % columnSize;
            clickDetectors[i].row = i / rowSize;
            clickDetectors[i].mainType = type;
        }
    }
}
