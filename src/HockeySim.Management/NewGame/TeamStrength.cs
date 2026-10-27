using HockeySim.Domain;

namespace HockeySim.Management.NewGame;

/// <summary>
/// A team's strength by group, from the players in its lineup slots. "Forwards" and "defence"
/// mean the line and pair slots, not natural position, because any skater can fill any slot.
/// </summary>
/// <remarks>
/// The team overall weights forwards 40%, defence 30% and the starting goalie 30%. The weighting
/// uses the unrounded means; only the displayed figures are rounded to whole numbers.
/// </remarks>
internal sealed record TeamStrength(double ForwardsMean, double DefenceMean, double GoalieOverall)
{
    private const double ForwardWeight = 0.4;
    private const double DefenceWeight = 0.3;
    private const double GoalieWeight = 0.3;

    public double OverallMean => (ForwardWeight * ForwardsMean) + (DefenceWeight * DefenceMean) + (GoalieWeight * GoalieOverall);

    public int Overall => Round(OverallMean);

    public int Forwards => Round(ForwardsMean);

    public int Defence => Round(DefenceMean);

    public int Goalies => Round(GoalieOverall);

    public static TeamStrength Create(Team team)
    {
        ArgumentNullException.ThrowIfNull(team);

        var lineup = team.Lineup;
        return new TeamStrength(
            lineup.ForwardLines.SelectMany(line => line.Players).Average(player => (double)player.Overall.Value),
            lineup.DefencePairs.SelectMany(pair => pair.Players).Average(player => (double)player.Overall.Value),
            lineup.StartingGoalie.Overall.Value);
    }

    /// <summary>
    /// Ranks every team by unrounded overall, highest first (ties keep league order), and buckets
    /// the ranks into quarters of the league. A generated league always has a spread, so the
    /// ranking, not a fixed rating threshold, decides each team's outlook.
    /// </summary>
    public static IReadOnlyList<TeamOutlook> Outlooks(IReadOnlyList<TeamStrength> strengths)
    {
        var quarter = Math.Max(1, strengths.Count / 4);
        var outlooks = new TeamOutlook[strengths.Count];
        var ranked = strengths
            .Select((strength, index) => (strength, index))
            .OrderByDescending(entry => entry.strength.OverallMean)
            .ThenBy(entry => entry.index)
            .ToList();

        for (var rank = 0; rank < ranked.Count; rank++)
        {
            outlooks[ranked[rank].index] = Math.Min(rank / quarter, (int)TeamOutlook.Rebuild) switch
            {
                0 => TeamOutlook.Contender,
                1 => TeamOutlook.PlayoffBubble,
                2 => TeamOutlook.Middle,
                _ => TeamOutlook.Rebuild,
            };
        }

        return outlooks;
    }

    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}