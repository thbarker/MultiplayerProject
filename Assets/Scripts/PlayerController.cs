using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [SerializeField]
    [Range(0f, 10f)]
    private float speed;
    public float currentForce = 0f;
    public float accelerationTime = 0.5f; // Time in seconds to reach full force
    float targetForce = 50f;
    private float timeElapsed = 0f;
    private Rigidbody rb;
    private Animator animator;
    private int lastDirection = 0; // 0: none, -1: left, 1: right
    private bool aiming = false; 
    public float sensitivity = 0.1f; // Sensitivity of the aiming
    private float AimX = 0f;
    private int xDirection = 0;


    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        UpdateInput();
        Aiming();
        if(!aiming)
            Movement();
        UpdateAnimations();
    }

    private void UpdateInput()
    {
        xDirection = 0;
        if (Input.GetKey(KeyCode.A)) xDirection = -1;
        if (Input.GetKey(KeyCode.D)) xDirection = 1;
    }
    private void Movement()
    {if (xDirection != 0)
        {
            if (lastDirection != xDirection)
            {
                // Reset the force when changing directions
                timeElapsed = 0;
                currentForce = 0;
            }

            // Update the force and apply it
            timeElapsed += Time.deltaTime;
            currentForce = Mathf.Lerp(0, targetForce, timeElapsed / accelerationTime);
            rb.AddForce(new Vector2(currentForce * xDirection, 0));

            lastDirection = xDirection; // Update last direction
        }
        else
        {
            // Reset the force when keys are not pressed
            timeElapsed = 0;
            currentForce = 0;
        }

        // Optional: Normalize the speed
        if (Mathf.Abs(rb.velocity.x) > speed)
        {
            rb.velocity = new Vector2(Mathf.Sign(rb.velocity.x) * speed, rb.velocity.y);
        }
    }

    private void Aiming()
    {
        if (Input.GetKey(KeyCode.Mouse1))
        {
            aiming = true;
            if (AimX + xDirection * sensitivity * Time.deltaTime > 0.5) 
                AimX = 0.5f;
            else if (AimX + xDirection * sensitivity * Time.deltaTime < -0.5) 
                AimX = -0.5f;
            else
                // Apply sensitivity and Time.deltaTime to smooth the aiming adjustments
                AimX += xDirection * sensitivity * Time.deltaTime;

            // Calculate new rotation angles
            float newRotationY = AimX * 90;
            // Clamping the X rotation to prevent flipping the camera over
            newRotationY = Mathf.Clamp(newRotationY, -45f, 45f);

            // Set the new rotation using Euler angles directly on the camera
            Camera.main.transform.eulerAngles = new Vector3(0, newRotationY, 0);
        }
        else
        {
            aiming = false;
            // Set the new rotation using Euler angles directly on the camera
            Camera.main.transform.eulerAngles = Vector3.zero;
        }
    }
    private void UpdateAnimations()
    {
        float animatorSpeed;
        animatorSpeed = (rb.velocity.x + speed) / (2 * speed);
        animator.SetFloat("Speed", Mathf.Abs(animatorSpeed));
        animator.SetBool("Aiming", aiming);
        animator.SetFloat("AimX", AimX);
        Debug.Log(AimX);
    }
}
