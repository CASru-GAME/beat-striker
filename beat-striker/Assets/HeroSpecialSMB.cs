using System.Collections;
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
    [SerializeField] public int projectileWaves = 5; // プロジェクトイルを発射する回数
    [SerializeField] public float waveInterval = 0.2f; // 各発射の間隔（秒）
    [SerializeField] public float[] waveHeights = new float[] { 0f, 0.5f, 1f, 0.5f, 0f }; // 各発射の高さオフセット
    [SerializeField] public float[] waveHueOffsets = new float[] { 0f, 0.2f, 0.4f, 0.6f, 0.8f }; // 各発射の色相オフセット（0-1）

    // runtime
    private bool hasSpawned = false;
    private Coroutine spawnCoroutine = null;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // reset spawn flag when entering the state
        hasSpawned = false;
        
        // 既存のコルーチンを停止
        if (spawnCoroutine != null)
        {
            var view = animator.GetComponent<Core.Battle.StrikerView>();
            if (view != null)
            {
                view.StopCoroutine(spawnCoroutine);
            }
            spawnCoroutine = null;
        }
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // spawn at the configured normalized time (animation progress)
        if (!hasSpawned && stateInfo.normalizedTime >= spawnNormalizedTime)
        {
            hasSpawned = true;
            var view = animator.GetComponent<Core.Battle.StrikerView>();
            if (view != null)
            {
                spawnCoroutine = view.StartCoroutine(SpawnSpecialWaves(animator));
            }
            else
            {
                // フォールバック: 1回だけ発射
                SpawnSpecial(animator, slashPrefab, count, spreadAngle, speed, damage, hitEffectPrefab, 0f, 0f);
            }
        }
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        hasSpawned = false;
        // コルーチンを停止
        if (spawnCoroutine != null)
        {
            var view = animator.GetComponent<Core.Battle.StrikerView>();
            if (view != null)
            {
                view.StopCoroutine(spawnCoroutine);
            }
            spawnCoroutine = null;
        }
    }

    private IEnumerator SpawnSpecialWaves(Animator animator)
    {
        for (int i = 0; i < projectileWaves; i++)
        {
            // 各発射ごとの高さオフセットを取得（配列の範囲を超えた場合は0）
            float heightOffset = (i < waveHeights.Length) ? waveHeights[i] : 0f;
            // 各発射ごとの色相オフセットを取得（配列の範囲を超えた場合は0）
            float hueOffset = (i < waveHueOffsets.Length) ? waveHueOffsets[i] : 0f;
            
            SpawnSpecial(animator, slashPrefab, count, spreadAngle, speed, damage, hitEffectPrefab, heightOffset, hueOffset);
            Debug.Log($"Special projectile wave {i + 1}/{projectileWaves} at height offset {heightOffset}, hue offset {hueOffset}");
            
            // 最後の発射以外は間隔をあける
            if (i < projectileWaves - 1)
            {
                yield return new WaitForSeconds(waveInterval);
            }
        }
        spawnCoroutine = null;
    }

    // Public helper so other scripts (or test components) can trigger the same spawn logic.
    public static void SpawnSpecial(Animator animator, GameObject slashPrefab, int count, float spreadAngle, float speed, int damage, GameObject hitEffectPrefab, float heightOffset = 0f, float hueOffset = 0f)
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
            Debug.Log($"HeroSpecialSMB.SpawnSpecial: spawning {count} projectiles using slashPrefab={(slashPrefab? slashPrefab.name : "null")} hitEffectPrefab={(hitEffectPrefab? hitEffectPrefab.name : "null")} heightOffset={heightOffset} hueOffset={hueOffset}");
            view.SpawnSpecialProjectiles(slashPrefab, count, spreadAngle, speed, damage, hitEffectPrefab, 0.1f, heightOffset, hueOffset);
        }
    }
}
