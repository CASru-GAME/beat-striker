using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
[RequireComponent(typeof(BoxCollider))]
public class beam11 : MonoBehaviour
{
    [SerializeField] AudioClip audioClip;
    [SerializeField, Min(0f)] float audioPlayDelaySeconds = 0f;
    [SerializeField, Min(0f)] float damagePerSecond = 10f;
    [SerializeField, Min(0f)] float knockbackPerSecond = 10f;
    [SerializeField, Min(0f)] float maxLength = 12f;
    [SerializeField, Min(0f)] float extendSpeed = 40f;
    [SerializeField, Min(0f)] float extendTime = 0.3f;
    [SerializeField] Vector3 localExtendDirection = Vector3.forward;
    [SerializeField] ParticleSystem beam11ParticleSystem;

    BoxCollider hitCollider;
    Vector3 initialColliderSize;
    Vector3 initialColliderCenter;
    StrikerHub ownerStrikerHub;
    float elapsed;
    int fixedTick;
    readonly Dictionary<Hurtbox, int> lastDamagedTickByHurtbox = new();

    void Awake()
    {
        beam11ParticleSystem = GetComponent<ParticleSystem>();
        hitCollider = GetComponent<BoxCollider>();
        hitCollider.isTrigger = true;

        initialColliderSize = hitCollider.size;
        initialColliderCenter = hitCollider.center;
    }

    public void SetOwnerStrikerHub(StrikerHub strikerHub)
    {
        ownerStrikerHub = strikerHub;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        beam11ParticleSystem.Play(true);

        if (audioPlayDelaySeconds <= 0f)
        {
            AudioSource.PlayClipAtPoint(audioClip, transform.position);
            return;
        }

        StartCoroutine(PlayAudioAfterDelay());
    }

    // Update is called once per frame
    void Update()
    {
        var localExtendDirectionNormalized = GetLocalExtendDirection();

        elapsed += Time.deltaTime;
        var lengthBySpeed = initialColliderSize.z + extendSpeed * elapsed;
        var timeLimitedLength = extendTime <= 0f ? maxLength : initialColliderSize.z + extendSpeed * extendTime;
        var length = Mathf.Min(maxLength, Mathf.Min(lengthBySpeed, timeLimitedLength));

        var size = initialColliderSize;
        size.z = length;
        hitCollider.size = size;

        hitCollider.center = initialColliderCenter + localExtendDirectionNormalized * ((length - initialColliderSize.z) * 0.5f);
    }

    void FixedUpdate()
    {
        fixedTick++;
    }

    void OnTriggerStay(Collider other)
    {
        if (other.transform.IsChildOf(transform))
        {
            return;
        }

        if (!other.TryGetComponent<Hurtbox>(out var hurtbox))
        {
            hurtbox = other.GetComponentInParent<Hurtbox>();
            if (hurtbox == null)
            {
                return;
            }
        }

        if (ownerStrikerHub == null)
        {
            return;
        }

        var otherStrikerHub = other.GetComponentInParent<StrikerHub>();
        if (otherStrikerHub == null)
        {
            otherStrikerHub = hurtbox.GetComponentInParent<StrikerHub>();
            if (otherStrikerHub == null)
            {
                return;
            }
        }

        if (otherStrikerHub == ownerStrikerHub)
        {
            return;
        }

        if (lastDamagedTickByHurtbox.TryGetValue(hurtbox, out var lastTick) && lastTick == fixedTick)
        {
            return;
        }

        lastDamagedTickByHurtbox[hurtbox] = fixedTick;
        var tickDamage = damagePerSecond * Time.fixedDeltaTime;
        var tickKnockback = GetWorldExtendDirection() * (knockbackPerSecond * Time.fixedDeltaTime);
        hurtbox.GiveHit(new HitStatus(tickDamage, tickKnockback));
    }

    System.Collections.IEnumerator PlayAudioAfterDelay()
    {
        yield return new WaitForSeconds(audioPlayDelaySeconds);
        AudioSource.PlayClipAtPoint(audioClip, transform.position);
    }

    Vector3 GetLocalExtendDirection()
    {
        if (localExtendDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector3.forward;
        }

        return localExtendDirection.normalized;
    }

    Vector3 GetWorldExtendDirection()
    {
        return transform.TransformDirection(GetLocalExtendDirection());
    }
}
