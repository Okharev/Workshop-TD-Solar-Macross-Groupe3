using System;
using UnityEngine;

public static class ReactiveExtensions
{
    // --- 1. Standard Subscription ---
    
    // Allows: myInt.Subscribe(val => Debug.Log(val))
    public static IDisposable Subscribe<T>(
        this IReadOnlyReactiveProperty<T> source, 
        Action<T> onNext, 
        bool fireImmediately = true)
    {
        if (fireImmediately) onNext(source.Value);
        source.OnValueChanged += onNext;
        return new ActionDisposable(() => source.OnValueChanged -= onNext);
    }

    public static IDisposable Subscribe<T>(
        this IReadOnlyReactiveProperty<T> source,
        Func<T, bool> predicate,
        Action<T> onNext)
    {
        var observer = ObserverPool<T, T>.Get();
        observer.ConfigureWhere(source, predicate, onNext);
        return observer;
    }

    public static IDisposable Subscribe<T, R>(
        this IReadOnlyReactiveProperty<T> source,
        Func<T, R> selector,
        Action<R> onNext)
    {
        var observer = ObserverPool<T, R>.Get();
        observer.ConfigureSelect(source, selector, onNext);
        return observer;
    }
    // --- 3. Lifecycle Management ---

    public static void AddTo(this IDisposable disposable, Component component)
    {
        if (component == null) return;

        // Optimization: TryGetComponent prevents garbage allocation compared to GetComponent
        if (!component.TryGetComponent(out DisposableTracker tracker))
        {
            tracker = component.gameObject.AddComponent<DisposableTracker>();
            tracker.hideFlags = HideFlags.HideInInspector;
        }

        tracker.Add(disposable);
    }
}