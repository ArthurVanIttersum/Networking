using UnityEngine;

public class Attack : MonoBehaviour
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
        if (type != "Tried") return;
        if (!client.isConnected) return;
        if (client.GameNotRunning) return;
        
        if (client.localModel.gamestate != 1) return;
        client.AttackLocationRequest(row, column);
    }
}
