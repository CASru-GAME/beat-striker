using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField]
    private ParticleSystem enemyEffect;
    private Animator anim;
    private SphereCollider attackCollider;
    private float attackTimer = 0.0f;

    void Start()
    {
        anim = GetComponent<Animator>();
        attackCollider = GetComponent<SphereCollider>();
    }

    void Update()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 moveDirection = new Vector3(x, 0, z);

        if (moveDirection.magnitude > 0.01f)
        {
            anim.SetBool("IsRun", true);

            transform.position += moveDirection.normalized * Time.deltaTime * 5.0f;

            transform.forward = moveDirection.normalized;
        }
        else
        {
            anim.SetBool("IsRun", false);
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            anim.SetTrigger("DoAttack");
            attackCollider.enabled = true;
        }

        if (attackCollider.enabled)
        {
            attackTimer += Time.deltaTime;
            if (attackTimer > 0.5f)
            {
                attackCollider.enabled = false;
                attackTimer = 0.0f;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            Destroy(other.gameObject);
            Instantiate(enemyEffect, other.transform.position, Quaternion.identity);
        }
    }
}
