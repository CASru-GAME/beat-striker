using UnityEngine;
using System;

[RequireComponent(typeof(Collider))]
public class Colliden : MonoBehaviour
{
    public event Action<Collider> OnEnterTrigger;
    public event Action<Collider> OnExitTrigger;
    public event Action<Collider> OnStayTrigger;
    public event Action<Collision> OnEnterCollision;
    public event Action<Collision> OnExitCollision;
    public event Action<Collision> OnStayCollision;

    void OnTriggerEnter(Collider other) => OnEnterTrigger?.Invoke(other);
    void OnTriggerExit(Collider other)  => OnExitTrigger?.Invoke(other);
    void OnTriggerStay(Collider other)  => OnStayTrigger?.Invoke(other);
    void OnCollisionEnter(Collision collision) => OnEnterCollision?.Invoke(collision);
    void OnCollisionExit(Collision collision)  => OnExitCollision?.Invoke(collision);
    void OnCollisionStay(Collision collision)  => OnStayCollision?.Invoke(collision);
}
