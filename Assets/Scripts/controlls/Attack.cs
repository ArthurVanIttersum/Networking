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
        if (type != "Tried") return;//check if we're clicking on the right board
        if (!client.isConnected) return;//check if the player is connected
        if (client.GameNotRunning) return;//check if the game is running
        if (client.localModel.gamestate != 1) return;//check if the game is in the attack state
        if (!client.localModel.boardTried.IsInBounds(row, column)) return;//check if the attack is in the bounds of the grid
        if (client.localModel.boardTried.SampleGrid(row, column) != TriedData.Tried.Empty) return;//check if the attack has been tried already
        
        client.AttackLocationRequest(row, column);
    }
}
