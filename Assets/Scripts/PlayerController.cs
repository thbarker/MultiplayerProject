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
    private GameObject camera;
    public NetworkVariable<Mode> mode = new NetworkVariable<Mode>();
    private GameManager gameManager;
    [SerializeField]
    private SkinnedMeshRenderer meshRenderer;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        camera = transform.Find("Camera").gameObject;
        gameManager = GameObject.FindWithTag("GameManager").GetComponent<GameManager>();
        if (gameManager)
            gameManager.AddPlayer();
        mode.Value = Mode.WAITING;
        if(IsLocalPlayer)
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
            ResetCameraRotationClientRpc();
        }
    }

    private void HandleInput()
    {
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

        
        if (IsLocalPlayer && mode.Value == Mode.OFFENSE) // Ensure this code is only run in Offense mode
        {
            // Handling mouse aiming
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

            currentPitch -= mouseY; // Subtracting to invert the vertical input
            currentPitch = Mathf.Clamp(currentPitch, -90f, 90f); // Clamping pitch to prevent overrotation

            currentYaw += mouseX;
            Quaternion targetRotation = Quaternion.Euler(currentPitch, currentYaw, 0);
            camera.transform.localRotation = targetRotation;
        }
    }
    [ClientRpc]
    private void ResetCameraRotationClientRpc()
    {
        // Resets the camera's rotation relative to the parent object (the player)
        camera.transform.localRotation = Quaternion.identity;
    }

    [ServerRpc]
    private void HandleAnimationsServerRpc()
    {
        // Convert global velocity to local velocity
        Vector3 localVelocity = transform.InverseTransformDirection(rb.velocity);

        // Set the Speed parameter based on the magnitude of local x velocity
        animator.SetFloat("Speed", localVelocity.x);
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

    [ServerRpc]
    void FireServerRpc()
    {
        if (IsServer && mode.Value == Mode.OFFENSE) // Ensure this code is executed on the server
        {
            
        }
    }


}