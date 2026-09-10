namespace Ledge.App.Services;

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

/// <summary>Retargetable, velocity-preserving springs, active only while a value is moving.</summary>
public static class SpringMotion
{
    private sealed class Motion(double value, double target)
    {
        public double Value = value;
        public double Velocity;
        public double Target = target;
    }

    private static readonly Dictionary<(Animatable Owner, DependencyProperty Property), Motion> Active = new();
    private static TimeSpan? _lastFrame;

    public static void To(Animatable owner, DependencyProperty property, double target, bool alwaysAnimate = false)
    {
        if (!alwaysAnimate && !SystemParameters.ClientAreaAnimation)
        {
            Active.Remove((owner, property));
            owner.SetValue(property, target);
            UnsubscribeIfIdle();
            return;
        }
        if (Active.TryGetValue((owner, property), out var motion)) motion.Target = target;
        else
        {
            var value = (double)owner.GetValue(property);
            if (Math.Abs(value - target) < .001) return;
            if (Active.Count == 0) CompositionTarget.Rendering += Render;
            Active.Add((owner, property), new Motion(value, target));
        }
    }

    public static void Stop(Animatable owner)
    {
        foreach (var key in Active.Keys.Where(k => k.Owner == owner).ToArray()) Active.Remove(key);
        UnsubscribeIfIdle();
    }

    // Exact damped-spring integration remains stable at low frame rates.
    public static void Advance(ref double value, ref double velocity, double target, double seconds)
    {
        const double omega = 22, damping = .82;
        var decay = damping * omega;
        var frequency = omega * Math.Sqrt(1 - damping * damping);
        var displacement = value - target;
        var b = (velocity + decay * displacement) / frequency;
        var sin = Math.Sin(frequency * seconds);
        var cos = Math.Cos(frequency * seconds);
        var envelope = Math.Exp(-decay * seconds);
        var next = envelope * (displacement * cos + b * sin);
        velocity = envelope * ((b * frequency - decay * displacement) * cos - (displacement * frequency + decay * b) * sin);
        value = target + next;
    }

    private static void Render(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime;
        var dt = _lastFrame is { } last ? Math.Clamp((now - last).TotalSeconds, 0, .1) : 1.0 / 60;
        _lastFrame = now;
        foreach (var (key, motion) in Active.ToArray())
        {
            Advance(ref motion.Value, ref motion.Velocity, motion.Target, dt);
            var settled = Math.Abs(motion.Value - motion.Target) < .02 && Math.Abs(motion.Velocity) < .02;
            key.Owner.SetValue(key.Property, settled ? motion.Target : motion.Value);
            if (settled) Active.Remove(key);
        }
        UnsubscribeIfIdle();
    }

    private static void UnsubscribeIfIdle()
    {
        if (Active.Count != 0) return;
        CompositionTarget.Rendering -= Render;
        _lastFrame = null;
    }
}
