using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    public float speed = 5.0f;
    private Rigidbody rb;
    private Animator animator;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
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
        } else
        {
            MovePlayerServerRpc(0);
        }
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
    void MovePlayerServerRpc(int direction)
    {
        if (IsServer) // Ensure this code is executed on the server
        {
            // Use transform.right for movement relative to the object's orientation
            Vector3 movement = transform.right * direction * speed;
            rb.velocity = movement;
        }
    }

}
