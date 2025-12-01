using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ReactiveProperty<T>
{
    // Protected so the Editor Drawer can find it
    [SerializeField] protected T _value;

    public ReactiveProperty(T initialValue = default)
    {
        _value = initialValue;
    }

    public T Value
    {
        get => _value;
        set
        {
            // Standard check: don't fire if nothing changed
            if (EqualityComparer<T>.Default.Equals(_value, value)) return;
            _value = value;
            OnValueChanged?.Invoke(_value);
        }
    }

    public event Action<T> OnValueChanged;

    // FIX: Allows the Editor to trigger the update even if _value was already set by Serialization
    public void Notify()
    {
        OnValueChanged?.Invoke(_value);
    }

    public static implicit operator T(ReactiveProperty<T> p)
    {
        return p.Value;
    }
}

[Serializable]
public class ReactiveInt : ReactiveProperty<int>
{
    public ReactiveInt(int v) : base(v)
    {
    }
}

[Serializable]
public class ReactiveFloat : ReactiveProperty<float>
{
    public ReactiveFloat(float v) : base(v)
    {
    }
}

[Serializable]
public class ReactiveBool : ReactiveProperty<bool>
{
    public ReactiveBool(bool v) : base(v)
    {
    }
}