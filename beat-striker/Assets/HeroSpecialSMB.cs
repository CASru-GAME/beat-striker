using UnityEngine;

// StateMachineBehaviour that spawns multiple slash projectiles when the special state starts.
public class HeroSpecialSMB : StateMachineBehaviour
{
    [SerializeField] public GameObject slashPrefab; // prefab that has SlashProjectile component
    [SerializeField] public int count = 12;
    [SerializeField] public float spreadAngle = 60f; // total spread in degrees
    [SerializeField] public float speed = 12f;
    [SerializeField] public int damage = 8;
    [SerializeField] public GameObject hitEffectPrefab;
    [SerializeField] public float spawnNormalizedTime = 0.5f; // 0..1 normalized time of the animation to spawn

    // runtime
    private bool hasSpawned = false;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // reset spawn flag when entering the state
        hasSpawned = false;
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // spawn at the configured normalized time (animation progress)
        if (!hasSpawned && stateInfo.normalizedTime >= spawnNormalizedTime)
        {
            hasSpawned = true;
            SpawnSpecial(animator, slashPrefab, count, spreadAngle, speed, damage, hitEffectPrefab);
        }
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        hasSpawned = false;
    }

    // Public helper so other scripts (or test components) can trigger the same spawn logic.
    public static void SpawnSpecial(Animator animator, GameObject slashPrefab, int count, float spreadAngle, float speed, int damage, GameObject hitEffectPrefab)
    {
        // If the behavior fields are not filled in the Animator state, try to fall back to a SpecialMoveTester
        if (slashPrefab == null && animator != null)
        {
            var tester = animator.GetComponent<SpecialMoveTester>();
            if (tester != null && tester.slashPrefab != null)
            {
                slashPrefab = tester.slashPrefab;
                if (hitEffectPrefab == null) hitEffectPrefab = tester.hitEffectPrefab;
                Debug.Log("HeroSpecialSMB.SpawnSpecial: using prefabs from SpecialMoveTester fallback.");
            }
        }

        if (slashPrefab == null)
        {
            Debug.LogWarning("HeroSpecialSMB.SpawnSpecial: slashPrefab not assigned (and no fallback available).");
            return;
        }

        var view = animator != null ? animator.GetComponent<Core.Battle.StrikerView>() : null;
        if (view != null)
        {
            Debug.Log($"HeroSpecialSMB.SpawnSpecial: spawning {count} projectiles using slashPrefab={(slashPrefab? slashPrefab.name : "null")} hitEffectPrefab={(hitEffectPrefab? hitEffectPrefab.name : "null")}");
            view.SpawnSpecialProjectiles(slashPrefab, count, spreadAngle, speed, damage, hitEffectPrefab, 0.1f);
        }
    }
}
