using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class Click : MonoBehaviour, IPointerClickHandler
{
    public int row;
    public int column;
    public string mainType;
    public static event Action<int, int, string> OnClick;

    public void OnPointerClick(PointerEventData eventData)
    {
        
        OnClick.Invoke(row, column, mainType);
    }
}
