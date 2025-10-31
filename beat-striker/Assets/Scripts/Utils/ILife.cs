

using System;
using UnityEngine;

public interface ILife {
    void Link(Action onEnabled, Action onDisabled);
    void Unlink(Action onEnabled, Action onDisabled);
}

public interface ILifeMutater {
    void SetEnable(bool isEnabled);
}