using BlazorMotion.Interop;
using Microsoft.JSInterop;

namespace BlazorMotion.Services;

/// <summary>
/// A reactive numeric value whose changes can be observed and linked to animations.
/// Analogous to Framer Motion's <c>MotionValue&lt;T&gt;</c>.
///
/// Create via <see cref="MotionValueFactory"/> or resolve from DI.
/// </summary>
public class MotionValue<T> : IAsyncDisposable where T : struct
{
    private readonly string _id;
    private readonly MotionInterop? _interop;
    private T _value;
    private readonly List<Func<T, Task>> _subscribers = new();

    internal MotionValue(string id, T initial, MotionInterop? interop)
    {
        _id      = id;
        _value   = initial;
        _interop = interop;
    }

    // ── Value access ──────────────────────────────────────────────────────────

    public T Value
    {
        get => _value;
        set => _ = SetAsync(value);
    }

    /// <summary>Update the value and notify all subscribers.</summary>
    public async Task SetAsync(T value)
    {
        _value = value;
        foreach (var sub in _subscribers)
            await sub(value);
        if (_interop != null && value is double d)
            await _interop.SetMotionValueAsync(_id, d);
    }

    // ── Subscriptions ─────────────────────────────────────────────────────────

    /// <summary>Subscribe to value changes. Returns an unsubscribe action.</summary>
    public IDisposable Subscribe(Func<T, Task> callback)
    {
        _subscribers.Add(callback);
        return new Subscription(() => _subscribers.Remove(callback));
    }

    /// <summary>Synchronous convenience overload.</summary>
    public IDisposable Subscribe(Action<T> callback)
        => Subscribe(v => { callback(v); return Task.CompletedTask; });

    // ── Transforms ────────────────────────────────────────────────────────────

    /// <summary>
    /// Create a derived MotionValue that applies a transformation function.
    /// Analogous to Framer Motion's <c>useTransform</c>.
    /// </summary>
    public MotionValue<TOut> Transform<TOut>(Func<T, TOut> fn) where TOut : struct
    {
        var derived = new MotionValue<TOut>($"{_id}_t", fn(_value), null);
        Subscribe(async v => await derived.SetAsync(fn(v)));
        return derived;
    }

    /// <summary>
    /// Map from an input range to an output range using linear interpolation.
    /// e.g. progress.Transform([0,1], [0, 100]) → pixel offset.
    /// </summary>
    public MotionValue<double> Transform(double[] inputRange, double[] outputRange)
    {
        if (inputRange.Length != outputRange.Length)
            throw new ArgumentException("inputRange and outputRange must have the same length.");

        double Map(T v)
        {
            double x = Convert.ToDouble(v);
            for (int i = 0; i < inputRange.Length - 1; i++)
            {
                if (x >= inputRange[i] && x <= inputRange[i + 1])
                {
                    double t = (x - inputRange[i]) / (inputRange[i + 1] - inputRange[i]);
                    return outputRange[i] + t * (outputRange[i + 1] - outputRange[i]);
                }
            }
            return x < inputRange[0] ? outputRange[0] : outputRange[^1];
        }

        var derived = new MotionValue<double>($"{_id}_tr", Map(_value), null);
        Subscribe(async v => await derived.SetAsync(Map(v)));
        return derived;
    }

    public async ValueTask DisposeAsync()
    {
        _subscribers.Clear();
        if (_interop != null)
            await _interop.DestroyMotionValueAsync(_id);
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _dispose;
        public Subscription(Action dispose) => _dispose = dispose;
        public void Dispose() => _dispose();
    }
}

/// <summary>Factory helper for creating MotionValues without DI.</summary>
public static class MotionValueFactory
{
    /// <summary>Create a MotionValue that is purely C#-side (no JS sync).</summary>
    public static MotionValue<T> Create<T>(T initial) where T : struct
        => new($"mv_{Guid.NewGuid():N}", initial, null);
}
