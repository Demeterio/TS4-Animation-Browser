namespace TS4AnimationBrowser.App;

public sealed class PlaybackController
{
    public double DurationSeconds { get; private set; }
    public double PositionSeconds { get; private set; }
    public double Speed { get; set; } = 1.0;
    public bool Loop { get; set; }
    public bool IsPlaying { get; private set; }
    public bool HasAnimation => DurationSeconds > 0;

    public void Load(double durationSeconds)
    {
        DurationSeconds = Math.Max(0, durationSeconds);
        PositionSeconds = 0;
        IsPlaying = false;
    }

    public void Clear()
    {
        DurationSeconds = 0;
        PositionSeconds = 0;
        IsPlaying = false;
    }

    public void Play()
    {
        if (HasAnimation)
            IsPlaying = true;
    }

    public void Pause() => IsPlaying = false;

    public void Stop()
    {
        IsPlaying = false;
        PositionSeconds = 0;
    }

    public void Seek(double seconds)
    {
        PositionSeconds = Math.Clamp(seconds, 0, DurationSeconds);
    }

    public void Tick(double elapsedSeconds)
    {
        if (!IsPlaying || !HasAnimation || elapsedSeconds <= 0)
            return;

        PositionSeconds += elapsedSeconds * Speed;
        if (PositionSeconds < DurationSeconds)
            return;

        if (Loop)
            PositionSeconds %= DurationSeconds;
        else
        {
            PositionSeconds = 0;
            IsPlaying = false;
        }
    }
}
