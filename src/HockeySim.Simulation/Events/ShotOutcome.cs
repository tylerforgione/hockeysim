namespace HockeySim.Simulation.Events;

/// <summary>What became of a shot attempt that did not score.</summary>
public enum ShotOutcome
{
    /// <summary>On goal and stopped by the goalie.</summary>
    Saved,

    /// <summary>Wide or high of the net.</summary>
    Missed,

    /// <summary>Stopped by a defending skater before reaching the net.</summary>
    Blocked,
}