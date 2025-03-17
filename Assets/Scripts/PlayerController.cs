using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Enumerator for the different player modes to specify which inputs are allowed
/// </summary>
public enum Mode
{
    WAITING,
    OFFENSE,
    DEFENSE
}

/// <summary>
/// This is the Player Controller Logic. It is server-authoritative for most 
/// functionalities including animation, rb movement, firing, getting shot, 
/// and scoring. Camera movement and Audio remain client sided.
/// </summary>
public class PlayerController : NetworkBehaviour
{
    // Tunables
    [SerializeField]
    [Range(0f, 500f)]
    private float mouseSensitivity = 100.0f;
    [SerializeField]
    [Range(0f, 10f)]
    private float speed = 5.0f;

    // Helper variables
    private float currentPitch = 0.0f;
    private float currentYaw = 0.0f;

    // Network Variables
    public NetworkVariable<Mode> mode = new NetworkVariable<Mode>();
    public NetworkVariable<bool> canFire = new NetworkVariable<bool>();
    public NetworkVariable<bool> dead = new NetworkVariable<bool>();
    public NetworkVariable<int> score = new NetworkVariable<int>();

    // References
    [SerializeField]
    private SkinnedMeshRenderer meshRenderer;
    private GameManager gameManager;
    private GameObject playerCamera;
    private Rigidbody rb;
    private Animator animator;
    private GameObject canvas;
    private GameObject crosshair;
    private GameObject deathImage;
    [SerializeField]
    private AudioSource audioSource;
    [SerializeField]
    private AudioClip gunshot, fall, grunt, whiz1, whiz2, whiz3, footstep1, footstep2, footstep3;

    private void Start()
    {
        // Get references to components
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        playerCamera = transform.Find("Camera").gameObject;
        gameManager = GameObject.FindWithTag("GameManager").GetComponent<GameManager>();
        // Initialize mode
        mode.Value = Mode.WAITING;
    }
    public override void OnNetworkSpawn()
    {
        // Handle specific client initializations in OnNetworkSpawn 
        // because in Start, the network flag might not be initialized
        if (IsLocalPlayer)
        {
            // Local Player can't see their own mesh
            meshRenderer.enabled = false;

            // Initialize UI references
            canvas = GameObject.FindWithTag("Canvas");
            if (canvas)
            {
                crosshair = canvas.transform.Find("Crosshair").gameObject;
                deathImage = canvas.transform.Find("DeathImage").gameObject;
            }
            else
            {
                Debug.Log("No Canvas found");
            }

            // Lock cursor to the center of the screen
            Cursor.lockState = CursorLockMode.Locked;
        }
        // Hide cursor from view
        Cursor.visible = false;
    }

    void Update()
    {
        if (IsClient && IsOwner) // Check if this is the client and it owns this GameObject
        {
            // Input Handling
            HandleInput();
            // UI Updates
            UpdateUI();
            // Animation Updates
            UpdateAnimationServerRpc(currentYaw, currentPitch);
        }
    }

    /// <summary>
    /// Updates the UI every frame so the crosshair is only visible on 
    /// Offense, and the death screen only appears on a death sequence.
    /// </summary>
    private void UpdateUI()
    {
        if (!IsLocalPlayer)
            return;
        if (!canvas || !deathImage)
            return;
        if (dead.Value)
        {
            deathImage.SetActive(true);
        }
        else
        {
            deathImage.SetActive(false);
        }
        if (mode.Value == Mode.OFFENSE)
        {
            crosshair.SetActive(true);
        }
        else
        {
            crosshair.SetActive(false);
        }
    }
    /// <summary>
    /// Handles the player's mode on the client and calls appropriate 
    /// functions to handle additional mode logic
    /// </summary>
    public void SetPlayerMode(Mode newMode)
    {
        if (IsClient)
        {
            mode.Value = newMode;
            if (mode.Value == Mode.OFFENSE)
            {
                SetOffense();
            }
            if (mode.Value == Mode.DEFENSE)
            {
                SetDefense();
            }
        }
    }
    /// <summary>
    /// Resets the Camera Rotation and Player Position when on offense
    /// </summary>
    private void SetOffense()
    {
        ResetPositionServerRpc();
        ResetCameraRotation();
    }
    /// <summary>
    /// Resets the Camera Rotation and Player Position when on defense
    /// </summary>
    private void SetDefense()
    {
        ResetPositionServerRpc();
        ResetCameraRotation();
    }
    /// <summary>
    /// Makes sure player can fire again on the server side.
    /// </summary>
    public void SetCanFire(bool flag)
    {
        if (IsServer)
        {
           canFire.Value = flag;
        }
    }

