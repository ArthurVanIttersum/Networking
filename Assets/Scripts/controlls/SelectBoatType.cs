using UnityEngine;

public class SelectBoatType : MonoBehaviour
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
        if (type != "BoatType") return;
        if (!client.isConnected) return;
        if (client.GameNotRunning) return;
        
        if (client.localModel.gamestate != 0) return;
        client.localModel.currentlySelectedBoatType = (BoatData.Boats)(column + 1);
    }
}
