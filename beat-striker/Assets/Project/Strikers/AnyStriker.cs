using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Striker))]
public sealed class AnyStriker : MonoBehaviour {
    Animator anim;
    Rigidbody rb;
    Striker striker;
    [SerializeField] float jumpSpeed = 5f;
    [SerializeField] float runSpeed = 0.5f;
    [SerializeField] int airJumpMax = 3;
    bool isGround = false;
    public Colliden[] collinden;

    void Start() {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        striker = GetComponent<Striker>();
        striker.OnBeated += res => {
            // 何かしらのペナルティ
        };
    }
    

    void Update() {
        anim.SetBool("IsGround", isGround);
        anim.SetFloat("Velocity", rb.linearVelocity.magnitude);

        HandleMovement();
        HandleAttackInputs();
    }

    private void HandleAttackInputs()
    {
        if (striker.player.GetBtnDown(Btn.East))
        {
            // 通常攻撃 (Northボタン)
            if (striker.player.GetBtnDown(Btn.North))
            {
                var result = striker.Beat();
                anim.SetTrigger("DoAttack");
                HandleBeatResult(result);
            }

            // 特殊攻撃 (Southボタン)
            if (striker.player.GetBtnDown(Btn.South))
            {
                var result = striker.Beat();
                anim.SetTrigger("DoSpecial");
                HandleBeatResult(result);
            }
        }
    }

    private void HandleBeatResult(BeatResult result)
    {
        switch (result.status)
        {
            case BeatResult.Status.PERFECT:
                Debug.Log("PERFECT HIT!");
                break;
            case BeatResult.Status.GOOD:
                Debug.Log("GOOD HIT.");
                break;
            default: // MISS
                Debug.Log("MISS!");
                break;
        }
    }

    private void HandleMovement()
    {
        var direction = striker.player.GetBtn(Btn.Direction);
        Vector2 movementInput = direction ? direction.direction : Vector2.zero;

        if (movementInput.sqrMagnitude > 0.1f)
        {
            transform.forward = Mathf.Sign(movementInput.x) * Vector3.right;
            
            if (isGround)
            {
                rb.linearVelocity = new Vector3(
                    movementInput.x * jumpSpeed,
                    rb.linearVelocity.y,
                    movementInput.y * jumpSpeed
                );
            }
            else if (Mathf.Abs(rb.linearVelocity.x) < runSpeed)
            {
                Vector3 newVelocity = rb.linearVelocity;
                newVelocity.x = runSpeed * Mathf.Sign(movementInput.x);
                rb.linearVelocity = newVelocity;
            }
        }

        if (striker.player.GetBtnDown(Btn.North)) 
        {
            anim.SetTrigger("DoAttack");
        }
        if (striker.player.GetBtnDown(Btn.South)) 
        {
            anim.SetTrigger("DoSpecial");
        }
        if (striker.player.GetBtnDown(Btn.West)) 
        {
            anim.SetTrigger("DoCharge");
        }
        if (striker.player.GetBtnDown(Btn.RightShoulder)) 
        {
            anim.SetTrigger("MagicAttack");
        }
        if (striker.player.GetBtnDown(Btn.LeftShoulder)) 
        {
            anim.SetTrigger("DoAirAttack");
        }
        if (striker.player.GetBtnDown(Btn.Direction)) 
        {
            anim.SetTrigger("DoGuard");
        }
        if (striker.player.GetBtnDown(Btn.Space)) 
        {
            anim.SetTrigger("ActJump");
        }
        if (striker.player.GetBtnDown(Btn.RightTrigger)) 
        {
            anim.SetTrigger("ActDash");
        }
         if(striker.player.GetBtnDown(Btn.LeftTrigger) || striker.player.GetBtnDown(Btn.RightTrigger)) {
            anim.SetTrigger("ActWalk");
        }
    }

    private void OnCollisionStay(Collision collision) {
        foreach (var contact in collision.contacts) {
            if (contact.normal.y > 0.5f) {
                isGround = true;
                return;
            }
        }
    }

    private void OnCollisionExit(Collision collision) {
        isGround = false;
    }
}
