using Core.Battle;
using UnityEngine;

public class HeroAttackSMB : StateMachineBehaviour {
    Colliden colliden;
    [SerializeField] private GameObject hitEffectPrefab; // ヒットエフェクトのプレハブ
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        var view = animator.GetComponent<StrikerView>();
        if (view == null) {
            Debug.LogWarning("HeroAttackSMB: StrikerView not found on animator's GameObject.");
            colliden = null;
            return;
        }
        colliden = view.GetColliden("sword");
        if (colliden == null) {
            Debug.LogWarning("HeroAttackSMB: colliden 'sword' not found. Check StrikerView.collidenRefs and weapon GameObject.");
        } else {
            colliden.OnEnterTrigger += OnEnterTrigger;
        }
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        if (colliden != null) {
            colliden.OnEnterTrigger -= OnEnterTrigger;
        }

    }

    void OnEnterTrigger(Collider other) {
        var target = other.GetComponent<StrikerView>();
        Debug.Log("HeroAttackSMB.OnEnterTrigger called. Other=" + (other ? other.name : "null") + ", colliden=" + (colliden ? colliden.name : "null"));
        if (target != null) {
            Debug.Log("Hit target: " + target.name);
            target.TakeDamage(new HitStatus(10));

            // ヒットエフェクトの生成
            if (hitEffectPrefab == null) {
                Debug.LogWarning("Hit effect prefab is not assigned on HeroAttackSMB.");
                return;
            }

            Vector3 hitPosition = other.ClosestPoint(colliden != null ? colliden.transform.position : other.transform.position);
            GameObject effect = Instantiate(hitEffectPrefab, hitPosition, Quaternion.identity);
            Debug.Log("Instantiated hit effect: " + (effect ? effect.name : "null"));

            // 明示的に再生して、再生終了後に自動破棄する
            ParticleSystem particles = effect.GetComponent<ParticleSystem>();
            if (particles != null) {
                var main = particles.main;
                // Stop Play On Awake in prefab recommended; we call Play() explicitly here anyway.
                particles.Play();

                // Calculate a safe lifetime to destroy the GameObject after effect finishes
                float life = main.duration;
                // startLifetime may be a MinMaxCurve; try to get a reasonable max value
                try {
                    life += main.startLifetime.constantMax;
                } catch {
                    // fallback if constantMax not available
                    life += 1f;
                }
                // Add a small margin
                Destroy(effect, life + 0.1f);
                Debug.Log("Will destroy effect in " + (life + 0.1f) + " seconds.");
            } else {
                // If it's not a ParticleSystem root, try to find one inside children
                var ps = effect.GetComponentInChildren<ParticleSystem>();
                if (ps != null) {
                    ps.Play();
                    var main = ps.main;
                    float life = main.duration;
                    try {
                        life += main.startLifetime.constantMax;
                    } catch {
                        life += 1f;
                    }
                    Destroy(effect, life + 0.1f);
                    Debug.Log("Will destroy nested effect in " + (life + 0.1f) + " seconds.");
                } else {
                    Debug.LogWarning("No ParticleSystem found on hit effect instance: " + effect.name);
                    // Safety: destroy after 3 seconds
                    Destroy(effect, 3f);
                }
            }
        }
    }
    }
