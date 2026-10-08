using HockeySim.Domain;
using HockeySim.Simulation.Events;

namespace HockeySim.Simulation.Tests;

/// <summary>
/// An independent statement of the NHL manpower rules, replayed over a match's play-by-play. From
/// the penalties and goals alone it works out how many skaters each team should have at every
/// event, who is in the box, and each team's power-play opportunities, so tests can check the
/// engine against the rules rather than against itself.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Penalty time runs on the game clock: two minutes for a minor, four for a double minor,
/// five for a major, ten for a misconduct. A game misconduct ejects the player.</item>
/// <item>A team serves at most two penalties that leave it short; a further one starts, in full,
/// when one of those ends.</item>
/// <item>Penalties on both teams at one stoppage are coincidental and leave neither short, except one
/// minor each at full strength with no other penalties, which plays four-on-four. A misconduct never
/// leaves a team short and starts once the player's other penalties are over.</item>
/// <item>In regulation and playoff overtime a team has five skaters less its penalties; in
/// regular-season overtime it has three, plus one for each penalty the opponent serves beyond its own.</item>
/// <item>A power-play goal ends the conceding team's running minor with the least time left, or the
/// current half of a double minor.</item>
/// <item>A goalie pulled during a delayed penalty adds one skater to the team on the ice.</item>
/// </list>
/// </remarks>
internal sealed class ManpowerReplay
{
    private const int MinorSeconds = 120;
    private const int RegulationSeconds = MatchResult.RegulationPeriodCount * 20 * 60;

    private readonly MatchResult _result;
    private readonly OvertimeFormat _overtime;
    private readonly List<Served> _box = [];
    private readonly HashSet<PlayerId> _leftMatch = [];
    private readonly Dictionary<TeamId, int> _powerPlayOpportunities = [];
    private readonly Dictionary<TeamId, long> _manpowerSeconds = [];
    private readonly List<Checkpoint> _checkpoints = [];
    private int _gameSeconds;

    // Regular-season overtime has begun: three skaters a side is the base.
    private bool _threeOnThree;

    public ManpowerReplay(MatchResult result, OvertimeFormat overtime = OvertimeFormat.RegularSeason)
    {
        _result = result;
        _overtime = overtime;
        _powerPlayOpportunities[Home] = 0;
        _powerPlayOpportunities[Away] = 0;
        _manpowerSeconds[Home] = 0;
        _manpowerSeconds[Away] = 0;
        Replay();
    }

    /// <summary>The state before each event, in play-by-play order.</summary>
    public IReadOnlyList<Checkpoint> Checkpoints => _checkpoints;

    private TeamId Home => _result.Home.TeamId;

    private TeamId Away => _result.Away.TeamId;

    public int PowerPlayOpportunities(TeamId teamId) => _powerPlayOpportunities[teamId];

    /// <summary>
    /// The seconds each skater the penalties allowed the team was on the ice, summed over the
    /// match: the team's time on ice, apart from any extra attacker.
    /// </summary>
    public long ManpowerSeconds(TeamId teamId) => _manpowerSeconds[teamId];

    public static int GameSeconds(MatchEvent matchEvent) =>
        ((matchEvent.Period - 1) * 20 * 60) + (int)matchEvent.TimeInPeriod.TotalSeconds;

    private void Replay()
    {
        var events = _result.Events;
        for (var index = 0; index < events.Count; index++)
        {
            var matchEvent = events[index];
            AdvanceTo(GameSeconds(matchEvent));
            if (_overtime == OvertimeFormat.RegularSeason && matchEvent.Period > MatchResult.RegulationPeriodCount && !_threeOnThree)
            {
                _threeOnThree = true;
                CountPowerPlays();
            }
            _checkpoints.Add(Snapshot(matchEvent));

            switch (matchEvent)
            {
                case PenaltyEvent:
                    // Penalties at the same stoppage are assessed together.
                    var batch = new List<PenaltyEvent>();
                    while (index < events.Count && events[index] is PenaltyEvent penalty && GameSeconds(penalty) == GameSeconds(matchEvent))
                    {
                        batch.Add(penalty);
                        if (batch.Count > 1)
                        {
                            _checkpoints.Add(Snapshot(penalty));
                        }

                        index++;
                    }

                    index--;
                    Assess(batch);
                    break;
                case InjuryEvent injury when !InjuryCatalogue.For(injury.Type).CanPlayThrough:
                    // A player who cannot play through an injury is out for the rest of the match.
                    _leftMatch.Add(injury.PlayerId);
                    break;
                case GoalEvent { Situation: GoalSituation.PowerPlay } goal:
                    EndMinorAfterPowerPlayGoal(Opponent(goal.TeamId));
                    break;
            }
        }

        AdvanceTo((int)_result.PlayingTime.TotalSeconds);
    }

    private Checkpoint Snapshot(MatchEvent matchEvent) => new(
        matchEvent,
        Manpower(Home),
        Manpower(Away),
        _box.Select(served => served.Player).Concat(_leftMatch).ToHashSet(),
        _box.Where(served => served.AffectsManpower).GroupBy(served => served.Team).ToDictionary(group => group.Key, group => group.Count()));

