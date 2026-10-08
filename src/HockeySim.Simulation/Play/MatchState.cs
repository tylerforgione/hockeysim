using HockeySim.Simulation.Events;
using HockeySim.Simulation.Randomness;

namespace HockeySim.Simulation.Play;

/// <summary>
/// The state of one match being played that every part of the match engine reads and changes: the
/// two teams, the clock and score, the puck, a stoppage waiting for its faceoff, a delayed penalty,
/// and the play-by-play. It holds no play rules of its own; <see cref="MatchPlay"/> and the parts
/// it coordinates decide what happens.
/// </summary>
internal sealed class MatchState(MatchSide home, MatchSide away, PenaltyBox penaltyBox, ControlledRandom random)
{
    private readonly List<MatchEvent> _events = [];
    private OnIcePlayers? _onIce;

    public ControlledRandom Random { get; } = random;

    public MatchSide Home { get; } = home;

    public MatchSide Away { get; } = away;

    public PenaltyBox PenaltyBox { get; } = penaltyBox;

    public IReadOnlyList<MatchEvent> Events => _events;

    public int Period { get; set; }

    public int Clock { get; private set; }

    public int PlayingSeconds { get; private set; }

    public bool OpenIce { get; set; }

    /// <summary>
    /// Regular-season overtime starts from three skaters a side, and a penalty adds a skater to the
    /// other team rather than taking one away.
    /// </summary>
    public bool ThreeOnThreeBase { get; set; }

    public int HomeGoals { get; private set; }

    public int AwayGoals { get; private set; }

    /// <summary>The team with the puck.</summary>
    public MatchSide Possessor { get; set; } = home;

    /// <summary>Where the puck is, from the possessing team's side.</summary>
    public Zone Zone { get; set; }

    public bool Rush { get; set; }

    public bool Rebound { get; set; }

    public bool FaceoffPending { get; set; }

    /// <summary>The team in whose defensive zone the pending faceoff is taken, or null for centre ice.</summary>
    public MatchSide? FaceoffZoneOwner { get; set; }

    /// <summary>The team that has committed a delayed penalty, while play continues.</summary>
    public MatchSide? DelayedAgainst { get; set; }

    public List<CalledPenalty> DelayedPenalties { get; } = [];

    public TimeSpan Now => TimeSpan.FromSeconds(Clock);

    public MatchSide Opponent(MatchSide side) => side == Home ? Away : Home;

    public void StartPeriod(int period)
    {
        Period = period;
        Clock = 0;
    }

    public void Elapse(int seconds)
    {
        Home.Elapse(seconds);
        Away.Elapse(seconds);
        PenaltyBox.Elapse(seconds);
        Clock += seconds;
        PlayingSeconds += seconds;
    }

    public void CountGoal(MatchSide side)
    {
        if (side == Home)
        {
            HomeGoals++;
        }
        else
        {
            AwayGoals++;
        }
    }

    /// <summary>
    /// The players on the ice for the next event. Consecutive events share one instance until
    /// <see cref="ForgetOnIce"/> is called after someone changes.
    /// </summary>
    public OnIcePlayers OnIce() => _onIce ??= new OnIcePlayers(
        Home.OnIce.Select(slot => slot.Skater.Id),
        Home.IsGoaliePulled ? null : Home.Goalie.Id,
        Away.OnIce.Select(slot => slot.Skater.Id),
        Away.IsGoaliePulled ? null : Away.Goalie.Id);

    public void ForgetOnIce() => _onIce = null;

    public void Record(MatchEvent matchEvent) => _events.Add(matchEvent);

    public SkaterState Choose(MatchSide side, Func<OnIceSkater, double> weight) => ChooseSlot(side, weight).Skater;

    public OnIceSkater ChooseSlot(MatchSide side, Func<OnIceSkater, double> weight)
    {
        var onIce = side.OnIce;
        var weights = new double[onIce.Count];
        for (var index = 0; index < onIce.Count; index++)
        {
            weights[index] = Math.Max(0, weight(onIce[index]));
        }

        return onIce[Random.NextWeightedIndex(weights)];
    }
}