using System.Globalization;

using HockeySim.Desktop.Playoffs;
using HockeySim.Desktop.Schedule;
using HockeySim.Domain;
using HockeySim.Management.Saves;

namespace HockeySim.Desktop.Saves;

/// <summary>
/// One saved game in a list of saves to choose from, with what its header says about the team.
/// </summary>
public sealed class SavedGameRowViewModel
{
    private const string Unavailable = "—";

    public SavedGameRowViewModel(SavedGameSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        Name = summary.Name;
        SavedAtLabel = summary.SavedAt.ToLocalTime().ToString("MMM d, yyyy · h:mm tt", CultureInfo.CurrentCulture);
        Details = summary.Details;
        if (summary.Details is { } details)
        {
            TeamName = details.TeamName;
            PrimaryColour = Colour.FromHex(details.PrimaryColour);
            SecondaryColour = Colour.FromHex(details.SecondaryColour);
            DateLabel = InGameDate(details.CurrentDate);
            RecordLabel = string.Create(CultureInfo.CurrentCulture, $"{details.Wins}-{details.Losses}-{details.OvertimeLosses}");
            StandingLabel = Standing(details);
        }
        else
        {
            TeamName = "Can't be read";
            DateLabel = Unavailable;
            RecordLabel = Unavailable;
            StandingLabel = Unavailable;
        }
    }

    /// <summary>What the save's header says about the team, or null when it cannot be read.</summary>
    public SavedGameDetails? Details { get; }

    public bool HasDetails => Details is not null;

    /// <summary>The team's name, or a note that the save cannot be read.</summary>
    public string TeamName { get; }

    public Colour? PrimaryColour { get; }

    public Colour? SecondaryColour { get; }

    public string DateLabel { get; }

    /// <summary>Wins, regulation losses, and overtime and shootout losses, as the team banner counts them.</summary>
    public string RecordLabel { get; }

    public string StandingLabel { get; }

    public SaveName Name { get; }

    public string DisplayName => Name.Value;

    public string SavedAtLabel { get; }

    /// <summary>Formats a season date as the saved games show it, such as "Oct 9, 2026".</summary>
    public static string InGameDate(DateOnly date) => date.ToString("MMM d, yyyy", CultureInfo.CurrentCulture);

    /// <summary>
    /// Where the team stands: preseason, the division rank through the regular season, and in the
    /// playoffs the round it is in, or that it was eliminated or won. A team that missed the
    /// playoffs keeps its final division rank.
    /// </summary>
    public static string Standing(SavedGameDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        if (details.Phase == SeasonPhase.Preseason)
        {
            return "Preseason";
        }

        return details.Playoffs switch
        {
            SavedPlayoffProgress.Alive => details.PlayoffRound is { } round ? PlayoffDisplay.RoundName(round) : "Playoffs",
            SavedPlayoffProgress.Eliminated => "Eliminated",
            SavedPlayoffProgress.Champion => "Champions",
            _ => string.Create(CultureInfo.CurrentCulture, $"{MatchDisplay.Ordinal(details.DivisionRank)} {details.DivisionName}"),
        };
    }

    /// <summary>
    /// Lists the library's saves, or reports why they cannot be listed.
    /// </summary>
    public static IReadOnlyList<SavedGameRowViewModel> List(ISavedGameLibrary saves, out string? error)
    {
        try
        {
            error = null;
            return saves.List().Select(summary => new SavedGameRowViewModel(summary)).ToList();
        }
        catch (GameSaveStorageException exception)
        {
            error = $"Your saved games could not be listed. {exception.Message}";
            return [];
        }
    }
}