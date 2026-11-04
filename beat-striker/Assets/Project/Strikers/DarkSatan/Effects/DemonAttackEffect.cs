using System.Collections.Generic;
using UnityEngine;

namespace Core.DarkSatan {
    public sealed class DemonAttackEffect : MonoBehaviour {
        [SerializeField] ParticleSystem lightning;
        [SerializeField] Renderer[] bodyRenderers;
        [SerializeField, ColorUsage(false, true)] Color glowColor = new(0.12f, 0.002f, 0.004f);
        [SerializeField, Min(0.01f)] float fadeSeconds = 0.12f;

        readonly Dictionary<Material, Material> materialInstances = new();
        readonly Dictionary<Material, Color> originalEmission = new();
        Material[][] originalMaterials;
        float remaining;
        bool playing;

        void Awake() {
            originalMaterials = new Material[bodyRenderers.Length][];
            for (int i = 0; i < bodyRenderers.Length; i++) {
                var materials = bodyRenderers[i].sharedMaterials;
                originalMaterials[i] = materials;
                var instances = new Material[materials.Length];
                for (int j = 0; j < materials.Length; j++) {
                    var source = materials[j];
                    if (!materialInstances.TryGetValue(source, out var instance)) {
                        instance = new Material(source);
                        instance.EnableKeyword("_EMISSION");
                        materialInstances.Add(source, instance);
                        originalEmission.Add(instance, source.GetColor("_EmissionColor"));
                    }
                    instances[j] = instance;
                }
                bodyRenderers[i].sharedMaterials = instances;
            }
            Stop();
        }

        public void Play(float duration) {
            remaining = Mathf.Max(duration, fadeSeconds);
            playing = true;
            SetGlow(1f);
            lightning.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            lightning.Play(true);
        }

        void Update() {
            if (!playing) return;
            remaining -= Time.deltaTime;
            if (remaining <= 0f) {
                Stop();
                return;
            }
            SetGlow(Mathf.Clamp01(remaining / fadeSeconds));
        }

        void SetGlow(float intensity) {
            foreach (var pair in originalEmission) {
                pair.Key.SetColor("_EmissionColor", pair.Value + glowColor * intensity);
            }
        }

        public void Stop() {
            playing = false;
            remaining = 0f;
            SetGlow(0f);
            lightning.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void OnDisable() => Stop();

        void OnDestroy() {
            if (originalMaterials == null) return;
            for (int i = 0; i < bodyRenderers.Length; i++) {
                if (bodyRenderers[i] != null) bodyRenderers[i].sharedMaterials = originalMaterials[i];
            }
            foreach (var instance in materialInstances.Values) Destroy(instance);
        }
    }
}
