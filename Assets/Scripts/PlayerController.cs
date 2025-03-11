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
    }
    public override void OnNetworkSpawn()
    {
        if(IsLocalPlayer)
            meshRenderer.enabled = false;
    }

    void Update()
    {
        if (IsClient && IsOwner) // Check if this is the client and it owns this GameObject
        {
            HandleInput();
            UpdateAnimationStateServerRpc(rb.velocity, mode.Value, currentYaw, currentPitch);
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

        if (IsLocalPlayer && Input.GetMouseButtonDown(0))
        {
            Vector3 aimDirection = playerCamera.transform.forward; // Assuming playerCamera is properly referenced
            FireServerRpc(aimDirection); // Call the ServerRpc method with the current camera forward vector
        }
    }

    [ClientRpc]
    private void ResetCameraRotationClientRpc()
    {
        // Resets the camera's rotation relative to the parent object (the player)
        playerCamera.transform.localRotation = Quaternion.identity;
    }
    [ServerRpc(RequireOwnership = false)]
    private void ResetPositionServerRpc()
    {
        if (IsServer)
        {
            transform.position = new Vector3(0, transform.position.y, transform.position.z);
            rb.velocity = Vector3.zero;
        }
    }


    [ClientRpc]
    private void HandleAnimationsClientRpc(Vector3 velocity, Mode mode, float yaw, float pitch)
    {
        // Handle this locally on each client
        Vector3 localVelocity = transform.InverseTransformDirection(velocity);

        animator.SetFloat("Speed", localVelocity.x);  // Use Mathf.Abs if speed should always be positive

        if (mode == Mode.OFFENSE)
        {
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

    // This ServerRpc is called by the client who owns the object, the server processes it, and then calls the ClientRpc
    [ServerRpc(RequireOwnership = true)]
    private void UpdateAnimationStateServerRpc(Vector3 velocity, Mode mode, float yaw, float pitch)
    {
        HandleAnimationsClientRpc(velocity, mode, yaw, pitch);
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
    void FireServerRpc(Vector3 aimDirection)
    {
        if (mode.Value == Mode.OFFENSE && canFire.Value)
        {
            PerformRaycast(aimDirection);  // Pass the aim direction calculated on the client
            canFire.Value = false;
            gameManager.UpdatePlayerFired(true);
        }
    }

    void PerformRaycast(Vector3 aimDirection)
    {
        if (playerCamera != null)
        {
            RaycastHit hit;
            Ray ray = new Ray(playerCamera.transform.position, aimDirection.normalized);  // Use normalized direction
            float maxDistance = 100.0f;

            if (Physics.Raycast(ray, out hit, maxDistance))
            {
                var damageable = hit.collider.GetComponent<PlayerController>();
                if (damageable != null && damageable != this)  // Ensure not hitting self
                {
                    // Server handles damage application
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