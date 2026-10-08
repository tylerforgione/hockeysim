using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Simulation;
using HockeySim.Simulation.Events;

namespace HockeySim.Management.Tests;

/// <summary>
/// League-wide averages of a played season, measured the way NHL league averages are reported:
/// per team per game unless the name says per match.
/// </summary>
internal sealed class LeagueMeasurements
{
    private const int DressedSkaters = (3 * Lineup.RequiredForwardLineCount) + (2 * Lineup.RequiredDefencePairCount);

    /// <param name="playersOut">Rostered players who could not play, summed over both teams of every match.</param>
    public LeagueMeasurements(IReadOnlyList<MatchResult> results, IReadOnlyList<TeamRecordSnapshot> records, int playersOut)
    {
        if (results.Count == 0)
        {
            throw new ArgumentException("A measurement needs at least one match.", nameof(results));
        }

        var sides = results.SelectMany(result => new[] { result.Home, result.Away }).ToList();
        var skaters = sides.SelectMany(side => side.Skaters).ToList();
        var events = results.SelectMany(result => result.Events).ToList();
        double PerTeam(double total) => total / sides.Count;
        double PerMatch(double total) => total / results.Count;
        double ShareOfMatches(MatchDecision decision) => results.Count(result => result.Decision == decision) / (double)results.Count;

        Goals = PerTeam(sides.Sum(side => side.Score));
        Shots = PerTeam(sides.Sum(side => side.Shots));

        // Every unblocked or blocked attempt and every goal, as in NHL shot attempts (Corsi);
        // penalty shots are not attempts in the run of play.
        ShotAttempts = PerTeam(
            events.OfType<ShotAttemptEvent>().Count(shot => !shot.Context.IsPenaltyShot)
            + events.OfType<GoalEvent>().Count(goal => !goal.Context.IsPenaltyShot));
        ExpectedGoals = PerTeam(skaters.Sum(skater => skater.ExpectedGoals));
        SavePercentage = sides.Sum(side => side.Goalie.Saves) / (double)sides.Sum(side => side.Goalie.ShotsAgainst);

        RegulationShare = ShareOfMatches(MatchDecision.Regulation);
        OvertimeShare = ShareOfMatches(MatchDecision.Overtime);
        ShootoutShare = ShareOfMatches(MatchDecision.Shootout);

        PowerPlayOpportunities = PerTeam(sides.Sum(side => side.PowerPlayOpportunities));
        PowerPlayPercentage = sides.Sum(side => side.PowerPlayGoals) / (double)sides.Sum(side => side.PowerPlayOpportunities);
        PenaltyMinutes = PerTeam(skaters.Sum(skater => skater.PenaltyMinutes));

        // Each fight is a fighting major to each of the two fighters.
        FightsPerMatch = PerMatch(events.OfType<PenaltyEvent>().Count(penalty => penalty.Infraction == Infraction.Fighting) / 2.0);

        Hits = PerTeam(skaters.Sum(skater => skater.Hits));
        BlockedShots = PerTeam(skaters.Sum(skater => skater.BlockedShots));
        Takeaways = PerTeam(skaters.Sum(skater => skater.Takeaways));
        Giveaways = PerTeam(skaters.Sum(skater => skater.Giveaways));

        var faceoffs = events.OfType<FaceoffEvent>().ToList();
        FaceoffsPerMatch = PerMatch(faceoffs.Count);
        CentreIceFaceoffShare = faceoffs.Count(faceoff => faceoff.DefendingZoneTeamId is null) / (double)faceoffs.Count;

        EmptyNetGoalsPerMatch = PerMatch(skaters.Sum(skater => skater.EmptyNetGoals));

        // Skaters are listed in lineup order: the forward lines, then the defence pairs. Only full
        // lineups are measured, since a skater who leaves a match injured leaves a gap the other
        // groups fill.
        var fullSides = sides.Where(side => side.Skaters.Count == DressedSkaters).ToList();
        ForwardLineTimeShares = TimeShares(fullSides, first: 0, groupSize: 3, groupCount: Lineup.RequiredForwardLineCount);
        DefencePairTimeShares = TimeShares(fullSides, first: 3 * Lineup.RequiredForwardLineCount, groupSize: 2, groupCount: Lineup.RequiredDefencePairCount);

        // An injury that cannot be played through costs the matches the team plays before the
        // player's return date; a team plays about every other day.
        var injuries = results.SelectMany(result => result.Injuries).ToList();
        var outInjuries = injuries.Where(injury => !InjuryCatalogue.For(injury.Type).CanPlayThrough).ToList();
        InjuriesMissingMatches = PerTeam(outInjuries.Count);
        PlayThroughInjuries = PerTeam(injuries.Count - outInjuries.Count);
        MeanRecoveryDays = outInjuries.Count == 0 ? 0 : outInjuries.Average(injury => injury.RecoveryDays);
        ManGamesLost = PerTeam(playersOut);

        var points = records.Select(record => (double)record.Points).ToList();
        var meanPoints = points.Average();
        PointsStandardDeviation = Math.Sqrt(points.Average(value => (value - meanPoints) * (value - meanPoints)));
        FewestPoints = points.Min();
        MostPoints = points.Max();
    }

    public double Goals { get; }

    public double Shots { get; }

    public double ShotAttempts { get; }

    public double ExpectedGoals { get; }

    /// <summary>Goalies' saves over shots against, so empty-net goals are excluded.</summary>
    public double SavePercentage { get; }

    public double RegulationShare { get; }

    public double OvertimeShare { get; }

    public double ShootoutShare { get; }

    public double PowerPlayOpportunities { get; }

    public double PowerPlayPercentage { get; }

    public double PenaltyMinutes { get; }

    public double FightsPerMatch { get; }

    public double Hits { get; }

    public double BlockedShots { get; }

    public double Takeaways { get; }

    public double Giveaways { get; }

    public double FaceoffsPerMatch { get; }

    public double CentreIceFaceoffShare { get; }

    public double EmptyNetGoalsPerMatch { get; }

    /// <summary>Each forward line's share of the forwards' time on ice, top line first.</summary>
    public IReadOnlyList<double> ForwardLineTimeShares { get; }

    /// <summary>Each defence pair's share of the defence's time on ice, top pair first.</summary>
    public IReadOnlyList<double> DefencePairTimeShares { get; }

    /// <summary>The spread of the teams' points in the final standings.</summary>
    /// <summary>Injuries that cannot be played through, per team per game.</summary>
    public double InjuriesMissingMatches { get; }

    /// <summary>Injuries played through, per team per game.</summary>
    public double PlayThroughInjuries { get; }

    /// <summary>The mean recovery time of injuries that cannot be played through, in league days.</summary>
    public double MeanRecoveryDays { get; }

    /// <summary>Rostered players out injured, per team per game: man-games lost.</summary>
    public double ManGamesLost { get; }

    public double PointsStandardDeviation { get; }

    public double FewestPoints { get; }

    public double MostPoints { get; }

    private static List<double> TimeShares(List<MatchTeamResult> sides, int first, int groupSize, int groupCount)
    {
        var seconds = Enumerable.Range(0, groupCount)
            .Select(group => sides.Sum(side => side.Skaters
                .Skip(first + (group * groupSize))
                .Take(groupSize)
                .Sum(skater => skater.TimeOnIce.TotalSeconds)))
            .ToList();
        var total = seconds.Sum();
        return seconds.Select(value => value / total).ToList();
    }
}