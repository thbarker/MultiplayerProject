using UnityEngine;
using Unity.Netcode;
using System.Collections;
using TMPro;

/// <summary>
/// This script is designed to handle the gameplay and interactions
/// between the two players.
/// </summary>
public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }
    private PlayerController player1, player2;
    private ulong player1Id, player2Id;
    public TextMeshProUGUI roundMessage;
    public TextMeshProUGUI playerScore;
    public TextMeshProUGUI enemyScore;

    public int numberOfPlayers = 0;
    private bool hasFired = false;
    private float timeCounter = 0f;
    private bool gameStarted = false;

    public float roundStartTime = 5f;
    public float roundTime = 30f;
    public float roundEndTime = 5f;
    public float restartTime = 5f;
    public int totalRounds = 7;

    public AudioSource ticking, buzzer;

    void Awake()
    {
        // Ensure this object is a singleton
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

    public override void OnNetworkSpawn()
    {
        // Set up client connections here
        RegisterNetworkEvents();
    }
    /// <summary>
    /// Client connection and disconnection handling
    /// </summary>
    private void RegisterNetworkEvents()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }
    /// <summary>
    /// Add a Player if a client connects
    /// </summary>
    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client connected: {clientId}");
        AddPlayer();
    }
    /// <summary>
    /// Remove a Player if a client disconnects
    /// </summary>
    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client disconnected: {clientId}");
        RemovePlayer();
    }

    private void Update()
    {
        if (!gameStarted && numberOfPlayers == 2 && IsServer) 
        {
            // Start the game when 2 players are connected and no game has been started
            StartCoroutine(GameplayLoop());
        }
    }
    /// <summary>
    /// Function to change the players mode 
    /// </summary>
    public void ChangePlayerMode(PlayerController player, Mode newMode)
    {
        player.SetPlayerMode(newMode);
    }

    /// <summary>
    /// Adds a player to the game's numberOfPlayers counter
    /// </summary>
    public void AddPlayer()
    {
        if (!IsServer)
            return;
        if(numberOfPlayers <= 2)
        {
            numberOfPlayers++;
            Debug.Log("Player added. Total players: " + numberOfPlayers);
        }
    }

    /// <summary>
    /// Removes a player to the game's numberOfPlayers counter
    /// </summary>
    public void RemovePlayer()
    {
        if (!IsServer)
            return;
        numberOfPlayers--;
        Debug.Log("Player removed. Total players: " + numberOfPlayers);
        if(numberOfPlayers < 2)
        {
            ChangeRoundMessageTextClientRpc("Waiting for Players...");
            StopAllCoroutines();
            gameStarted = false;
        }
    }
    /// <summary>
    /// Register that a player has already fired, so it can progress the round
    /// </summary>
    public void UpdatePlayerFired(bool flag)
    {
        hasFired = flag;
    }
    /// <summary>
    /// Coroutine for the entire Gameplay Loop. Meant to be started when 2
    /// players connect, and a gameplayloop isn't already running.
    /// </summary>
    private IEnumerator GameplayLoop()
    {
        // Update gameStarted flag
        gameStarted = true;

        // Retrieve player references randomly for random team selection
        int index1 = UnityEngine.Random.Range(0, 2);
        int index2 = Mathf.Abs(index1 - 1);
        player1 = NetworkManager.Singleton.ConnectedClientsList[index1].PlayerObject.GetComponent<PlayerController>();
        player2 = NetworkManager.Singleton.ConnectedClientsList[index2].PlayerObject.GetComponent<PlayerController>();
        player1Id = NetworkManager.Singleton.ConnectedClientsIds[index1];
        player2Id = NetworkManager.Singleton.ConnectedClientsIds[index2];

        // Initialize starting teams based on random selection
        ChangePlayerMode(player1, Mode.OFFENSE);
        ChangePlayerMode(player2, Mode.DEFENSE);

        // Reset Scores to 0
        player1.score.Value = 0;
        player2.score.Value = 0;

        // Update Scoreboards
        UpdateScoresTextClientRpc(player1Id, player1.score.Value, player2.score.Value);
        UpdateScoresTextClientRpc(player2Id, player2.score.Value, player1.score.Value);

        // Determine Game Over Condition
        int victoryScore = (totalRounds + 1) / 2;

        while (player1.score.Value < victoryScore && player1.score.Value < victoryScore)
        {
            // Make sure both players are registered as alive
            player1.dead.Value = false;
            player2.dead.Value = false;

            // Play Ticking Audio
            PlayTickingClientRpc();

            // Countdown roundStartTime
            timeCounter = 0; // roundTime should be defined as the duration of the round in seconds
            while (timeCounter < roundStartTime)
            {
                int timer = 1 + (int)(roundStartTime - timeCounter);
                ChangeRoundMessageTextClientRpc(timer.ToString());
                timeCounter += Time.deltaTime;
                yield return null;
            }

            // Update Message
            ChangeRoundMessageTextClientRpc("GO!");

            // Allow players to fire
            player1.canFire.Value = true;
            player2.canFire.Value = true;

            // Play Buzzer Audio
            PlayBuzzerClientRpc();

            // Set initial round time
            timeCounter = 0;

            // Countdown roundtime if player hasn't shot using hasFired bool
            while (timeCounter < roundTime && !hasFired)
            {
                timeCounter += Time.deltaTime;
                yield return null;
            }

            // Update Round Message Appropriately
            if (player1.dead.Value || player2.dead.Value)
                ChangeRoundMessageTextClientRpc("Headshot!");
            else if (hasFired)
                ChangeRoundMessageTextClientRpc("Miss!");
            else
            {
                ChangeRoundMessageTextClientRpc("Times Up!");
                // Play Buzzer Audio when times up
                PlayBuzzerClientRpc();
            }

            // Update Scoreboards
            UpdateScoresTextClientRpc(player1Id, player1.score.Value, player2.score.Value);
            UpdateScoresTextClientRpc(player2Id, player2.score.Value, player1.score.Value);

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
        // Reset Players to Waiting
        ChangePlayerMode(player1, Mode.WAITING);
        ChangePlayerMode(player2, Mode.WAITING);

        // Update Game Over Text
        if (player1.score.Value > player2.score.Value)
        {
            GameOverTextClientRpc(true, player1Id);
            GameOverTextClientRpc(false, player2Id);
        } else
        {
            GameOverTextClientRpc(true, player2Id);
            GameOverTextClientRpc(false, player1Id);
        }
        // Pause for a moment before restarting game
        yield return new WaitForSeconds(restartTime);

        // Update the game started flag
        gameStarted = false;
    }
    /// <summary>
    /// Updates the Round message on the clients
    /// </summary>
    [ClientRpc]
    private void ChangeRoundMessageTextClientRpc(string message)
    {
        roundMessage.text = message;
    }
    /// <summary>
    /// Updates the scores on the client, based upon their ID, Score, and Opponent score
    /// </summary>
    [ClientRpc]
    private void UpdateScoresTextClientRpc(ulong playerId, int playerScoreParam, int enemyScoreParam)
    {
        if (NetworkManager.Singleton.LocalClientId == playerId)
        {
            playerScore.text = playerScoreParam.ToString();
            enemyScore.text = enemyScoreParam.ToString();
        }
    }
    /// <summary>
    /// Updates the Round Message for a Game Over message for the clients,
    /// depending on which client won or lost
    /// </summary>
    [ClientRpc]
    private void GameOverTextClientRpc(bool win, ulong playerId)
    {
        if(NetworkManager.Singleton.LocalClientId == playerId)
        {
            if (win)
            {
                roundMessage.text = "You Win!";
            } else
            {
                roundMessage.text = "You Lose!";
            }
        }
    }
    /// <summary>
    /// Handle Ticking Audio on Clients
    /// </summary>
    [ClientRpc]
    private void PlayTickingClientRpc()
    {
        ticking.Play();
    }
    /// <summary>
    /// Handle Buzzing Audio on Clients
    /// </summary>
    [ClientRpc]
    private void PlayBuzzerClientRpc()
    {
        buzzer.Play();
    }

}
