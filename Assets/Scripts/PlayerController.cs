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
    private GameManager gameManager;
    [SerializeField]
    private SkinnedMeshRenderer meshRenderer;
    public GameObject canvas;
    public GameObject crosshair;
    public GameObject deathImage;

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
    void SetHasFiredTrueServerRpc()
    {
        if(IsServer)
            gameManager.UpdatePlayerFired(true);
    }

    [ServerRpc]
    void FireServerRpc(Vector3 aimDirection)
    {
        Debug.Log("Can Fire is " + canFire.Value);
        Debug.Log("Aim Direction is " + aimDirection);
        if (!IsServer)
            return;
        if (mode.Value == Mode.OFFENSE && canFire.Value)
        {
            RaycastHit hit;
            Ray ray = new Ray(playerCamera.transform.position, aimDirection.normalized);  // Use normalized direction
            float maxDistance = 100.0f;
            if (Physics.Raycast(ray, out hit, maxDistance))
            {
                Debug.Log("Performing Raycast!");
                var hitPlayer = hit.collider.GetComponent<HitBox>(); 
                Debug.Log("Hit " + hit.collider.name);
                if (hitPlayer != null && hitPlayer != this)  // Ensure not hitting self
                {
                    hitPlayer.GetShotHitBox();
                }
            }
            else
            {
                Debug.Log("No hit");
            }
            canFire.Value = false;
            gameManager.UpdatePlayerFired(true);
        }
    }


    [ServerRpc(RequireOwnership = false)]
    public void GetShotServerRpc()
    {
        if (IsServer)
        {
            Debug.Log("I've been shot!");
            dead.Value = true;
            animator.SetBool("Dead", true);
        }
    }


}