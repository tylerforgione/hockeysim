namespace HockeySim.Management.NewGame;

/// <summary>
/// How a team is expected to fare, from where its overall ranks in the league: the top quarter
/// of teams are contenders, then the playoff bubble, the middle, and the rebuilding teams.
/// </summary>
public enum TeamOutlook
{
    Contender,
    PlayoffBubble,
    Middle,
    Rebuild,
}