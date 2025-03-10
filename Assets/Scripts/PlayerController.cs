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
    private GameManager gameManager;
    [SerializeField]
    private SkinnedMeshRenderer meshRenderer;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        playerCamera = transform.Find("Camera").gameObject;
        gameManager = GameObject.FindWithTag("GameManager").GetComponent<GameManager>();
        if (gameManager)
            gameManager.AddPlayer();
        mode.Value = Mode.WAITING;
        if (IsLocalPlayer)
        {
            meshRenderer.enabled = false;
        }
    }
    void Update()
    {
        if (IsClient && IsOwner) // Check if this is the client and it owns this GameObject
        {
            HandleInput();
            HandleAnimationsServerRpc();
        }

    }
    public void SetPlayerMode(Mode newMode)
    {
        if (IsServer)
        {
            mode.Value = newMode;
            if(mode.Value == Mode.OFFENSE)
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
        ResetCameraRotationClientRpc();
    }
    private void SetDefense()
    {
        ResetPositionServerRpc();
        ResetCameraRotationClientRpc();
    }
    public void SetCanFire(bool flag)
    {
        if (IsServer)
        {
           canFire.Value = flag;
        }
    }

    private void HandleInput()
    {
        // Server sided movement
        if (Input.GetKey(KeyCode.A))
        {
            MovePlayerServerRpc(-1);
        }
        else if (Input.GetKey(KeyCode.D))
        {
            MovePlayerServerRpc(1);
        }
        else
        {
            MovePlayerServerRpc(0);
        }

        // Client Sided Aiming
        AimPlayer();

        // Server Sided Shooting
        if (Input.GetMouseButtonDown(0))
        {
            FireServerRpc();
        }
    }

    [ClientRpc]
    private void ResetCameraRotationClientRpc()
    {
        // Resets the camera's rotation relative to the parent object (the player)
        playerCamera.transform.localRotation = Quaternion.identity;
    }
    [ServerRpc]
    private void ResetPositionServerRpc()
    {
        if(IsServer)
            // Resets the players movement to the spawnpoint
            transform.position = new Vector3(0, transform.position.y, transform.position.z);
    }

    [ServerRpc]
    private void HandleAnimationsServerRpc()
    {
        // Convert global velocity to local velocity
        Vector3 localVelocity = transform.InverseTransformDirection(rb.velocity);

        // Set the Speed parameter based on the magnitude of local x velocity
        animator.SetFloat("Speed", localVelocity.x);

        if (mode.Value == Mode.OFFENSE)
        {
            animator.SetBool("Aiming", true);
            animator.SetFloat("AimX", currentYaw / 45);
            animator.SetFloat("AimY", -currentPitch / 45);
        }
        else
        {
            animator.SetBool("Aiming", false);
            animator.SetFloat("AimX", 0);
            animator.SetFloat("AimY", 0);
        }
    }

    [ServerRpc]
    private void MovePlayerServerRpc(int direction)
    {
        if (IsServer && mode.Value == Mode.DEFENSE) // Ensure this code is executed on the server
        {
            // Use transform.right for movement relative to the object's orientation
            Vector3 movement = transform.right * direction * speed;
            rb.velocity = movement;
        }
    }

    private void AimPlayer()
    {
        if (IsLocalPlayer && mode.Value == Mode.OFFENSE) // Ensure this code is only run in Offense mode
        {
            // Handling mouse aiming
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

            currentPitch -= mouseY; // Subtracting to invert the vertical input
            currentYaw += mouseX;

            // Clamping the pitch and yaw
            currentPitch = Mathf.Clamp(currentPitch, -45f, 45f);
            currentYaw = Mathf.Clamp(currentYaw, -45f, 45f);

            Quaternion targetRotation = Quaternion.Euler(currentPitch, currentYaw, 0);
            playerCamera.transform.localRotation = targetRotation;
        }
    }

    [ServerRpc]
    void SetHasFiredTrueServerRpc()
    {
        if(IsServer)
            gameManager.UpdatePlayerFired(true);
    }

    [ServerRpc(RequireOwnership = true)]
    void FireServerRpc()
    {
        // Check if the player is in OFFENSE mode and allowed to fire
        if (mode.Value == Mode.OFFENSE && canFire.Value)
        {
            // Perform the raycast
            PerformRaycast();
            // After firing, set canFire to false to prevent repeated shots without authorization
            canFire.Value = false;
            // Inform the GameManager that this player has fired
            gameManager.UpdatePlayerFired(true);
        }
    }

    void PerformRaycast()
    {
        if (playerCamera != null)
        {
            RaycastHit hit;
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            float maxDistance = 100.0f;  // Define the maximum distance for the raycast

            if (Physics.Raycast(ray, out hit, maxDistance))
            {
                // Example: Apply damage if the hit object has a damageable component
                var damageable = hit.collider.GetComponent<PlayerController>();
                if (damageable != null)
                {
                    Debug.Log("Hit " + hit.collider.name);
                }
            }
            else
            {
                Debug.Log("No hit");
            }
        }
        else
        {
            Debug.LogError("Camera not found on the player object");
        }
    }



}