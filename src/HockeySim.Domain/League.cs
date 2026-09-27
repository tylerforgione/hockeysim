using System.Collections.ObjectModel;

namespace HockeySim.Domain;

public sealed class League
{
    public const int RequiredConferenceCount = 2;

    private readonly ReadOnlyCollection<Conference> _conferences;
    private readonly ReadOnlyCollection<Team> _teams;

    public League(int seasonYear, IEnumerable<Conference> conferences)
    {
        if (seasonYear < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(seasonYear), "Season year must be positive.");
        }

        ArgumentNullException.ThrowIfNull(conferences);
        var conferenceList = conferences.ToList();
        if (conferenceList.Count != RequiredConferenceCount)
        {
            throw new ArgumentException(
                $"A league must contain exactly {RequiredConferenceCount} conferences.",
                nameof(conferences));
        }

        if (conferenceList.Select(conference => conference.Name).Distinct(StringComparer.Ordinal).Count()
            != conferenceList.Count)
        {
            throw new ArgumentException("Conference names must be unique.", nameof(conferences));
        }

        var teamList = conferenceList
            .SelectMany(conference => conference.Divisions)
            .SelectMany(division => division.Teams)
            .ToList();

        if (teamList.Select(team => team.Id).Distinct().Count() != teamList.Count)
        {
            throw new ArgumentException("Team identities must be unique across the league.", nameof(conferences));
        }

        if (teamList.Select(team => team.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            != teamList.Count)
        {
            throw new ArgumentException("Team names must be unique across the league.", nameof(conferences));
        }

        SeasonYear = seasonYear;
        _conferences = conferenceList.AsReadOnly();
        _teams = teamList.AsReadOnly();
    }

    public int SeasonYear { get; }

    public IReadOnlyList<Conference> Conferences => _conferences;

    public IReadOnlyList<Team> Teams => _teams;
}