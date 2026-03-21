using BlazorMotion.Models;

namespace BlazorMotion.Engine;

/// <summary>
/// Semi-implicit Euler spring physics driver for numeric properties.
/// Automatically subdivides each frame to maintain numerical stability for
/// high-stiffness / high-damping configurations.
/// </summary>
internal sealed class SpringDriver : IAnimationDriver
{
    private readonly double _target;
    private readonly double _k;        // stiffness
    private readonly double _d;        // damping
    private readonly double _m;        // mass
    private readonly double _restSpeed;
    private readonly double _restDelta;
    private readonly double _delayMs;
    private readonly double _maxSubDt;
    private readonly Action<double> _apply;

    private double _pos;
    private double _vel;
    private double _lastTs = -1;
    private double _startTs = -1;
    private bool _cancelled;

    public SpringDriver(double from, double to, TransitionConfig config, Action<double> apply)
    {
        _pos = from;
        _target = to;
        _k = config.Stiffness;
        _d = config.Damping;
        _m = config.Mass;
        _vel = config.Velocity;
        _restSpeed = config.RestSpeed;
        _restDelta = config.RestDelta;
        _delayMs = config.Delay * 1000;
        _apply = apply;

        // Compute a maximum sub-step size that keeps semi-implicit Euler stable
        _maxSubDt = Math.Max(0.001, Math.Min(
            _d > 0 ? 1.8 / _d : 1.0,
            _k > 0 ? 0.9 / Math.Sqrt(_k) : 1.0));
    }

    public bool Tick(double timestamp)
    {
        if (_cancelled) { _apply(_target); return true; }

        if (_startTs < 0) _startTs = timestamp;
        if (timestamp - _startTs < _delayMs) { _apply(_pos); return false; }

        if (_lastTs < 0) _lastTs = timestamp;

        double dt = Math.Min((timestamp - _lastTs) / 1000.0, 0.064);
        _lastTs = timestamp;

        int subSteps = Math.Max(1, (int)Math.Ceiling(dt / _maxSubDt));
        double subDt = dt / subSteps;
        for (int i = 0; i < subSteps; i++)
        {
            double springF = -_k * (_pos - _target);
            double dampF = -_d * _vel;
            _vel += (springF + dampF) / _m * subDt;
            _pos += _vel * subDt;
        }

        _apply(_pos);

        if (Math.Abs(_vel) < _restSpeed && Math.Abs(_pos - _target) < _restDelta)
        {
            _apply(_target);
            return true;
        }
        return false;
    }

    public void Cancel() => _cancelled = true;
}
