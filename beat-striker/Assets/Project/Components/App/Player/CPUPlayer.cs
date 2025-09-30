using System;
using UnityEngine;

public class CPUPlayer : Player {
    const float EPS = 0.1f; 

    protected override void Awake() {
        base.Awake(); 
    }

    protected override void Start() {
        base.Start();
    }

    void Update() {
        UpdateDirectionInput();
    }

    private void UpdateDirectionInput() {
        bool shouldBePressed = direction.magnitude > EPS;
        bool currentlyPressed = isDown[Btn.Direction];

        if (shouldBePressed != currentlyPressed) {
            HandleButton(Btn.Direction, shouldBePressed);
        }
    }

    public void PressButton(Btn btn, float duration) {
        StartCoroutine(PressButtonCoroutine(btn, duration));
    }

    private System.Collections.IEnumerator PressButtonCoroutine(Btn btn, float duration) {
        HandleButton(btn, true);
        yield return new WaitForSeconds(duration);
        HandleButton(btn, false);
    }

    public void SetDirection(Vector2 dir) {
        direction = dir;
    }
}