    /// <summary>
    /// Call appropriate movement ServerRpc's based on input, and 
    /// adjust the camera on the client side.
    /// </summary>
    void HandleInput()
    {
        // Handle movement
        if (Input.GetKey(KeyCode.A))
        {
            MovePlayerServerRpc(-1);
        }
        else if (Input.GetKey(KeyCode.D))
        {
            MovePlayerServerRpc(1);
        } else
        {
            MovePlayerServerRpc(0);
        }

        // Handle aiming and firing
        if (IsLocalPlayer)
        {
            AimPlayer();  // Local aiming logic remains client-side for responsiveness

            if (Input.GetMouseButtonDown(0))
            {
                Vector3 aimDirection = playerCamera.transform.forward;
                FireServerRpc(aimDirection); // Fire action is server validated
            }
        }
    }
    /// <summary>
    /// Update the animations on the server so that the network 
    /// animator can handle the client updates
    /// </summary>
    [ServerRpc]
    void UpdateAnimationServerRpc(float yaw, float pitch)
    {
        UpdateAnimation(yaw, pitch); // Update server-side
    }

    /// <summary>
    /// Logic called by a ServerRpc for animator variables
    /// </summary>
    void UpdateAnimation(float yaw, float pitch)
    {
        // Update movement animations based on rb speed
        Vector3 localVelocity = transform.InverseTransformDirection(rb.velocity);
        animator.SetFloat("Speed", localVelocity.x);

        if (mode.Value == Mode.OFFENSE)
        {
            // Update aiming blend space when on offense
            animator.SetFloat("Speed", 0);
            animator.SetBool("Aiming", true);
            animator.SetFloat("AimX", yaw / 90);
            animator.SetFloat("AimY", -pitch / 90);
        }
        else
        {
            // Reset aiming to center when not on offense
            animator.SetBool("Aiming", false);
            animator.SetFloat("AimX", 0);
            animator.SetFloat("AimY", 0);
        }
    }
    /// <summary>
    /// Set Camera Look Forward
    /// </summary>
    private void ResetCameraRotation()
    {
        currentPitch = 0;
        currentYaw = 0;
        playerCamera.transform.localRotation = Quaternion.identity;
    }
    /// <summary>
    /// Set Position back to center on the server
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void ResetPositionServerRpc()
    {
        if (IsServer)
        {
            transform.position = new Vector3(0, transform.position.y, transform.position.z);
            rb.velocity = Vector3.zero;
            animator.SetBool("Dead", false);
        }
    }
    /// <summary>
    /// ServerRpc controls the Movement Code
    /// </summary>
    [ServerRpc]
    private void MovePlayerServerRpc(int direction)
    {
        if (IsServer && mode.Value == Mode.DEFENSE) // Ensure this code is executed on the server
        {
            if (dead.Value)
                return;
            // Use transform.right for movement relative to the object's orientation
            Vector3 movement = transform.right * direction * speed;
            rb.velocity = movement;
        }
    }
    /// <summary>
    /// Client controls the Aiming Code
    /// </summary>
    private void AimPlayer()
    {
        if (!IsLocalPlayer)
            return;
        if (mode.Value == Mode.OFFENSE) // Ensure this code is only run in Offense mode
        {
            // Handling mouse aiming
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

            currentPitch -= mouseY; // Subtracting to invert the vertical input
            currentYaw += mouseX;

            // Clamping the pitch and yaw
            currentPitch = Mathf.Clamp(currentPitch, -45f, 45f);
            currentYaw = Mathf.Clamp(currentYaw, -45f, 45f);

        } else if (mode.Value != Mode.OFFENSE)
        {
            currentPitch = 0; // Subtracting to invert the vertical input
            currentYaw = 0;
        }
        Quaternion targetRotation = Quaternion.Euler(currentPitch, currentYaw, 0);
        playerCamera.transform.localRotation = targetRotation;
    }
    /// <summary>
    /// ServerRpc controls the Firing
    /// </summary>
    [ServerRpc]
    void FireServerRpc(Vector3 aimDirection)
    {
        if (!IsServer)
            return;
        if (mode.Value == Mode.OFFENSE && canFire.Value)
        {
            // Play gunshot audio
            PlayGunshotClientRpc();

            // Perform a raycast
            RaycastHit hit;
            Ray ray = new Ray(playerCamera.transform.position, aimDirection.normalized);  // Use normalized direction
            float maxDistance = 100.0f;
            if (Physics.Raycast(ray, out hit, maxDistance))
            {
                // Detect hitting the enemy
                var hitPlayer = hit.collider.GetComponent<HitBox>(); 
                Debug.Log("Hit " + hit.collider.name);
                if (hitPlayer != null && hitPlayer != this)  // Ensure not hitting self
                {
                    // Hit the player
                    hitPlayer.GetShotHitBox();
                    // Update Score
                    IncrementScoreServerRpc();
                } else
                {
                    // If they miss the target, play the whiz audio
                    PlayWhizClientRpc();
                }
            }
            else
            {
                // If they miss the target and don't hit anything at all, play the whiz audio
                PlayWhizClientRpc();
                Debug.Log("No hit");
            }
            // Disallow another shot
            canFire.Value = false;
            // Update Game Manager
            gameManager.UpdatePlayerFired(true);
        }
    }

