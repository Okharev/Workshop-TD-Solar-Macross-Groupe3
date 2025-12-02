using System;
using System.Collections.Generic;
using UnityEngine;

// A hidden component that cleans up subscriptions when the GameObject dies
public class DisposableTracker : MonoBehaviour
{
    private readonly List<IDisposable> _disposables = new();

    private void OnDestroy()
    {
        for (int i = 0; i < _disposables.Count; i++)
        {
            _disposables[i].Dispose();
        }
        _disposables.Clear();
    }

    public void Add(IDisposable disposable)
    {
        _disposables.Add(disposable);
    }
}

// Simple wrapper for basic actions
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