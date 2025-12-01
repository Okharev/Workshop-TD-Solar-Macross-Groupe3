using System;
using System.Collections;
using System.Collections.Generic;
using Towers;
using UnityEngine;

public static class ReactiveExtensions
{
    public static IDisposable Subscribe<T, R>(
        this ReactiveProperty<T> source,
        Func<T, R> selector,
        Action<R> onNext)
    {
        return source.Subscribe(val => onNext(selector(val)));
    }

    // WHERE: Filter (e.g., Only update UI if damage > 0)
    public static IDisposable Subscribe<T>(
        this ReactiveProperty<T> source,
        Func<T, bool> predicate,
        Action<T> onNext)
    {
        return source.Subscribe(val =>
        {
            if (predicate(val)) onNext(val);
        });
    }

    // --- 1. Subscribe Extensions ---

    // Allows: myReactiveInt.Subscribe(val => ...)
    public static IDisposable Subscribe<T>(this ReactiveProperty<T> source, Action<T> onNext)
    {
        // 1. Call immediately (Standard Reactive behavior)
        onNext(source.Value);

        // 2. Subscribe to event
        source.OnValueChanged += onNext;

        // 3. Return an object that unsubscribes when Dispose is called
        return new ActionDisposable(() => source.OnValueChanged -= onNext);
    }

    // Allows: myStat.Subscribe(val => ...)
    public static IDisposable Subscribe(this Stat source, Action<float> onNext)
    {
        // 1. Call immediately
        onNext(source.Value);

        // 2. Subscribe to event
        source.OnValueChanged += onNext;

        // 3. Return unsubscribe token
        return new ActionDisposable(() => source.OnValueChanged -= onNext);
    }

    // --- 2. AddTo (Lifecycle Management) ---

    // Allows: .AddTo(this) inside a MonoBehaviour
    public static void AddTo(this IDisposable disposable, Component component)
    {
        if (component == null) return; // Object already destroyed

        // Get or Add a component that listens for OnDestroy
        var tracker = component.GetComponent<DisposableTracker>();
        if (tracker == null)
        {
            tracker = component.gameObject.AddComponent<DisposableTracker>();
            // Hide it in Inspector so it doesn't clutter
            tracker.hideFlags = HideFlags.HideInInspector;
        }

        tracker.Add(disposable);
    }
}

// --- Helpers ---

// A simple wrapper that runs an Action when Disposed
public class ActionDisposable : IDisposable
{
    private Action _onDispose;

    public ActionDisposable(Action onDispose)
    {
        _onDispose = onDispose;
    }

    public void Dispose()
    {
        _onDispose?.Invoke();
        _onDispose = null;
    }
}

// A hidden component that cleans up subscriptions when the GameObject dies
public class DisposableTracker : MonoBehaviour
{
    private readonly List<IDisposable> _disposables = new();

    private void OnDestroy()
    {
        foreach (var d in _disposables) d.Dispose();
        _disposables.Clear();
    }

    public void Add(IDisposable disposable)
    {
        _disposables.Add(disposable);
    }
}

[Serializable]
public class ReactiveList<T> : IEnumerable<T>
{
    private readonly List<T> _innerList = new();

    public int Count => _innerList.Count;
    public T this[int index] => _innerList[index];

    // Enable foreach loops
    public IEnumerator<T> GetEnumerator()
    {
        return _innerList.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _innerList.GetEnumerator();
    }

    public event Action<T> OnItemAdded;
    public event Action<T> OnItemRemoved;
    public event Action OnCountChanged; // Useful for refreshing "Item Count: 5"

    public void Add(T item)
    {
        _innerList.Add(item);
        OnItemAdded?.Invoke(item);
        OnCountChanged?.Invoke();
    }

    public void Remove(T item)
    {
        if (_innerList.Remove(item))
        {
            OnItemRemoved?.Invoke(item);
            OnCountChanged?.Invoke();
        }
    }

    public void Clear()
    {
        foreach (var item in _innerList) OnItemRemoved?.Invoke(item);
        _innerList.Clear();
        OnCountChanged?.Invoke();
    }
}