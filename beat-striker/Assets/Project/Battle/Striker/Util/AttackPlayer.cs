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
    [SerializeField] EffectPlayer attackEffectPlayer, hitEffectPlayer;
    [SerializeField] AudioClip attackSound, hitSound;
    [SerializeField] bool multipleHit = false;
    [SerializeField] float hitDetectionStartTime = 0f;
    [SerializeField] float hitDetectionDuration = 0.5f;
    readonly List<Hit> hitsInFrame = new();
    public record Hit(Vector3 hitPoint, Collider collider);
    public Observable<Collider> OnHit => onHitSubject;
    private readonly Subject<Collider> onHitSubject = new();
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

        attackEffectPlayer.Emit(effectTransform.position, effectTransform.rotation, effectTransform.lossyScale);
        AudioSource.PlayClipAtPoint(attackSound, effectTransform.position);

        enabled = true;
    }

    void OnEnable() {
        episodeTime = 0f;
        hitsInFrame.Clear();
        isVirgin = true;
    }

    void OnDisable() {
        hitsInFrame.Clear();
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

        hitsInFrame.RemoveAll(e => e.collider == null);
        if (hitsInFrame.Count == 0) return;

        var closestHit = hitsInFrame.MinBy(e => Vector3.Distance(e.hitPoint, transform.position));
        ExecuteHit(closestHit);
    }

    void ExecuteHit(Hit hit) {
        if(enabled == false) return;
        hitEffectPlayer.Emit(hit.hitPoint, Quaternion.identity, Vector3.one);
        AudioSource.PlayClipAtPoint(hitSound, hit.hitPoint);
        onHitSubject.OnNext(hit.collider);

        // In multi-hit mode, keep listening until hitDetectionDuration expires.
        if (multipleHit == false) {
            enabled = false;
        }
    }
}
