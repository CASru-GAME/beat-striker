using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Striker))]
public class Hero : MonoBehaviour {
    Animator anim;
    Rigidbody rb;
    Striker striker;
    [SerializeField] float jumpSpeed = 5f;
    int airJumpCount = 0;
    public Colliden swardColliden;

    void Start() {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        striker = GetComponent<Striker>();
        striker.OnLanded += () => { airJumpCount = 0; };
    }

    void Update() {
        anim.SetBool("IsGround", striker.isGround);

        var btnDownDir = striker.player.GetBtnDown(Btn.Direction);

        if (btnDownDir && (striker.isGround || airJumpCount < 1)) {
            rb.linearVelocity = jumpSpeed * (Vector3)btnDownDir.direction.normalized;
            if (!striker.isGround) airJumpCount++;
        }


        if (striker.player.GetBtnDown(Btn.East)) {
            Debug.Log("east");
            anim.SetTrigger("DoAttack");
        }
    }
}
