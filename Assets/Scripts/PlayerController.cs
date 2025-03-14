using System.Collections;
using System.Threading;
using Unity.Netcode;
using UnityEngine;
public enum Mode
{
    WAITING,
    OFFENSE,
    DEFENSE
}

public class PlayerController : NetworkBehaviour
{
    public float mouseSensitivity = 100.0f;
    private float currentPitch = 0.0f;
    private float currentYaw = 0.0f;
    public float speed = 5.0f;
    private Rigidbody rb;
    private Animator animator;
    private GameObject playerCamera;
    public NetworkVariable<Mode> mode = new NetworkVariable<Mode>();
    public NetworkVariable<bool> canFire = new NetworkVariable<bool>();
    public NetworkVariable<bool> dead = new NetworkVariable<bool>();
    public NetworkVariable<int> score = new NetworkVariable<int>();
    private GameManager gameManager;
    [SerializeField]
    private SkinnedMeshRenderer meshRenderer;
    public GameObject canvas;
    public GameObject crosshair;
    public GameObject deathImage;
    public AudioSource audioSource;
    public AudioClip gunshot, fall, grunt, whiz1, whiz2, whiz3, footstep1, footstep2, footstep3;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        playerCamera = transform.Find("Camera").gameObject;
        gameManager = GameObject.FindWithTag("GameManager").GetComponent<GameManager>();
        mode.Value = Mode.WAITING;
    }
    public override void OnNetworkSpawn()
    {
        if (IsLocalPlayer)
        {
            meshRenderer.enabled = false;
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
            HandleInput();
            UpdateUI();
            UpdateAnimationServerRpc(currentYaw, currentPitch);
        }
    }

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

    private void SetOffense()
    {
        ResetPositionServerRpc();
        ResetCameraRotation();
    }
    private void SetDefense()
    {
        ResetPositionServerRpc();
        ResetCameraRotation();
    }
    public void SetCanFire(bool flag)
    {
        if (IsServer)
        {
           canFire.Value = flag;
        }
    }

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

    [ServerRpc]
    void UpdateAnimationServerRpc(float yaw, float pitch)
    {
        UpdateAnimation(yaw, pitch); // Update server-side
    }

    void UpdateAnimation(float yaw, float pitch)
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.velocity);

        animator.SetFloat("Speed", localVelocity.x);

        if (mode.Value == Mode.OFFENSE)
        {
            animator.SetFloat("Speed", 0);
            animator.SetBool("Aiming", true);
            animator.SetFloat("AimX", yaw / 90);
            animator.SetFloat("AimY", -pitch / 90);
        }
        else
        {
            animator.SetBool("Aiming", false);
            animator.SetFloat("AimX", 0);
            animator.SetFloat("AimY", 0);
        }
    }

    private void ResetCameraRotation()
    {
        currentPitch = 0;
        currentYaw = 0;
        playerCamera.transform.localRotation = Quaternion.identity;
    }

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


    [ServerRpc(RequireOwnership = false)]
    public void IncrementScoreServerRpc()
    {
        if (IsServer)
        {
            score.Value++;
        }
    }

    [ClientRpc]
    private void PlayGunshotClientRpc()
    {
        // Slightly vary pitch and volume
        audioSource.volume = Random.Range(0.9f, 1f);
        audioSource.pitch = Random.Range(0.9f, 1.1f);
        audioSource.PlayOneShot(gunshot);
    }

    [ClientRpc]
    private void PlayWhizClientRpc()
    {
        StartCoroutine(WhizSoundCoroutine());
    }
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

    [ClientRpc]
    private void PlayDeathClientRpc()
    {
        StartCoroutine(DeathSoundCoroutine());
    }

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
    public ulong GetPlayerNetworkObjectId()
    {
        return NetworkObjectId; // Directly accessing the NetworkObjectId property
    }
}