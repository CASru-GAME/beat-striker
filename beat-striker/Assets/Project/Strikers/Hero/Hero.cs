using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Striker))]
public class Hero : MonoBehaviour {
    Animator anim;
    Rigidbody rb;
    Striker striker;
    [SerializeField] float jumpSpeed = 5f;
    [SerializeField] float runSpeed = 0.5f;
    [SerializeField] int airJumpMax = 3;
    int airJumpCount = 0;

    public Colliden swardColliden;

    void Start() {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        striker = GetComponent<Striker>();
        striker.OnBeated += res => {
            // 何かしらのペナルティ
        };
    }

    void Update() {
        anim.SetBool("IsGround", false);

        var east = striker.player.GetBtnDown(Btn.East);

        if (east && (airJumpCount < airJumpMax)) {
            var res = striker.Beat();
            if (res) {
                var d = east.direction;
                transform.forward = Mathf.Sign(d.x) * Vector3.right;
                rb.linearVelocity = jumpSpeed * d;
                airJumpCount++;
            }
        }
        else if (Mathf.Abs(rb.linearVelocity.x) < runSpeed && east.direction.sqrMagnitude > 0e-3) {
            transform.forward = Mathf.Sign(east.direction.x) * Vector3.right;
            rb.linearVelocity = rb.linearVelocity.X(runSpeed * Mathf.Sign(east.direction.x));
        }


        if (striker.player.GetBtnDown(Btn.South)) {
            var res = striker.Beat();
            if (res)
                anim.SetTrigger("DoAttack");
        }
    }
}
