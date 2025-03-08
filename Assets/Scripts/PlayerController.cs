using Unity.Netcode;
using UnityEngine;
public enum Mode
{
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
    public NetworkVariable<bool> offense = new NetworkVariable<bool>();


    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        camera = transform.Find("Camera").gameObject;
    }

    void Update()
    {
        if (IsClient && IsOwner) // Check if this is the client and it owns this GameObject
        {
            HandleInput();
            HandleAnimationsServerRpc();
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

        // Handling mouse aiming
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        currentPitch -= mouseY; // Subtracting to invert the vertical input
        currentPitch = Mathf.Clamp(currentPitch, -90f, 90f); // Clamping pitch to prevent overrotation

        currentYaw += mouseX;

        // Send the computed pitch and yaw to the server to update rotation
        AimPlayerServerRpc(currentPitch, currentYaw);
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
        if (IsServer && !offense.Value) // Ensure this code is executed on the server
        {
            // Use transform.right for movement relative to the object's orientation
            Vector3 movement = transform.right * direction * speed;
            rb.velocity = movement;
        }
    }

    [ServerRpc]
    void AimPlayerServerRpc(float pitch, float yaw)
    {
        if (IsServer && offense.Value) // Ensure this code is executed on the server
        {
            Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0);
            camera.transform.rotation = targetRotation;
        }
    }


}