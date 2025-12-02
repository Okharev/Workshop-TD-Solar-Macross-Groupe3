using System.Collections.Generic;

public static class ObserverPool<TSource, TResult>
{
    private static readonly Stack<Observer<TSource, TResult>> _pool = new();

    public static Observer<TSource, TResult> Get()
    {
        return _pool.Count > 0 ? _pool.Pop() : new Observer<TSource, TResult>();
    }

    public static void Return(Observer<TSource, TResult> observer)
    {
        _pool.Push(observer);
    }
}