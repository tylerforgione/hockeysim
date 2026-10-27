using System.Collections.ObjectModel;

using HockeySim.Domain;
using HockeySim.Management.Scheduling;

namespace HockeySim.Management.NewGame;

/// <summary>
/// Describes the league a new game would generate for a season year and random state, so a
/// caller can show it before the game is created.
/// </summary>
public sealed class NewGamePreviewSnapshot
{
    private readonly ReadOnlyCollection<NewGamePreviewConferenceSnapshot> _conferences;
    private readonly ReadOnlyCollection<NewGamePreviewTeamSnapshot> _teams;

    private NewGamePreviewSnapshot(
        int seasonYear,
        IReadOnlyList<NewGamePreviewConferenceSnapshot> conferences)
    {
        SeasonYear = seasonYear;
        _conferences = new ReadOnlyCollection<NewGamePreviewConferenceSnapshot>(conferences.ToList());
        _teams = new ReadOnlyCollection<NewGamePreviewTeamSnapshot>(
            conferences.SelectMany(conference => conference.Divisions).SelectMany(division => division.Teams).ToList());
    }

    public int SeasonYear { get; }

    /// <summary>Conferences, divisions and teams, in league order.</summary>
    public IReadOnlyList<NewGamePreviewConferenceSnapshot> Conferences => _conferences;

    /// <summary>Every team, in league order.</summary>
    public IReadOnlyList<NewGamePreviewTeamSnapshot> Teams => _teams;

    internal static NewGamePreviewSnapshot Create(League league)
    {
        var openingDay = ScheduleGenerator.OpeningDay(league.SeasonYear);
        var strengths = league.Teams.ToDictionary(team => team.Id, TeamStrength.Create);
        var outlooks = TeamStrength.Outlooks(league.Teams.Select(team => strengths[team.Id]).ToList());
        var outlookById = league.Teams
            .Select((team, index) => (team.Id, Outlook: outlooks[index]))
            .ToDictionary(entry => entry.Id, entry => entry.Outlook);

        var conferences = league.Conferences.Select(conference =>
            new NewGamePreviewConferenceSnapshot(
                conference.Name,
                conference.Divisions.Select(division =>
                    new NewGamePreviewDivisionSnapshot(
                        division.Name,
                        division.Teams.Select(team => NewGamePreviewTeamSnapshot.Create(
                            team,
                            division.Name,
                            conference.Name,
                            strengths[team.Id],
                            outlookById[team.Id],
                            openingDay)).ToList())).ToList())).ToList();

        return new NewGamePreviewSnapshot(league.SeasonYear, conferences);
    }
}

public sealed class NewGamePreviewConferenceSnapshot
{
    private readonly ReadOnlyCollection<NewGamePreviewDivisionSnapshot> _divisions;

    internal NewGamePreviewConferenceSnapshot(string name, IReadOnlyList<NewGamePreviewDivisionSnapshot> divisions)
    {
        Name = name;
        _divisions = new ReadOnlyCollection<NewGamePreviewDivisionSnapshot>(divisions.ToList());
    }

    public string Name { get; }

    public IReadOnlyList<NewGamePreviewDivisionSnapshot> Divisions => _divisions;
}

public sealed class NewGamePreviewDivisionSnapshot
{
    private readonly ReadOnlyCollection<NewGamePreviewTeamSnapshot> _teams;

    internal NewGamePreviewDivisionSnapshot(string name, IReadOnlyList<NewGamePreviewTeamSnapshot> teams)
    {
        Name = name;
        _teams = new ReadOnlyCollection<NewGamePreviewTeamSnapshot>(teams.ToList());
    }

    public string Name { get; }

    public IReadOnlyList<NewGamePreviewTeamSnapshot> Teams => _teams;
}

/// <summary>
/// A team as a new game would open: its strength by group (whole numbers, see
/// <see cref="TeamStrength"/>), its outlook, and the players to know.
/// </summary>
public sealed class NewGamePreviewTeamSnapshot
{
    private readonly ReadOnlyCollection<NewGamePreviewPlayerSnapshot> _keyPlayers;

    private NewGamePreviewTeamSnapshot(
        string name,
        TeamColours colours,
        string divisionName,
        string conferenceName,
        TeamStrength strength,
        TeamOutlook outlook,
        double averageAge,
        IReadOnlyList<NewGamePreviewPlayerSnapshot> keyPlayers)
    {
        Name = name;
        Colours = colours;
        DivisionName = divisionName;
        ConferenceName = conferenceName;
        Overall = strength.Overall;
        Forwards = strength.Forwards;
        Defence = strength.Defence;
        Goalies = strength.Goalies;
        Outlook = outlook;
        AverageAge = averageAge;
        _keyPlayers = new ReadOnlyCollection<NewGamePreviewPlayerSnapshot>(keyPlayers.ToList());
    }

    public string Name { get; }

    public TeamColours Colours { get; }

    public string DivisionName { get; }

    public string ConferenceName { get; }

    /// <summary>The team overall: 40% forwards, 30% defence, 30% starting goalie.</summary>
    public int Overall { get; }

    /// <summary>Mean overall of the 12 dressed forwards.</summary>
    public int Forwards { get; }

    /// <summary>Mean overall of the 6 dressed defence.</summary>
    public int Defence { get; }

    /// <summary>Overall of the starting goalie.</summary>
    public int Goalies { get; }

    public TeamOutlook Outlook { get; }

    /// <summary>Mean age of the whole roster on opening day, to one decimal place.</summary>
    public double AverageAge { get; }

    /// <summary>
    /// The best centre, the best wing, the two best defence and the starting goalie, in that
    /// order. Best means highest overall across the whole roster; ties go to roster order.
    /// </summary>
    public IReadOnlyList<NewGamePreviewPlayerSnapshot> KeyPlayers => _keyPlayers;

    internal static NewGamePreviewTeamSnapshot Create(
        Team team,
        string divisionName,
        string conferenceName,
        TeamStrength strength,
        TeamOutlook outlook,
        DateOnly openingDay)
    {
        IEnumerable<Player> Best(Position position, int count) =>
            team.Roster
                .Where(player => player.Position == position)
                .OrderByDescending(player => player.Overall.Value)
                .Take(count);

        var keyPlayers = Best(Position.Centre, 1)
            .Concat(Best(Position.Wing, 1))
            .Concat(Best(Position.Defence, 2))
            .Append(team.Lineup.StartingGoalie)
            .Select(NewGamePreviewPlayerSnapshot.Create)
            .ToList();

        var averageAge = Math.Round(
            team.Roster.Average(player => (double)player.AgeOn(openingDay)),
            1,
            MidpointRounding.AwayFromZero);

        return new NewGamePreviewTeamSnapshot(
            team.Name,
            team.Colours,
            divisionName,
            conferenceName,
            strength,
            outlook,
            averageAge,
            keyPlayers);
    }
}

public sealed record NewGamePreviewPlayerSnapshot(
    Position Position,
    string FirstName,
    string LastName,
    int Number,
    int Overall)
{
    internal static NewGamePreviewPlayerSnapshot Create(Player player) =>
        new(player.Position, player.FirstName, player.LastName, player.Number, player.Overall.Value);
}