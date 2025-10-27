

using System;
using UnityEngine;

public interface ILife {
    void Link(Action onEnabled, Action onDisabled);
    void Unlink(Action onEnabled, Action onDisabled);
}

public sealed class Life : MonoBehaviour, ILife {
    public event Action OnEnabled = delegate { };
    public event Action OnDisabled = delegate { };

    void OnEnable() {
        OnEnabled.Invoke();
    }

    void OnDisable() {
        OnDisabled.Invoke();
    }

    public void Link(Action onEnabled, Action onDisabled) {
        OnEnabled += onEnabled;
        OnDisabled += onDisabled;
    }
    
    public void Unlink(Action onEnabled, Action onDisabled) {
        OnEnabled -= onEnabled;
        OnDisabled -= onDisabled;
    }
}