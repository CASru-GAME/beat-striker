using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class beam11 : MonoBehaviour
{
    [SerializeField] AudioClip audioClip;
    [SerializeField, Min(0f)] float audioPlayDelaySeconds = 0f;
    [SerializeField, Min(0f)] float damagePerSecond = 10f;
    [SerializeField, Min(0f)] float knockbackPerSecond = 10f;
    [SerializeField, Min(0f)] float maxLength = 12f;
    [SerializeField, Min(0f)] float extendSpeed = 40f;
    [SerializeField, Min(0f)] float extendStartDelaySeconds = 0f;
    [SerializeField, Min(0f)] float extendTime = 0.3f;
    [SerializeField] Vector3 localExtendDirection = Vector3.forward;
    [SerializeField] ParticleSystem particleRoot;

    [SerializeField] AudioClip chargeAudioClip;
    [SerializeField, Min(0f)] float chargePlayDelaySeconds = 0f;

    BoxCollider hitCollider;
    Vector3 initialColliderSize;
    Vector3 initialColliderCenter;
    StrikerHub ownerStrikerHub;
    float elapsed;
    int fixedTick;
    readonly Dictionary<Hurtbox, int> lastDamagedTickByHurtbox = new();

    void Awake()
    {
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
        Destroy(gameObject, extendStartDelaySeconds + extendTime);
        particleRoot.Play(true);

        if (audioPlayDelaySeconds <= 0f)
        {
            AudioSource.PlayClipAtPoint(audioClip, transform.position);
            AudioSource.PlayClipAtPoint(chargeAudioClip, transform.position);
            return;
        }

        StartCoroutine(PlayAudioAfterDelay());;
        StartCoroutine(PlayChargeAudioAfterDelay());
    }


    System.Collections.IEnumerator PlayChargeAudioAfterDelay()
    {
        yield return new WaitForSeconds(chargePlayDelaySeconds);
        AudioSource.PlayClipAtPoint(chargeAudioClip, transform.position);
    }

    // Update is called once per frame
    void Update()
    {
        var localExtendDirectionNormalized = GetLocalExtendDirection();

        elapsed += Time.deltaTime;
        var extendElapsed = Mathf.Max(0f, elapsed - extendStartDelaySeconds);
        var lengthBySpeed = initialColliderSize.z + extendSpeed * extendElapsed;
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
        other.TryGetComponent<Hurtbox>(out var hurtbox);
        if(hurtbox == null) return;
        other.TryGetComponent<StrikerHub>(out var strikerHub);
        if (strikerHub != null && strikerHub == ownerStrikerHub) return;

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
