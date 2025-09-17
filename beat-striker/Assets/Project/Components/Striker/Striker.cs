using System;
using UnityEngine;

public class Striker : MonoBehaviour {
    public float hp = 100;
    public bool isGround { get; private set; }
    [NonSerialized] public Player player;
    public event Action OnLanded,OnTakeoff;

    void Start() {

    }

    void Update() {
    }
    
    private void OnCollisionStay(Collision collision)
    {
        foreach (var contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                if (!isGround) OnLanded?.Invoke();
                isGround = true;
                return;
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (isGround) OnTakeoff?.Invoke();
        isGround = false;
    }
}
