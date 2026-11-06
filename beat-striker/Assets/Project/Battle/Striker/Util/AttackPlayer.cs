using System;
using System.Collections;
using System.Collections.Generic;
using App;
using R3;
using UnityEngine;
using UnityEngine.Pool;

[RequireComponent(typeof(Collider))]
[AddComponentMenu(" 🟠Attack Player")]
public class AttackPlayer : MonoBehaviour {
    [SerializeField] EffectPlayer attackEffectPlayer, hitEffectPlayer, blockedHitEffectPlayer;
    [SerializeField] AudioClip attackSound, hitSound, blockedHitSound;
    [SerializeField] bool multipleHit = false;
    [SerializeField] float hitDetectionStartTime = 0f;
    [SerializeField] float hitDetectionDuration = 0.5f;
    readonly List<Hit> hitsInFrame = new();
    public record Hit(Vector3 Position, Collider Collider);
    public enum HitType {
        Normal,
        Blocked,
        Cancel,
    }
    public ObservableFunc<Hit, HitType> OnHit => onHitSubject;
    private readonly FuncSubject<Hit, HitType> onHitSubject = new();
    public ObservableFunc<Collider, bool> OnFilterHit => onFilterHitSubject;
    private readonly FuncSubject<Collider, bool> onFilterHitSubject = new();
    float episodeTime;
    bool isVirgin;
    Collider observationCollider;

    public Bounds ObservationBounds => observationCollider.bounds;
    // Collider.bounds is empty while disabled; forecasts must use the authored shape.
    public Bounds ObservationAuthoredBounds {
        get {
            if (observationCollider is SphereCollider sphere) {
                var scale = sphere.transform.lossyScale;
                var radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                return new Bounds(sphere.transform.TransformPoint(sphere.center), Vector3.one * radius * 2f);
            }
            if (observationCollider is BoxCollider box) {
                var bounds = new Bounds(box.transform.TransformPoint(box.center), Vector3.zero);
                for (var i = 0; i < 8; i++) {
                    var signs = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                    bounds.Encapsulate(box.transform.TransformPoint(box.center + Vector3.Scale(box.size * .5f, signs)));
                }
                return bounds;
            }
            return observationCollider.bounds;
        }
    }

    public float? ObservationEffectLifetimeSeconds => attackEffectPlayer != null ? attackEffectPlayer.ObservationFixedLifetimeSeconds : null;
    public float ObservationHitStart => hitDetectionStartTime;
    public float ObservationHitEnd => hitDetectionStartTime + hitDetectionDuration;
    public float ObservationStartDelay => Mathf.Max(0f, hitDetectionStartTime - episodeTime);
    public float ObservationRemainingSeconds => enabled ? Mathf.Max(0f, hitDetectionStartTime + hitDetectionDuration - episodeTime) : 0f;

    private void Awake() {
        observationCollider = GetComponent<Collider>();
        enabled = false;
    }

    public void Emit(Transform effectTransform = null) {
        if(enabled) return;

        effectTransform = effectTransform != null ? effectTransform : transform;

        if(attackEffectPlayer != null) attackEffectPlayer.Emit(effectTransform.position, effectTransform.rotation, effectTransform.lossyScale);
        if(attackSound != null) attackSound.PlayAtApp(effectTransform.position);

        enabled = true;
    }

    void OnEnable() {
        episodeTime = 0f;
        hitsInFrame.Clear();
        isVirgin = true;
        if(TryGetComponent<Collider>(out var collider)) {
            collider.enabled = true;
        }
    }

    void OnDisable() {
        hitsInFrame.Clear();
        if(TryGetComponent<Collider>(out var collider)) {
            collider.enabled = false;
        }
    }

    void OnTriggerStay(Collider other) {
        if(!isVirgin) return;
        OnTriggerEnter(other);
    }

    void OnTriggerEnter(Collider other) {
        if (episodeTime < hitDetectionStartTime) return;
        if (!onFilterHitSubject.InvokeAllAnd(other)) return;
        isVirgin = false;

        var hitPoint = other.ClosestPoint(transform.position);
        var hit = new Hit(hitPoint, other);

        if (multipleHit) {
            ExecuteHit(hit);
            return;
        }

        hitsInFrame.Add(hit);
    }

    void Update() {
        episodeTime += Time.deltaTime;
        if(episodeTime > hitDetectionStartTime + hitDetectionDuration) {
            enabled = false;
            return;
        }

        hitsInFrame.RemoveAll(e => e.Collider == null);
        if (hitsInFrame.Count == 0) return;

        var closestHit = hitsInFrame.MinBy(e => Vector3.Distance(e.Position, transform.position));
        ExecuteHit(closestHit);
    }

    void ExecuteHit(Hit hit) {
        if(enabled == false) return;

        if(onHitSubject.InvokeAllAndTryGetFirst(hit, out var hitType) && hitType != HitType.Cancel) {
            var clip = hitType == HitType.Blocked ? blockedHitSound : hitSound;
            if(clip != null) clip.PlayAtApp(hit.Position);

            var effectPlayer = hitType == HitType.Blocked ? blockedHitEffectPlayer : hitEffectPlayer;
            effectPlayer.Emit(hit.Position, Quaternion.identity, Vector3.one);
        }
        
        // In multi-hit mode, keep listening until hitDetectionDuration expires.
        if (multipleHit == false) {
            enabled = false;
        }
    }
}
