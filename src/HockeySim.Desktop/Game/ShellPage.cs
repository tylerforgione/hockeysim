namespace HockeySim.Desktop.Game;

/// <summary>
/// A page the shell can show. Each belongs to one <see cref="ShellSection"/>; the team and league
/// schedules are the same page opened from different sections.
/// </summary>
public enum ShellPage
{
    Home,
    Inbox,
    Roster,
    Lines,
    TeamSchedule,
    TeamStatistics,
    Injuries,
    Standings,
    Teams,
    LeagueLeaders,
    PlayoffPicture,
    LeagueSchedule,
    PlayerStatistics,
    Staff,
    Transactions,
}

/// <summary>A group of pages shown together as tabs under the team banner.</summary>
public enum ShellSection
{
    Home,
    Team,
    League,
    Stats,
    Club,
    Inbox,
}