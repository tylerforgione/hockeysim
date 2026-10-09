using HockeySim.Domain;

namespace HockeySim.Management.Scheduling;

/// <summary>
/// Builds rounds in which every team in a group plays once.
/// </summary>
internal static class RoundRobin
{
    /// <summary>
    /// Pairs every team with every other team once, using the circle method: one team stays
    /// fixed while the rest rotate, so each round is a perfect matching.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<TeamPair>> Create(IReadOnlyList<TeamId> teams)
    {
        var rotating = teams.Skip(1).ToList();
        var rounds = new List<IReadOnlyList<TeamPair>>(rotating.Count);

        for (var round = 0; round < rotating.Count; round++)
        {
            var order = new List<TeamId>(teams.Count) { teams[0] };
            order.AddRange(rotating.Skip(round).Concat(rotating.Take(round)));

            rounds.Add(Enumerable.Range(0, order.Count / 2)
                .Select(index => TeamPair.Create(order[index], order[order.Count - 1 - index]))
                .ToList());
        }

        return rounds;
    }

    /// <summary>
    /// Merges same-numbered rounds from disjoint groups of teams into league-wide rounds.
    /// </summary>
    public static IEnumerable<IReadOnlyList<TeamPair>> Combine(
        IEnumerable<IReadOnlyList<IReadOnlyList<TeamPair>>> groups)
    {
        var groupList = groups.ToList();
        return Enumerable.Range(0, groupList[0].Count)
            .Select(round => groupList.SelectMany(group => group[round]).ToList());
    }
}