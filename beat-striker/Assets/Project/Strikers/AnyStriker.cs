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

        var east = striker.player.GetBtnDown(Btn.East);

        if (east) {
            var d = east.direction;
            transform.forward = Mathf.Sign(d.x) * Vector3.right;
            rb.linearVelocity = jumpSpeed * d;
        }

        else if (Mathf.Abs(rb.linearVelocity.x) < runSpeed && east.direction.sqrMagnitude > 0e-3) {
            transform.forward = Mathf.Sign(east.direction.x) * Vector3.right;
            rb.linearVelocity = rb.linearVelocity.X(runSpeed * Mathf.Sign(east.direction.x));
        }

        if (striker.player.GetBtnDown(Btn.South)) {
            anim.SetTrigger("DoAttack");
        }
        if (striker.player.GetBtnDown(Btn.North)) {
            anim.SetTrigger("DoSpecial");
        }
        if(striker.player.GetBtnDown(Btn.West)) {
            anim.SetTrigger("DoCharge");
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
