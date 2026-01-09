using System;
using NUnit.Framework;
using UnityEngine;

public sealed class Life : MonoBehaviour, ILife, ILifeMutater {
    public event Action OnEnabled = delegate { };
    public event Action OnDisabled = delegate { };
    
    private bool isEnabled = false;

    void OnEnable() {
        if (isEnabled) return;
        isEnabled = true;   
        OnEnabled.Invoke();
    }

    void OnDisable() {
        if (!isEnabled) return;
        isEnabled = false;
        OnDisabled.Invoke();
    }

    void OnDestroy() {
        if (isEnabled) {
            isEnabled = false;
            OnDisabled.Invoke();
        }
    }

    public void Link(Action onEnabled, Action onDisabled) {
        OnEnabled += onEnabled;
        OnDisabled += onDisabled;

        if (isEnabled) {
            onEnabled?.Invoke();
        }
    }
    
    public void Unlink(Action onEnabled, Action onDisabled) {
        OnEnabled -= onEnabled;
        OnDisabled -= onDisabled;
    }

    public void SetEnable(bool isEnabled) {
        if (isEnabled) OnEnable();
        else OnDisable();
    }
}