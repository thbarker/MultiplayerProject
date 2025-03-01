using Unity.Netcode;
using UnityEngine;

public class PlayerSpawner : NetworkBehaviour
{
    public GameObject playerPrefab;
    public Vector3 spawnA, spawnB;
    private int playersSpawned = 0;
    
    public override void OnNetworkSpawn()
    {
        if (IsServer) // Only the server should spawn the players
        {
            NetworkManager.Singleton.OnClientConnectedCallback += SpawnPlayer;
        }
    }

    private void SpawnPlayer(ulong clientId)
    {
        if (playersSpawned >= 2) return; // Only spawn two players

        // Choose spawn point based on the number of players spawned
        Vector3 spawnPoint = (playersSpawned == 0) ? spawnA : spawnB;

        // Spawn the player at the chosen position
        GameObject playerInstance = Instantiate(playerPrefab, spawnPoint, Quaternion.identity);
        playerInstance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);

        playersSpawned++;
    }
}
