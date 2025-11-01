using System;
using UnityEngine;

public sealed class Life : MonoBehaviour, ILife, ILifeMutater {
    public event Action OnEnabled = delegate { };
    public event Action OnDisabled = delegate { };
    
    private bool isEnabled = false;

    void OnEnable() {
        ApplyEnable(true);
    }

    void OnDisable() {
        ApplyEnable(false);
    }

    public void Link(Action onEnabled, Action onDisabled) {
        if (onEnabled != null) OnEnabled += onEnabled;
        if (onDisabled != null) OnDisabled += onDisabled;

        if (isEnabled) {
            onEnabled?.Invoke();
        }
    }
    
    public void Unlink(Action onEnabled, Action onDisabled) {
        if (onEnabled != null) OnEnabled -= onEnabled;
        if (onDisabled != null) OnDisabled -= onDisabled;
    }

    public void SetEnable(bool isEnabled) {
        ApplyEnable(isEnabled);
    }

    // Centralized enable/disable logic so callers don't need to invoke Unity
    // lifecycle methods directly. Unity will still call OnEnable/OnDisable;
    // ApplyEnable guards against duplicate invocations.
    private void ApplyEnable(bool enable) {
        if (enable) {
            if (isEnabled) return;
            isEnabled = true;
            OnEnabled.Invoke();
        } else {
            if (!isEnabled) return;
            isEnabled = false;
            OnDisabled.Invoke();
        }
    }
}