    /// <summary>
    /// ServerRpc controls character reaction to a hit
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void GetShotServerRpc()
    {
        if (IsServer)
        {
            Debug.Log("I've been shot!");
            // Play death audio
            PlayDeathClientRpc();

            // Update death boolean and animator
            dead.Value = true;
            animator.SetBool("Dead", true);
        }
    }

    /// <summary>
    /// ServerRpc controls the Scoring System
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void IncrementScoreServerRpc()
    {
        if (IsServer)
        {
            score.Value++;
        }
    }
    /// <summary>
    /// Gunshot Audio on the client side
    /// </summary>
    [ClientRpc]
    private void PlayGunshotClientRpc()
    {
        // Slightly vary pitch and volume
        audioSource.volume = Random.Range(0.9f, 1f);
        audioSource.pitch = Random.Range(0.9f, 1.1f);
        audioSource.PlayOneShot(gunshot);
    }
    /// <summary>
    /// Bullet Whiz by Audio on the client side
    /// </summary>
    [ClientRpc]
    private void PlayWhizClientRpc()
    {
        StartCoroutine(WhizSoundCoroutine());
    }
    /// <summary>
    /// Coroutine to delay the whiz sound effect for better perception of a miss
    /// </summary>
    private IEnumerator WhizSoundCoroutine()
    {
        // Wait 0.1 second so the gunshot doesn't hide the whiz
        yield return new WaitForSeconds(0.1f);
        // Slightly vary pitch and volume
        audioSource.volume = Random.Range(0.9f, 1f);
        audioSource.pitch = Random.Range(0.9f, 1.1f);
        int rand = (int)Random.Range(1f, 4f);
        // Choose a random whiz sound to play for more variation
        switch (rand)
        {
            case 1:
                audioSource.PlayOneShot(whiz1); break;
            case 2:
                audioSource.PlayOneShot(whiz2); break;
            default:
                audioSource.PlayOneShot(whiz3); break;
        }
    }
    /// <summary>
    /// Footstep Audio on the client side
    /// </summary>
    [ClientRpc]
    private void PlayFootstepClientRpc()
    {
        // Slightly vary pitch and volume
        audioSource.volume = Random.Range(0.9f, 1f);
        audioSource.pitch = Random.Range(0.9f, 1.1f);
        int rand = (int)Random.Range(1f, 4f);
        // Choose a random footstep sound to play for more variation
        switch (rand)
        {
            case 1:
                audioSource.PlayOneShot(footstep1); break;
            case 2:
                audioSource.PlayOneShot(footstep2); break;
            default:
                audioSource.PlayOneShot(footstep3); break;
        }
    }
    /// <summary>
    /// Death Sound Sequence Audio on the client side
    /// </summary>
    [ClientRpc]
    private void PlayDeathClientRpc()
    {
        StartCoroutine(DeathSoundCoroutine());
    }
    /// <summary>
    /// Death Sequence Sound is controlled by a coroutine to implement proper delays
    /// </summary>
    private IEnumerator DeathSoundCoroutine()
    {
        // Wait for 0.25s before the player grunts
        yield return new WaitForSeconds(0.25f);
        // Slightly vary pitch and volume
        audioSource.volume = Random.Range(0.9f, 1f);
        audioSource.pitch = Random.Range(0.9f, 1.1f);
        audioSource.PlayOneShot(grunt);
        // Wait for another 1.1s for the player to hit the floor
        yield return new WaitForSeconds(1.1f);
        // Slightly vary pitch and volume
        audioSource.volume = Random.Range(0.9f, 1f);
        audioSource.pitch = Random.Range(0.9f, 1.1f);
        audioSource.PlayOneShot(fall);
    }
    /// <summary>
    /// Client ID Getter
    /// </summary>
    public ulong GetPlayerNetworkObjectId()
    {
        return NetworkObjectId; // Directly accessing the NetworkObjectId property
    }
}