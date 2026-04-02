using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

[AddComponentMenu(" 🟠Effect Player", 0)]
public class EffectPlayer : MonoBehaviour {
    [Header("References")]
    [SerializeField] private ParticleSystem effectPrefab;

    [Header("Pool")]
    [Min(1)]
    [SerializeField] private int initialPoolSize = 4;
    [Min(1)]
    [SerializeField] private int maxPoolSize = 32;

    private IObjectPool<ParticleSystem> pool;
    private readonly HashSet<ParticleSystem> playingEffects = new();

    private void Awake() {
        pool = new ObjectPool<ParticleSystem>(
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
        var effectInstanceTransform = effect.transform;
        effectInstanceTransform.SetPositionAndRotation(position, rotation);
        effectInstanceTransform.localScale = scale;

        effect.Play(true);
        playingEffects.Add(effect);
        StartCoroutine(ReturnToPoolWhenFinished(effect));
    }

    private ParticleSystem CreateEffect() {
        var effect = Instantiate(effectPrefab, transform);
        effect.gameObject.SetActive(false);
        return effect;
    }

    private void OnGet(ParticleSystem effect) {
        effect.gameObject.SetActive(true);
    }

    private void OnRelease(ParticleSystem effect) {
        effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        effect.gameObject.SetActive(false);
        playingEffects.Remove(effect);
    }

    private void OnDestroyEffect(ParticleSystem effect) {
        Destroy(effect.gameObject);
    }

    private IEnumerator ReturnToPoolWhenFinished(ParticleSystem effect) {
        while (effect.IsAlive(true)) {
            yield return null;
        }

        if (playingEffects.Contains(effect)) {
            pool.Release(effect);
        }
    }

    private void OnDisable() {
        if (pool == null || playingEffects.Count == 0) {
            return;
        }

        var snapshot = new List<ParticleSystem>(playingEffects);
        for (var i = 0; i < snapshot.Count; i++) {
            pool.Release(snapshot[i]);
        }
    }
}
