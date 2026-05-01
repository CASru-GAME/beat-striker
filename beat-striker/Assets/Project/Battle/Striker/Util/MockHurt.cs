using UnityEngine;
using System;
using Alice;
using R3;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Hurtbox))]
public class MockHurt : MonoBehaviour
{
    private Vector3 _initialPosition;
    private Coroutine _returnCoroutine;

    void Awake() {
        var rb = GetComponent<Rigidbody>();
        _initialPosition = transform.position;
        GetComponent<Hurtbox>().OnHit.Subscribe(status => {
            Debug.Log(status.KnockbackVelocity);
            rb.linearVelocity = status.KnockbackVelocity;
            if (_returnCoroutine != null)
            {
                StopCoroutine(_returnCoroutine);
            }
            _returnCoroutine = StartCoroutine(ReturnToInitialPositionAfterDelay(1f));
        }).AddTo(this);
    }

    private System.Collections.IEnumerator ReturnToInitialPositionAfterDelay(float delay)
    {
        yield return Ex.Wait(delay);
        transform.position = _initialPosition;
        var rb = GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero;
        _returnCoroutine = null;
    }
}
