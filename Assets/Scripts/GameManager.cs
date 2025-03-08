using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    public int numberOfPlayers = 0;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F)) 
        {
            ChangePlayerModes(Mode.OFFENSE);
        }
    }
    public void ChangePlayerModes(Mode newMode)
    {
        foreach (var player in NetworkManager.Singleton.ConnectedClientsList)
        {
            PlayerController controller = player.PlayerObject.GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.SetPlayerMode(newMode);
            }
        }
    }
    public void AddPlayer()
    {
        numberOfPlayers++;
        Debug.Log("Player added. Total players: " + numberOfPlayers);
    }
}
