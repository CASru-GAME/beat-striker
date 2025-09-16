using System;
using UnityEngine;

public class Striker : MonoBehaviour {
    public float hp = 100;
    public bool isGround { get; private set; }
    [NonSerialized] public Player player;

    void Start() {

    }

    void Update() {
    }
    
    private void OnCollisionStay(Collision collision)
    {
        isGround = false;
        foreach (var contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                isGround = true;
                return;
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        isGround = false;
    }
}
