using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ReactiveProperty<T> : IReadOnlyReactiveProperty<T>
{
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
            if (EqualityComparer<T>.Default.Equals(_value, value)) return;
            _value = value;
            OnValueChanged?.Invoke(_value);
        }
    }

    public event Action<T> OnValueChanged;

    public void Notify()
    {
        OnValueChanged?.Invoke(_value);
    }

    public static implicit operator T(ReactiveProperty<T> p)
    {
        return p.Value;
    }

    public override string ToString()
    {
        return _value != null ? _value.ToString() : "null";
    }
}

public interface IReadOnlyReactiveProperty<out T>
{
    T Value { get; }
    event Action<T> OnValueChanged;
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