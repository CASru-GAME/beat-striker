using Core.Battle;
using UnityEngine;

public class HeroAttackSMB : StateMachineBehaviour {
    Colliden colliden;
    [SerializeField] private GameObject hitEffectPrefab; // ヒットエフェクトのプレハブ
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        colliden = animator.GetComponent<StrikerView>().GetColliden("sword");
        colliden.OnEnterTrigger += OnEnterTrigger;
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        colliden.OnEnterTrigger -= OnEnterTrigger;

    }

    void OnEnterTrigger(Collider other) {
        var target = other.GetComponent<StrikerView>();
        if (target != null) {
            target.TakeDamage(new HitStatus(new HitPoint(10)));
            
            // ヒットエフェクトの生成
            if (hitEffectPrefab != null) {
                Vector3 hitPosition = other.ClosestPoint(colliden.transform.position);
                Instantiate(hitEffectPrefab, hitPosition, Quaternion.identity);
            }
        }
    }
    }
