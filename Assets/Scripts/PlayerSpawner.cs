using Unity.Netcode;
using UnityEngine;

public class PlayerSpawner : NetworkBehaviour
{
    public GameObject playerPrefab; // Assign this in the Unity editor with your player prefab
    public Transform spawnA; // Assign in Unity editor, initial spawn point for the first player
    public Transform spawnB; // Assign in Unity editor, spawn point for all subsequent players

    private bool isFirstPlayerSpawned = false; // To track if the first player has been spawned

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        // Determine the spawn point based on whether the first player has been spawned
        Transform spawnPoint = isFirstPlayerSpawned ? spawnB : spawnA;

        // Spawn the player at the determined spawn point
        GameObject player = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);

        // Update the flag to ensure the next player spawns at spawnB
        isFirstPlayerSpawned = true;
    }

    void OnDestroy()
    {
        // Clean up the event callback when the spawner is destroyed
        if (NetworkManager.Singleton != null && IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        }
    }
}
