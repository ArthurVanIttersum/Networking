using System.Drawing;
using UnityEngine;

public class PlaceBoat : MonoBehaviour
{
    Client client;
    private void Start()
    {
        client = FindFirstObjectByType<Client>();
    }
    private void OnEnable()
    {
        Click.OnClick += HandleClick;
    }

    private void OnDisable()
    {
        Click.OnClick -= HandleClick;
    }

    private void HandleClick(int row, int column, string type)
    {
        if (type != "Boats") return;
        if (!client.isConnected) return;
        if (client.GameNotRunning) return;
        if (client.localModel.gamestate != 0) return;

        if (!client.localModel.boardBoats.IsInBounds(row, column)) return;
        if (client.localModel.boardBoats.SampleGrid(row, column) == BoatData.Boats.Empty)
        {
            client.ChooseBoatLocationRequest(client.localModel.currentlySelectedBoatType, row, column, client.localModel.horizontal);
        }
        else
        {
            client.RemoveBoatRequest(row, column);
        }
    }
}