    private void Assess(List<PenaltyEvent> batch)
    {
        var coincidental = batch.Select(penalty => penalty.TeamId).Distinct().Count() > 1;
        var timed = batch.Where(penalty => penalty.Kind is PenaltyKind.Minor or PenaltyKind.DoubleMinor or PenaltyKind.Major).ToList();
        var fourOnFour = coincidental
            && !_threeOnThree
            && !_box.Exists(served => served.AffectsManpower)
            && timed.Count == 2
            && timed.All(penalty => penalty.Kind == PenaltyKind.Minor)
            && timed[0].TeamId != timed[1].TeamId;

        foreach (var penalty in batch)
        {
            switch (penalty.Kind)
            {
                case PenaltyKind.GameMisconduct:
                    _leftMatch.Add(penalty.PlayerId);
                    break;
                case PenaltyKind.PenaltyShot:
                    break;
                default:
                    _box.Add(new Served(
                        penalty.TeamId,
                        penalty.PlayerId,
                        penalty.Kind,
                        affectsManpower: penalty.Kind != PenaltyKind.Misconduct && (!coincidental || fourOnFour),
                        penalty.Minutes * 60));
                    break;
            }
        }

        StartWaiting();
        CountPowerPlays();
    }

    private void EndMinorAfterPowerPlayGoal(TeamId teamId)
    {
        var minor = _box
            .Where(served => served.Team == teamId && served.AffectsManpower && served.IsRunning
                && served.Kind is PenaltyKind.Minor or PenaltyKind.DoubleMinor)
            .OrderBy(served => served.Remaining > MinorSeconds ? served.Remaining - MinorSeconds : served.Remaining)
            .FirstOrDefault();
        if (minor is null)
        {
            return;
        }

        if (minor.Kind == PenaltyKind.DoubleMinor && minor.Remaining > MinorSeconds)
        {
            minor.Remaining = MinorSeconds;
            return;
        }

        _box.Remove(minor);
        StartWaiting();
        CountPowerPlays();
    }

    /// <summary>Runs the clock forward, ending penalties on time and starting any waiting.</summary>
    private void AdvanceTo(int gameSeconds)
    {
        while (_gameSeconds < gameSeconds)
        {
            var next = gameSeconds;
            foreach (var served in _box.Where(served => served.IsRunning))
            {
                next = Math.Min(next, _gameSeconds + served.Remaining);
            }

            // Overtime begins a new strength state.
            if (_gameSeconds < RegulationSeconds && next > RegulationSeconds)
            {
                next = RegulationSeconds;
            }

            if (_gameSeconds >= RegulationSeconds && _overtime == OvertimeFormat.RegularSeason && !_threeOnThree)
            {
                _threeOnThree = true;
                CountPowerPlays();
            }

            var seconds = next - _gameSeconds;
            _manpowerSeconds[Home] += (long)seconds * Manpower(Home);
            _manpowerSeconds[Away] += (long)seconds * Manpower(Away);
            foreach (var served in _box.Where(served => served.IsRunning))
            {
                served.Remaining -= seconds;
            }

            _gameSeconds = next;
            if (_box.RemoveAll(served => served.IsRunning && served.Remaining <= 0) > 0)
            {
                StartWaiting();
            }

            CountPowerPlays();
        }
    }

    private void StartWaiting()
    {
        foreach (var served in _box.Where(served => !served.IsRunning))
        {
            served.IsRunning = served.AffectsManpower
                ? _box.Count(other => other.Team == served.Team && other.AffectsManpower && other.IsRunning) < 2
                : served.Kind != PenaltyKind.Misconduct
                    || !_box.Exists(other => other != served && other.Player == served.Player && other.Kind != PenaltyKind.Misconduct);
        }
    }

    private void CountPowerPlays()
    {
        foreach (var (team, opponent) in new[] { (Home, Away), (Away, Home) })
        {
            if (Manpower(team) <= Manpower(opponent))
            {
                continue;
            }

            foreach (var served in _box.Where(served => served.Team == opponent && served.AffectsManpower && served.IsRunning && !served.Counted))
            {
                served.Counted = true;
                _powerPlayOpportunities[team]++;
            }
        }
    }

    private int Manpower(TeamId teamId)
    {
        var shorthanded = Shorthanded(teamId);
        return _threeOnThree
            ? 3 + Math.Max(0, Shorthanded(Opponent(teamId)) - shorthanded)
            : 5 - shorthanded;
    }

    private int Shorthanded(TeamId teamId) => _box.Count(served => served.Team == teamId && served.AffectsManpower && served.IsRunning);

    private TeamId Opponent(TeamId teamId) => teamId == Home ? Away : Home;

    /// <summary>The rules' view of the match just before an event.</summary>
    /// <param name="Unavailable">Skaters in the box, including those waiting to start a penalty, and ejected skaters.</param>
    /// <param name="ManpowerPenalties">Penalties, running or waiting, that leave each team short.</param>
    public sealed record Checkpoint(
        MatchEvent Event,
        int HomeSkaters,
        int AwaySkaters,
        IReadOnlySet<PlayerId> Unavailable,
        IReadOnlyDictionary<TeamId, int> ManpowerPenalties)
    {
        /// <summary>The skaters on the ice the rules call for, with an extra attacker for a pulled goalie.</summary>
        public StrengthState ExpectedStrength => new(
            HomeSkaters + (Event.OnIce.HomeGoalie is null ? 1 : 0),
            AwaySkaters + (Event.OnIce.AwayGoalie is null ? 1 : 0));

        public int ManpowerPenaltiesFor(TeamId teamId) => ManpowerPenalties.GetValueOrDefault(teamId);
    }

    private sealed class Served(TeamId team, PlayerId player, PenaltyKind kind, bool affectsManpower, int duration)
    {
        public TeamId Team { get; } = team;

        public PlayerId Player { get; } = player;

        public PenaltyKind Kind { get; } = kind;

        public bool AffectsManpower { get; } = affectsManpower;

        public int Remaining { get; set; } = duration;

        public bool IsRunning { get; set; }

        public bool Counted { get; set; }
    }
}