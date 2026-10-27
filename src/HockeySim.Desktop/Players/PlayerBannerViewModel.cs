using System.Globalization;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HockeySim.Domain;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Desktop.Players;

/// <summary>
/// A player's banner, shown in place of the managed team's: the jersey number, name, position,
/// handedness, age, team link, an injury badge, and the figures that sum the player up, all in the
/// colours of the player's own team.
/// </summary>
public sealed partial class PlayerBannerViewModel : ObservableObject
{
    [ObservableProperty]
    private string _number = string.Empty;

    [ObservableProperty]
    private string _fullName = string.Empty;

    /// <summary>Position, handedness, and age, such as "Wing · Shoots Right · Age 29"; the team link follows.</summary>
    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _teamName = string.Empty;

    [ObservableProperty]
    private TeamColours _colours = default!;

    /// <summary>Out or playing through, with the injury's name; null for a healthy player.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInjury))]
    private string? _injuryBadge;

    [ObservableProperty]
    private string? _injurySummary;

    [ObservableProperty]
    private bool _isOut;

    [ObservableProperty]
    private IReadOnlyList<PlayerFigureViewModel> _figures = [];

    private TeamId _teamId;

    public PlayerBannerViewModel(Action<TeamId> openTeam)
    {
        ArgumentNullException.ThrowIfNull(openTeam);

        OpenTeamCommand = new RelayCommand(() => openTeam(_teamId));
    }

    public bool HasInjury => InjuryBadge is not null;

    /// <summary>Shows the team's page.</summary>
    public ICommand OpenTeamCommand { get; }

    /// <param name="totals">The totals of the statistic set the page shows.</param>
    public void Show(PlayerSnapshot player, TeamSnapshot team, PlayerSeasonTotals totals)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(totals);

        _teamId = team.Id;
        Number = player.Number.ToString(CultureInfo.CurrentCulture);
        FullName = PlayerDisplay.FullName(player);
        Description = $"{PlayerDisplay.PositionName(player.Position)} · {PlayerDisplay.HandednessLabel(player.Position)} {PlayerDisplay.HandednessName(player.Biography.Handedness)} · Age {player.Age}";
        TeamName = team.Name;
        Colours = team.Colours;

        // A player who cannot play is out whatever else ails them; the badge names the injury that matters.
        var injury = player.Injuries.FirstOrDefault(candidate => !candidate.CanPlayThrough) ?? player.Injuries.FirstOrDefault();
        IsOut = !player.CanPlay;
        InjuryBadge = injury is null
            ? null
            : $"{InjuryDisplay.StatusMarker(player)} · {InjuryDisplay.Name(injury.Type)} · {InjuryDisplay.Status(injury)}";
        InjurySummary = injury is null ? null : InjuryDisplay.Summary(player);

        Figures = player.Position == Position.Goalie
            ? [new("OVERALL", player.Overall.ToString(CultureInfo.CurrentCulture), player.Overall), new("SV%", totals.SavePercentage), new("GSAx", totals.GoalsSavedAboveExpected)]
            : [new("OVERALL", player.Overall.ToString(CultureInfo.CurrentCulture), player.Overall), new("POINTS", totals.Points.ToString(CultureInfo.CurrentCulture)), new("5V5 xGF%", totals.OnIceExpectedGoalsPercentage)];
    }
}

/// <summary>One of the figures on the right of the player banner.</summary>
/// <param name="Rating">The rating the figure shows, coloured by tier; null for other figures.</param>
public sealed record PlayerFigureViewModel(string Label, string Value, int? Rating = null)
{
    public bool HasRating => Rating is not null;
}