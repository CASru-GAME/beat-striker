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
    [SerializeField] int airJumpMax = 3;
    int airJumpCount = 0;

    public Colliden swardColliden;

    void Start() {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        striker = GetComponent<Striker>();
        striker.OnLanded += () => { airJumpCount = 0; };
        striker.OnBeated += res => {
            // 何かしらのペナルティ
        };
    }

    void Update() {
        anim.SetBool("IsGround", striker.isGround);

        var btnDownDir = striker.player.GetBtnDown(Btn.Direction);

        if (btnDownDir && (striker.isGround || airJumpCount < airJumpMax) && striker.Beat()) {
            var dir = btnDownDir.direction.normalized;
            transform.forward = Mathf.Sign(dir.x) * Vector3.right;
            rb.linearVelocity = jumpSpeed * new Vector2(0.7f, 1f) * dir;
            if (!striker.isGround) airJumpCount++;
        }


        if (striker.player.GetBtnDown(Btn.East)) {
            var res = striker.Beat();
            if (res)
                anim.SetTrigger("DoAttack");
        }
    }
}
