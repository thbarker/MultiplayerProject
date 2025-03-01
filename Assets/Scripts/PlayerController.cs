using System.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Animator))]
public class PlayerController : NetworkBehaviour
{
    [SerializeField][Range(0f, 10f)] private float speed;
    public float accelerationTime = 0.5f;
    private float targetForce = 50f;
    private float timeElapsed = 0f;
    private Rigidbody rb;
    private Animator animator;

    private int lastDirection = 0;
    private float AimX = 0f;
    private int xDirection = 0;

    private float smoothTime = 0.1f;
    private float velocityRef;
    private float forceVelocityRef;

    // Networked Variables
    private NetworkVariable<Vector2> netVelocity = new NetworkVariable<Vector2>(Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> netAnimatorSpeed = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> netAiming = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> netAimX = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private float localAnimatorSpeed = 0f;

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        // Subscribe to animation updates for all players
        netAnimatorSpeed.OnValueChanged += (prev, next) => UpdateAnimations();
        netAiming.OnValueChanged += (prev, next) => UpdateAnimations();
        netAimX.OnValueChanged += (prev, next) => UpdateAnimations();
    }

    void FixedUpdate()
    {
        if (IsOwner)
        {
            UpdateInput();
            UpdateAnimatorSpeed();
            Aiming();
            if (!netAiming.Value)
                SmoothMovement();

            // Owner updates animations
            UpdateAnimations();
        }
        else
        {
            // Apply velocity updates for clients
            rb.velocity = netVelocity.Value;
        }
    }

    private void UpdateInput()
    {
        xDirection = 0;
        if (Input.GetKey(KeyCode.A)) xDirection = -1;
        if (Input.GetKey(KeyCode.D)) xDirection = 1;
    }

    private void SmoothMovement()
    {
        if (xDirection != 0)
        {
            if (lastDirection != xDirection)
            {
                timeElapsed = 0;
            }

            timeElapsed += Time.deltaTime;

            float smoothedForce = Mathf.SmoothDamp(rb.velocity.x, xDirection * targetForce, ref forceVelocityRef, accelerationTime);

            // Send smoothed force to the server
            MoveServerRpc(smoothedForce);

            lastDirection = xDirection;
        }
    }

    [ServerRpc]
    private void MoveServerRpc(float force, ServerRpcParams rpcParams = default)
    {
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out var client)) return;
        Rigidbody playerRb = client.PlayerObject.GetComponent<Rigidbody>();

        Vector2 newVelocity = new Vector2(force, playerRb.velocity.y);

        if (Mathf.Abs(newVelocity.x) > speed)
        {
            newVelocity.x = Mathf.Sign(newVelocity.x) * speed;
        }

        playerRb.velocity = newVelocity;
        netVelocity.Value = newVelocity;
    }

    private void Aiming()
    {
        if (Input.GetKey(KeyCode.Mouse1))
        {
            AimX += xDirection * 0.1f * Time.deltaTime;
            AimX = Mathf.Clamp(AimX, -0.5f, 0.5f);

            // Sync aiming across the network
            UpdateAimingServerRpc(true, AimX);
        }
        else
        {
            // Sync aiming reset
            UpdateAimingServerRpc(false, 0f);
        }

        Camera.main.transform.eulerAngles = new Vector3(0, AimX * 90, 0);
    }

    [ServerRpc]
    private void UpdateAimingServerRpc(bool isAiming, float aimX)
    {
        netAiming.Value = isAiming;
        netAimX.Value = aimX;
    }

    private void UpdateAnimatorSpeed()
    {
        float velocityX = rb.velocity.x;
        float targetSpeed = Mathf.Abs(velocityX) < 0.1f ? 0f : velocityX / speed;

        if (IsOwner)
        {
            netAnimatorSpeed.Value = targetSpeed;
        }
    }

    private void UpdateAnimations()
    {
        // Smooth animation transitions
        localAnimatorSpeed = Mathf.SmoothDamp(localAnimatorSpeed, netAnimatorSpeed.Value, ref velocityRef, smoothTime);

        // Apply networked values to animations on all clients
        animator.SetFloat("Speed", localAnimatorSpeed);
        animator.SetBool("Aiming", netAiming.Value);
        animator.SetFloat("AimX", netAimX.Value);
    }
}
