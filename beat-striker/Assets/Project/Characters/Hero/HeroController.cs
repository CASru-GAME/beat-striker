using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
public class HeroController : CharacterController
{
    Animator anim;
    Rigidbody rb;
    [SerializeField] float speed = 5f;
    [SerializeField] float jupmForce = 5f;
    // isGround is specific to Hero and determined via collision callbacks
    public bool isGround { get; private set; }

    void Awake()
    {
        hp = 200;
    }

    void Start()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        var inputX = Input.GetAxis("Horizontal");
        anim.SetBool("IsGround", isGround);

        if (Mathf.Abs(inputX) > 0.1f)
        {
            anim.SetBool("IsRun", true);
            transform.position += speed * Time.deltaTime * Mathf.Sign(inputX) * Vector3.right;
            transform.forward = Mathf.Sign(inputX) * Vector3.right;
        }
        else
        {
            anim.SetBool("IsRun", false);
        }

        if (isGround && Input.GetKeyDown(KeyCode.W))
        {
            rb.AddForce(jupmForce * Vector3.up);
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            anim.SetTrigger("DoAttack");
        }

        this.hp -= 5 * Time.deltaTime;
    }

    private void OnCollisionStay(Collision collision)
    {
        isGround = false;
        foreach (var contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                isGround = true;
                return;
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        isGround = false;
    }
}
