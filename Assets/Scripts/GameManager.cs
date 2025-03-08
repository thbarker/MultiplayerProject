using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public float roundTime = 30f; // Duration of each round
    private List<ulong> playerClientIds = new List<ulong>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        }
    }

    private void OnDestroy()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        StartCoroutine(WaitAndAddPlayer(clientId));
    }

    IEnumerator WaitAndAddPlayer(ulong clientId)
    {
        // Wait until the client's player object is spawned
        while (NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject == null)
        {
            yield return null;
        }

        if (NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
        {
            playerClientIds.Add(clientId);
            if (playerClientIds.Count == 2)
            {
                StartCoroutine(GameplayLoop());
            }
        }
    }

    IEnumerator GameplayLoop()
    {
        if (!IsServer)
            yield break;  // Ensure this loop only runs on the server

        int offenseIndex = Random.Range(0, 2);
        int defenseIndex = 1 - offenseIndex;

        while (true)  // Loop indefinitely or until a game end condition is met
        {
            SetPlayerMode(playerClientIds[offenseIndex], true);
            SetPlayerMode(playerClientIds[defenseIndex], false);

            yield return new WaitForSeconds(roundTime);

            // Swap roles after each round
            int temp = offenseIndex;
            offenseIndex = defenseIndex;
            defenseIndex = temp;
        }
    }

    private void SetPlayerMode(ulong clientId, bool offense)
    {
        /*
        var player = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
        if (player != null && IsServer)  // Ensure code execution on the server
        {
            player.GetComponent<PlayerController>().IsOffense.Value = offense;
        }*/
    }
}
