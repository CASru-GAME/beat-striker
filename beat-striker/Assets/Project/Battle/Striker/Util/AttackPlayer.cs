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

    private void Awake() {
        enabled = false;
    }

    public void Emit(Transform effectTransform = null) {
        if(enabled) return;

        effectTransform = effectTransform != null ? effectTransform : transform;

        if(attackEffectPlayer != null) attackEffectPlayer.Emit(effectTransform.position, effectTransform.rotation, effectTransform.lossyScale);
        if(attackSound != null) AudioSource.PlayClipAtPoint(attackSound, effectTransform.position);

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
            if(clip != null) AudioSource.PlayClipAtPoint(clip, hit.Position);

            var effectPlayer = hitType == HitType.Blocked ? blockedHitEffectPlayer : hitEffectPlayer;
            effectPlayer.Emit(hit.Position, Quaternion.identity, Vector3.one);
        }
        
        // In multi-hit mode, keep listening until hitDetectionDuration expires.
        if (multipleHit == false) {
            enabled = false;
        }
    }
}
