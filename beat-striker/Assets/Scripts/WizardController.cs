using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class WizardController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 5f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.1f;
    public LayerMask groundLayer;

    private Rigidbody rb;
    private bool isGrounded;

    // Input System
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction runAction;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("WizardController requires a Rigidbody component (3D).", this);
            enabled = false;
            return;
        }

        // Common misconfiguration checks
        if (rb.isKinematic)
        {
            Debug.LogWarning("Rigidbody is set to Kinematic - physics (gravity/jumps) will not work. Set to Dynamic.", this);
        }
        if (!rb.useGravity)
        {
            Debug.LogWarning("Rigidbody.useGravity is false - the object won't fall. Enable Use Gravity.", this);
        }

        // PlayerInput / InputAction setup (new Input System)
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            Debug.LogWarning("PlayerInput component not found on this GameObject. Add PlayerInput and assign PlayerControls actions.", this);
        }
        else if (playerInput.actions != null)
        {
            // try to bind commonly expected actions; if action map differs, these will be null
            moveAction = playerInput.actions.FindAction("Move", true);
            jumpAction = playerInput.actions.FindAction("Jump", true);
            runAction = playerInput.actions.FindAction("Run", true);
        }
    }

    void Update()
    {
        Move();
        Jump();
    }

    private void Move()
    {
        float moveInput = 0f;

        // Read from new Input System if available, otherwise fall back to legacy (should not be used if project is in Input System only mode)
        if (moveAction != null)
        {
            Vector2 mv = moveAction.ReadValue<Vector2>();
            moveInput = mv.x;
        }
        else
        {
            // fallback (only valid if Unity Player Settings allow both systems)
            moveInput = Input.GetAxis("Horizontal");
        }

        // Preserve Y and Z velocity; only modify X (left/right)
        Vector3 vel = rb.linearVelocity;
        vel.x = moveInput * moveSpeed;
        rb.linearVelocity = vel;
    }

    private void Jump()
    {
        if (groundCheck == null)
        {
            Debug.LogWarning("groundCheck Transform is not assigned.", this);
            return;
        }

        isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);

        bool jumpTriggered = false;
        if (jumpAction != null)
        {
            jumpTriggered = jumpAction.triggered;
        }
        else
        {
            // fallback
            jumpTriggered = Input.GetButtonDown("Jump");
        }

        if (isGrounded && jumpTriggered)
        {
            // Use impulse so gravity immediately affects the body afterwards
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    // Gizmo to visualize ground check in editor
    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}