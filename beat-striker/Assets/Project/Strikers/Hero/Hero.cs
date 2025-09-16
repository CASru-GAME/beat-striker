using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Striker))]
public class Hero : MonoBehaviour {
    Animator anim;
    Rigidbody rb;
    Striker striker;
    [SerializeField] float jupmForce = 5f;

    void Start() {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        striker = GetComponent<Striker>();
    }

    void Update() {
        anim.SetBool("IsGround", striker.isGround);

        var actionDir = striker.player.GetBtnDown(Btn.Direction);

        if (actionDir) {
            Debug.Log(actionDir.direction);
            var dir = actionDir.direction.normalized;
            rb.AddForce(jupmForce * (Vector3)dir);
        }

        if (striker.player.GetBtnDown(Btn.East)) {
            Debug.Log("east");
            anim.SetTrigger("DoAttack");
        }

        striker.hp -= 5 * Time.deltaTime;
    }
}
