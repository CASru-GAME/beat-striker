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
    [Header("Collider Settings")]
    [SerializeField, Min(0f)] float maxLength = 12f;
    [Tooltip("コライダーが0.1秒あたりに伸びる長さ")]
    [SerializeField, Min(0f)] float colliderExtendLengthPer0p1Second = 4f;
    [SerializeField, Min(0f)] float extendTime = 0.3f;
    [SerializeField] ParticleSystem beam11ParticleSystem;

    BoxCollider hitCollider;
    Vector3 initialColliderSize;
    Vector3 initialColliderCenter;
    Hurtbox ownerHurtbox;
    StrikerHub ownerStrikerHub;
    float elapsed;
    float currentLength;
    bool isShrinking;
    float shrinkSpeedPerSecond;
    float nextAttackJudgeTime;

    void Awake()
    {
        beam11ParticleSystem = GetComponent<ParticleSystem>();
        hitCollider = GetComponent<BoxCollider>();
        hitCollider.isTrigger = true;
        ownerStrikerHub = GetComponentInParent<StrikerHub>();
        if (ownerStrikerHub != null)
        {
            ownerHurtbox = ownerStrikerHub.GetComponentInChildren<Hurtbox>();
        }

        initialColliderSize = hitCollider.size;
        initialColliderCenter = hitCollider.center;
        currentLength = initialColliderSize.z;
    }

    public void SetOwnerStrikerHub(StrikerHub strikerHub)
    {
        ownerStrikerHub = strikerHub;
        ownerHurtbox = strikerHub.GetComponentInChildren<Hurtbox>();
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
        if (beam11ParticleSystem != null && !beam11ParticleSystem.IsAlive(true))
        {
            Destroy(gameObject);
            return;
        }

        var localExtendDirectionNormalized = GetParticleShootDirectionLocal();

        var previousElapsed = elapsed;
        elapsed += Time.deltaTime;

        var growthDeltaTime = Time.deltaTime;
        if (extendTime > 0f)
        {
            var remainingGrowTime = extendTime - previousElapsed;
            if (remainingGrowTime <= 0f)
            {
                growthDeltaTime = 0f;
            }
            else if (growthDeltaTime > remainingGrowTime)
            {
                growthDeltaTime = remainingGrowTime;
            }
        }

        if (growthDeltaTime > 0f)
        {
            var colliderExtendSpeedPerSecond = colliderExtendLengthPer0p1Second / 0.1f;
            var speedMultiplier = Mathf.Pow(2f, Mathf.Floor(previousElapsed / 0.5f));
            var currentExtendSpeedPerSecond = colliderExtendSpeedPerSecond * speedMultiplier;

            if (!isShrinking)
            {
                currentLength = Mathf.Min(maxLength, currentLength + currentExtendSpeedPerSecond * growthDeltaTime);
                if (currentLength >= maxLength)
                {
                    isShrinking = true;
                    shrinkSpeedPerSecond = currentExtendSpeedPerSecond;
                }
            }
            else
            {
                currentLength = Mathf.Max(initialColliderSize.z, currentLength - shrinkSpeedPerSecond * growthDeltaTime);
            }
        }

        var size = initialColliderSize;
        size.z = currentLength;
        hitCollider.size = size;

        float centerOffset;
        if (!isShrinking)
        {
            centerOffset = (currentLength - initialColliderSize.z) * 0.5f;
        }
        else
        {
            // 先端側を固定して、進行方向と逆側（後端側）から短くする
            centerOffset = (2f * maxLength - initialColliderSize.z - currentLength) * 0.5f;
        }

        hitCollider.center = initialColliderCenter + localExtendDirectionNormalized * centerOffset;
    }

    void OnTriggerStay(Collider other)
    {
        if (Time.time < nextAttackJudgeTime)
        {
            return;
        }

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

        if (ownerHurtbox != null && hurtbox == ownerHurtbox)
        {
            return;
        }

        if (ownerStrikerHub != null)
        {
            var otherStrikerHub = other.GetComponentInParent<StrikerHub>();
            if (otherStrikerHub == null)
            {
                otherStrikerHub = hurtbox.GetComponentInParent<StrikerHub>();
            }

            if (otherStrikerHub == ownerStrikerHub)
            {
                return;
            }
        }

        var damage = damagePerSecond;
        var knockback = GetParticleShootDirectionWorld() * knockbackPerSecond;
        hurtbox.GiveHit(new HitStatus(damage, knockback));
        nextAttackJudgeTime = Time.time + 0.3f;
    }

    System.Collections.IEnumerator PlayAudioAfterDelay()
    {
        yield return new WaitForSeconds(audioPlayDelaySeconds);
        AudioSource.PlayClipAtPoint(audioClip, transform.position);
    }

    Vector3 GetParticleShootDirectionLocal()
    {
        var worldDirection = GetParticleShootDirectionWorld();
        if (worldDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector3.forward;
        }

        return transform.InverseTransformDirection(worldDirection).normalized;
    }

    Vector3 GetParticleShootDirectionWorld()
    {
        if (beam11ParticleSystem == null)
        {
            return transform.forward;
        }

        var direction = beam11ParticleSystem.transform.forward;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return transform.forward;
        }

        return direction.normalized;
    }
}
