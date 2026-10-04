using System.Collections.ObjectModel;

namespace HockeySim.Domain;

public sealed class Match
{
    public Match(
        Team home,
        Team away
    )
    {
        ArgumentNullException.ThrowIfNull(home);
        ArgumentNullException.ThrowIfNull(away);

        if (home.Id == away.Id)
        {
            throw new ArgumentException("A team cannot play a match against itself.");
        }

        Home = home;
        Away = away;
    }

    public Team Home { get; }

    public Team Away { get; }
}