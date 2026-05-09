using UnityEngine;

/// <summary>
/// The scene starts with a SessionManager, which allows use to choose whether this instance
/// will be client, server or both (=host).
/// </summary>
public class SessionManager : MonoBehaviour
{
	
	bool IsClient = false;
	bool IsServer = false;
    GUIStyle bigButton;

    void OnGUI()
    {
        if (bigButton == null)
        {
            bigButton = new GUIStyle(GUI.skin.button);
            bigButton.fontSize = 14; // make text bigger
        }

        GUILayout.BeginArea(new Rect(300, 20, 500, 500));

        if (!IsClient && !IsServer)
        {
            StartButtons();
        }

        GUILayout.EndArea();
    }

    void StartButtons()
    {
        if (GUILayout.Button("Host", bigButton, GUILayout.Height(30)))
        {
            StartServer();
            StartClient();
        }
        if (GUILayout.Button("Client", bigButton, GUILayout.Height(30)))
        {
            StartClient();
        }
        if (GUILayout.Button("Server", bigButton, GUILayout.Height(30)))
        {
            StartServer();
        }
    }



    void StartServer() {
		Debug.Log("Starting server: creating board");

		Server server = GetComponent<Server>();
		server.enabled = true;

		//var boardOwner = FindFirstObjectByType<ModelOwner>();
		//boardOwner.enabled = true;

		IsServer = true;
	}
	void StartClient() {
		Debug.Log($"Starting client: enabling controller");

		Client client = GetComponent<Client>();
		client.enabled = true;

		

		IsClient = true;
	}	
}
