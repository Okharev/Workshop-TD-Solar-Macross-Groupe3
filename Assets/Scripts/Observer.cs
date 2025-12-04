using System;

public class Observer<TSource, TResult> : IDisposable
{
    private Action<TResult> _onNext;
    private Func<TSource, bool> _predicate;
    private Func<TSource, TResult> _selector;
    private IReadOnlyReactiveProperty<TSource> _source;

    public void Dispose()
    {
        if (_source != null)
        {
            _source.OnValueChanged -= OnSourceChanged;

            // Clear references
            _source = null;
            _selector = null;
            _predicate = null;
            _onNext = null;

            // Return to Pool
            ObserverPool<TSource, TResult>.Return(this);
        }
    }

    public void ConfigureSelect(IReadOnlyReactiveProperty<TSource> source, Func<TSource, TResult> selector,
        Action<TResult> onNext)
    {
        _source = source;
        _selector = selector;
        _onNext = onNext;
        Connect();
    }

    public void ConfigureWhere(IReadOnlyReactiveProperty<TSource> source, Func<TSource, bool> predicate,
        Action<TResult> onNext)
    {
        _source = source;
        _predicate = predicate;
        _onNext = onNext;
        Connect();
    }

    private void Connect()
    {
        _source.OnValueChanged += OnSourceChanged;
        OnSourceChanged(_source.Value);
    }

    private void OnSourceChanged(TSource value)
    {
        // 1. Handle Transformation
        if (_selector != null)
        {
            _onNext(_selector(value));
            return;
        }

        // 2. Handle Filter
        if (_predicate != null)
            if (_predicate(value))
                // Safe cast because TSource == TResult in a Where clause
                _onNext((TResult)(object)value);
    }
}