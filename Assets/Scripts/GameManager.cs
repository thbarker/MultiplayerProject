using UnityEngine;
using Unity.Netcode;
using System.Collections;
using TMPro;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }
    private PlayerController player1, player2;
    public TextMeshProUGUI roundMessage;

    public int numberOfPlayers = 0;
    private bool hasFired = false;
    private float timeCounter = 0f;
    private bool gameStarted = false;

    public float roundStartTime = 5f;
    public float roundTime = 30f;
    public float roundEndTime = 5f;

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
        if (!gameStarted && numberOfPlayers == 2 && IsServer) 
        {
            gameStarted = true;
            ChangePlayerMode(player1, Mode.OFFENSE);
            ChangePlayerMode(player2, Mode.DEFENSE);
            StartCoroutine(GameplayLoop());
        }
    }

    public void ChangePlayerMode(PlayerController player, Mode newMode)
    {
        player.SetPlayerMode(newMode);
    }
    public void AddPlayer()
    {
        if (!IsServer)
            return;
        if(numberOfPlayers <= 2)
        {
            numberOfPlayers++;
            Debug.Log("Player added. Total players: " + numberOfPlayers);
        }
        if (numberOfPlayers == 2)
        {
            int index1 = Random.Range(0, 2);
            int index2 = Mathf.Abs(index1 - 1);
            player1 = NetworkManager.Singleton.ConnectedClientsList[index1].PlayerObject.GetComponent<PlayerController>();
            player2 = NetworkManager.Singleton.ConnectedClientsList[index2].PlayerObject.GetComponent<PlayerController>();
        }
    }
    public void UpdatePlayerFired(bool flag)
    {
        hasFired = flag;
    }

    private IEnumerator GameplayLoop()
    {
        while (true)
        {
            player1.dead.Value = false;
            player2.dead.Value = false;
            // Countdown roundStartTime
            timeCounter = 0; // roundTime should be defined as the duration of the round in seconds
            while (timeCounter < roundStartTime)
            {
                int timer = 1 + (int)(roundStartTime - timeCounter);
                ChangeRoundMessageTextClientRpc(timer.ToString());
                timeCounter += Time.deltaTime;
                yield return null;
            }

            ChangeRoundMessageTextClientRpc("GO!");
            // Allow players to fire
            player1.canFire.Value = true;
            player2.canFire.Value = true;
            // Set initial round time
            timeCounter = 0; // roundTime should be defined as the duration of the round in seconds

            // Countdown roundtime if player hasn't shot using hasFired bool
            while (timeCounter < roundTime && !hasFired)
            {
                timeCounter += Time.deltaTime;
                yield return null;
            }

            if(player1.dead.Value || player2.dead.Value)
                ChangeRoundMessageTextClientRpc("Headshot!");
            else
                ChangeRoundMessageTextClientRpc("Miss!");

            // Reset player's fire ability for the next round
            player1.canFire.Value = false;
            player2.canFire.Value = false;
            hasFired = false;

            yield return new WaitForSeconds(roundEndTime);

            // Check who was on offense and switch sides
            if (player1.mode.Value == Mode.OFFENSE)
            {
                ChangePlayerMode(player1, Mode.DEFENSE);
                ChangePlayerMode(player2, Mode.OFFENSE);
            }
            else
            {
                ChangePlayerMode(player1, Mode.OFFENSE);
                ChangePlayerMode(player2, Mode.DEFENSE);
            }


        }
    }

    [ClientRpc]
    private void ChangeRoundMessageTextClientRpc(string message)
    {
        roundMessage.text = message;
    } 

}
