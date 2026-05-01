

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class Ex {
    public static Vector3 X(this Vector3 self, float v) {
        self.x = v;
        return self;
    }
    public static Vector3 Y(this Vector3 self, float v) {
        self.y = v;
        return self;
    }
    public static Vector3 Z(this Vector3 self, float v) {
        self.z = v;
        return self;
    }

    public static Coroutine Delay(this MonoBehaviour monoBehaviour, System.Action action, float delay)
    {
        // If the target MonoBehaviour is inactive or disabled we cannot start a coroutine on it.
        // Use a central always-active runner in that case so delayed actions still execute.
        if (monoBehaviour == null || !monoBehaviour.gameObject.activeInHierarchy || !monoBehaviour.enabled)
        {
            return CoroutineRunner.Instance.StartCoroutine(CoroutineAction(action, delay));
        }
        return monoBehaviour.StartCoroutine(CoroutineAction(action, delay));
    }

    private static IEnumerator CoroutineAction(Action action, float delay) {
        if (delay > 0f) {
            float elapsed = 0f;
            while (elapsed < delay) {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        action?.Invoke();
    }
    
    public static IEnumerator Wait(float seconds) {
        if (seconds <= 0f) {
            yield break;
        }
        float elapsed = 0f;
        while (elapsed < seconds) {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    public static IEnumerator WaitRealtime(float seconds) {
        if (seconds <= 0f) {
            yield break;
        }
        float elapsed = 0f;
        while (elapsed < seconds) {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    public static async Awaitable WaitAsync(float seconds) {
        if (seconds <= 0f) {
            return;
        }
        float elapsed = 0f;
        while (elapsed < seconds) {
            elapsed += Time.deltaTime;
            await Awaitable.NextFrameAsync();
        }
    }
    
    public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
    {
        foreach (var item in source)
            action(item);
    }
}

/// <summary>
/// A small helper MonoBehaviour that ensures coroutines can be started even if the caller
/// MonoBehaviour is inactive. The GameObject is created on demand and marked DontDestroyOnLoad.
/// </summary>
internal class CoroutineRunner : MonoBehaviour
{
    private static CoroutineRunner _instance;
    public static CoroutineRunner Instance
    {
        get
        {
            if (_instance != null) return _instance;
            var go = GameObject.Find("[CoroutineRunner]");
            if (go == null)
            {
                go = new GameObject("[CoroutineRunner]");
                DontDestroyOnLoad(go);
            }
            _instance = go.GetComponent<CoroutineRunner>();
            if (_instance == null) _instance = go.AddComponent<CoroutineRunner>();
            return _instance;
        }
    }
}

public class EventWrapper<T> {
    public event Action<T> Handler;

    public void Add(Action<T> handler) => Handler += handler;
    public void Add(Action handler) => Handler += _ => handler();

    public void Invoke(T arg) => Handler?.Invoke(arg);
}

public class EventWrapper {
    private event Action handlers;
    public void Add(Action h) => handlers += h;
    public void Invoke() => handlers?.Invoke();
}

