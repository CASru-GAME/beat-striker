using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

[AddComponentMenu(" 🟠Effect Player", 0)]
public class EffectPlayer : MonoBehaviour {
    [Header("References")]
    [SerializeField] private GameObject effectPrefab;

    [Header("Lifetime")]
    [Tooltip("ParticleSystem がないプレハブを、何秒後にプールへ返却するか")]
    [Min(0.01f)]
    [SerializeField] private float fallbackLifetimeSeconds = 1f;

    [Header("Pool")]
    [Min(1)]
    [SerializeField] private int initialPoolSize = 4;
    [Min(1)]
    [SerializeField] private int maxPoolSize = 32;

    private IObjectPool<GameObject> pool;
    private readonly HashSet<GameObject> playingEffects = new();
    private readonly Dictionary<GameObject, ParticleSystem> particleSystemByInstance = new();

    private void Awake() {
        pool = new ObjectPool<GameObject>(
            CreateEffect,
            OnGet,
            OnRelease,
            OnDestroyEffect,
            collectionCheck: false,
            defaultCapacity: initialPoolSize,
            maxSize: maxPoolSize
        );

        for (var i = 0; i < initialPoolSize; i++) {
            var effect = pool.Get();
            pool.Release(effect);
        }
    }

    public void Emit(Transform effectTransform) {
        Emit(effectTransform.position, effectTransform.rotation, effectTransform.lossyScale);
    }

    public void Emit(Vector3 position, Quaternion rotation, Vector3 scale) {
        var effect = pool.Get();
        if (!effect) {
            effect = CreateEffect();
        }

        var effectInstanceTransform = effect.transform;
        effectInstanceTransform.SetPositionAndRotation(position, rotation);
        effectInstanceTransform.localScale = scale;

        effect.SetActive(true);

        var particleSystem = particleSystemByInstance.TryGetValue(effect, out var registeredParticleSystem) ? registeredParticleSystem : null;
        if (particleSystem) {
            particleSystem.Play(true);
        }

        playingEffects.Add(effect);

        if (particleSystem) {
            StartCoroutine(ReturnToPoolWhenFinished(effect, particleSystem));
            return;
        }

        StartCoroutine(ReturnToPoolAfterDelay(effect));
    }

    private GameObject CreateEffect() {
        var effect = Instantiate(effectPrefab, transform);
        effect.TryGetComponent<ParticleSystem>(out var particleSystem);
        particleSystemByInstance[effect] = particleSystem;
        effect.SetActive(false);
        return effect;
    }

    private void OnGet(GameObject effect) {
    }

    private void OnRelease(GameObject effect) {
        if (!effect) {
            CleanupEffect(effect);
            return;
        }

        if (particleSystemByInstance.TryGetValue(effect, out var particleSystem) && particleSystem) {
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        effect.SetActive(false);
        CleanupEffect(effect);
    }

    private void OnDestroyEffect(GameObject effect) {
        particleSystemByInstance.Remove(effect);
        Destroy(effect);
    }

    private IEnumerator ReturnToPoolWhenFinished(GameObject effect, ParticleSystem particleSystem) {
        while (particleSystem && particleSystem.IsAlive(true)) {
            yield return null;
        }

        if (effect && playingEffects.Contains(effect)) {
            pool.Release(effect);
            yield break;
        }

        CleanupEffect(effect);
    }

    private IEnumerator ReturnToPoolAfterDelay(GameObject effect) {
        yield return new WaitForSeconds(fallbackLifetimeSeconds);

        if (effect && playingEffects.Contains(effect)) {
            pool.Release(effect);
            yield break;
        }

        CleanupEffect(effect);
    }

    private void OnDisable() {
        if (pool == null || playingEffects.Count == 0) {
            return;
        }

        var snapshot = new List<GameObject>(playingEffects);
        for (var i = 0; i < snapshot.Count; i++) {
            var effect = snapshot[i];
            if (effect) {
                pool.Release(effect);
                continue;
            }

            CleanupEffect(effect);
        }
    }

    private void CleanupEffect(GameObject effect) {
        playingEffects.Remove(effect);
    }
}
