using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using static BoatData;


public class DisplayGrid : MonoBehaviour
{
    private Image []images;
    private Client theClient;
    private BoatData boatData;
    [SerializeField] public string type;
    [SerializeField] private int columnSize;
    [SerializeField] private int rowSize;
    

    private void Start()
    {
        images = transform.GetComponentsInChildren<Image>();
        theClient = FindFirstObjectByType<Client>();
        boatData = FindFirstObjectByType<BoatData>();
    }

    public void UpdateDisplay<T>(Board<T> board, Func<T, Sprite> converter)
    { 
        for (int i = 0; i < images.Length; i++)
        {
            int column = i % columnSize;
            int row = i / rowSize;
            T value = board.SampleGrid(row, column);
            
            
            images[i].sprite = converter(value);
        }
        
    }

    public void UpdateBoatSize(BoatList list)//only for boats
    {
        for (int i = 0; i < images.Length; i++)
        {
            int column = i % columnSize;
            int row = i / rowSize;

            images[i].rectTransform.localScale = Vector3.one;
            images[i].color = Color.clear;
        }
        foreach (Boat boat in list.boats)
        {
            Image image = images[boat.row * columnSize + boat.column];
            int boatSize = boatData.boatSizes[(int)boat.boatType];
            image.rectTransform.localScale = new Vector3(boatSize, 1, 1);
            image.color = Color.white;
            if (boat.horizontal)
            {
                image.rectTransform.rotation = Quaternion.identity;
                
            }
            else
            {
                image.rectTransform.rotation = Quaternion.Euler(0, 180, -90);
                
            }
        }
    }
}
