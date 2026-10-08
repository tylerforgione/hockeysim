using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Inbox;
using HockeySim.Management.Lineups;

using Xunit;

using static HockeySim.Management.Tests.SeasonAdvancementTests;

namespace HockeySim.Management.Tests;

/// <summary>
/// Injured players in lineups and the user's view of injuries. Injuries come from the real engine,
/// so each test plays seeded days until the situation it needs arises, within a bounded number of
/// days.
/// </summary>
public sealed class InjuredPlayerTests
{
    // Far more days than any of these situations takes to arise with the seeds used.
    private const int MaximumDays = 120;

    [Fact]
    public void AdvancingIsRejectedWhileTheManagedLineupDressesAPlayerWhoCannotPlay()
    {
        var manager = new GameManager();
        StartGame(manager);

        var (before, rejection) = AdvanceUntilRejected(manager);

        Assert.NotEmpty(rejection.PlayerIds);
        Assert.Equal(before.PlayersToReplace, rejection.PlayerIds);
        var team = ManagedTeam(before);
        Assert.All(rejection.PlayerIds, id =>
        {
            Assert.Contains(id, team.Lineup.DressedPlayerIds);
            Assert.False(team.Roster.Single(player => player.Id == id).CanPlay);
        });
        Assert.Contains(before.Schedule.Matches, match => match.Date == before.Season.CurrentDate
            && (match.HomeTeamId == before.ManagedTeamId || match.AwayTeamId == before.ManagedTeamId));

        var after = manager.GetSnapshot();
        Assert.Equal(before.Season.CurrentDate, after.Season.CurrentDate);
        Assert.Equal(before.RandomState, after.RandomState);
        Assert.Equal(before.Season.Results.Count, after.Season.Results.Count);
    }

    [Fact]
    public void ReplacingThePlayersWhoCannotPlayLetsTheDayBePlayed()
    {
        var manager = new GameManager();
        StartGame(manager);
        var (before, _) = AdvanceUntilRejected(manager);

        var replaced = manager.SetLineup(InjuredPlayerReplacement.ReplacingInjured(before));
        Assert.Empty(replaced.PlayersToReplace);
        var after = manager.AdvanceDay();

        Assert.Equal(before.Season.CurrentDate.AddDays(1), after.Season.CurrentDate);
        var managedResult = after.Season.Results.Single(result => result.Date == before.Season.CurrentDate
            && (result.Home.TeamId == before.ManagedTeamId || result.Away.TeamId == before.ManagedTeamId));
        var managedSide = managedResult.Home.TeamId == before.ManagedTeamId ? managedResult.Home : managedResult.Away;
        Assert.DoesNotContain(managedSide.Skaters, skater => before.PlayersToReplace.Contains(skater.PlayerId));
    }

    [Fact]
    public void AnInjuredPlayerCanStayDressedOnADayTheManagedTeamDoesNotPlay()
    {
        var manager = new GameManager();
        var snapshot = StartGame(manager);
        for (var day = 0; day < MaximumDays; day++)
        {
            var team = ManagedTeam(snapshot);
            var dressedOut = team.Roster.Where(player => !player.CanPlay && team.Lineup.DressedPlayerIds.Contains(player.Id));
            if (dressedOut.Any() && !ManagedTeamPlays(snapshot))
            {
                Assert.Empty(snapshot.PlayersToReplace);
                var next = manager.AdvanceDay();
                Assert.Equal(snapshot.Season.CurrentDate.AddDays(1), next.Season.CurrentDate);
                return;
            }

            snapshot = ManagedTeamPlays(snapshot) && snapshot.PlayersToReplace.Count > 0
                ? manager.AdvanceDayReplacingInjured()
                : manager.AdvanceDay();
        }

        Assert.Fail("No managed player was out on a day without a managed match.");
    }

    [Fact]
    public void AiTeamsDressHealthyScratchesInPlaceOfPlayersWhoCannotPlayAndRestoreTheirLineups()
    {
        var manager = new GameManager();
        var opening = StartGame(manager);
        var preferred = opening.League.Teams.ToDictionary(team => team.Id, team => team.Lineup);
        var snapshot = opening;
        var replacedTeams = new HashSet<TeamId>();
        var restoredTeams = new HashSet<TeamId>();

        for (var day = 0; day < MaximumDays && restoredTeams.Count == 0; day++)
        {
            foreach (var team in snapshot.League.Teams.Where(team => team.Id != snapshot.ManagedTeamId))
            {
                var outIds = team.Roster.Where(player => !player.CanPlay).Select(player => player.Id).ToHashSet();
                if (outIds.Count == 0)
                {
                    AssertSameLineup(preferred[team.Id], team.Lineup);
                    if (replacedTeams.Contains(team.Id))
                    {
                        restoredTeams.Add(team.Id);
                    }

                    continue;
                }

                Assert.DoesNotContain(team.Lineup.DressedPlayerIds, outIds.Contains);
                if (preferred[team.Id].DressedPlayerIds.Any(outIds.Contains))
                {
                    replacedTeams.Add(team.Id);
                    AssertReplacedOnlyThePlayersOut(preferred[team.Id], team.Lineup, outIds);
                }
            }

            snapshot = manager.AdvanceDayReplacingInjured();
        }

        Assert.NotEmpty(restoredTeams);
    }

    [Fact]
    public void TheHeadTrainerReportsEachManagedInjuryAndEachRecovery()
    {
        var manager = new GameManager();
        var snapshot = StartGame(manager);
        PlayerSnapshot? injured = null;
        InjurySnapshot? injury = null;
        for (var day = 0; day < MaximumDays && injury is null; day++)
        {
            var played = snapshot.Season.CurrentDate;
            snapshot = manager.AdvanceDayReplacingInjured();
            injured = ManagedTeam(snapshot).Roster.FirstOrDefault(player =>
                player.Injuries.Any(candidate => candidate.Date == played));
            injury = injured?.Injuries.First(candidate => candidate.Date == played);
        }

        Assert.NotNull(injured);
        Assert.NotNull(injury);
        var report = snapshot.Inbox.First(message => message.Subject == $"Injury: {injured.FirstName} {injured.LastName}");
        Assert.Equal(InboxSenderRole.HeadTrainer, report.SenderRole);
        Assert.False(report.IsRead);
        Assert.Contains(injury.ExpectedReturn.Earliest.ToString("MMMM d", System.Globalization.CultureInfo.InvariantCulture), report.Body);
        Assert.Contains(injury.ExpectedReturn.Latest.ToString("MMMM d", System.Globalization.CultureInfo.InvariantCulture), report.Body);
        Assert.Contains(injury.CanPlayThrough ? "can play through it" : "cannot play until it heals", report.Body);

        var recoverySubject = $"Recovered: {injured.FirstName} {injured.LastName}";
        Assert.DoesNotContain(snapshot.Inbox, message => message.Subject == recoverySubject);
        while (Player(snapshot, injured.Id).Injuries.Contains(injury))
        {
            snapshot = manager.AdvanceDayReplacingInjured();
        }

        // The injury healed on the current date, which falls within the expected return.
        Assert.InRange(snapshot.Season.CurrentDate, injury.ExpectedReturn.Earliest, injury.ExpectedReturn.Latest);
        var recovery = snapshot.Inbox.First(message => message.Subject == recoverySubject);
        Assert.Equal(InboxSenderRole.HeadTrainer, recovery.SenderRole);
    }

    [Fact]
    public void InjuryReportsCoverOnlyTheManagedTeam()
    {
        var manager = new GameManager();
        var snapshot = StartGame(manager);
        for (var day = 0; day < 30; day++)
        {
            snapshot = manager.AdvanceDayReplacingInjured();
        }

        var managedNames = ManagedTeam(snapshot).Roster.Select(player => $"{player.FirstName} {player.LastName}").ToHashSet();
        var reports = snapshot.Inbox.Where(message => message.SenderRole == InboxSenderRole.HeadTrainer).ToList();
        Assert.NotEmpty(reports);
        Assert.All(reports, message => Assert.Contains(message.Subject[(message.Subject.IndexOf(':') + 2)..], managedNames));
    }

    [Fact]
    public void SnapshotsShowOnlyInjuriesThatHaveNotHealed()
    {
        var manager = new GameManager();
        var snapshot = StartGame(manager);
        Assert.All(snapshot.League.Teams.SelectMany(team => team.Roster), player =>
        {
            Assert.Empty(player.Injuries);
            Assert.True(player.CanPlay);
        });

        for (var day = 0; day < 30; day++)
        {
            snapshot = manager.AdvanceDayReplacingInjured();
            Assert.All(snapshot.League.Teams.SelectMany(team => team.Roster).SelectMany(player => player.Injuries), injury =>
            {
                Assert.True(injury.Date < snapshot.Season.CurrentDate);
                Assert.True(injury.ExpectedReturn.Latest > snapshot.Season.CurrentDate);
                Assert.Equal(InjuryCatalogue.For(injury.Type).BodyPart, injury.BodyPart);
                Assert.Equal(InjuryCatalogue.For(injury.Type).CanPlayThrough, injury.CanPlayThrough);
            });
        }

        Assert.Contains(snapshot.League.Teams.SelectMany(team => team.Roster), player => !player.CanPlay);
    }

    private static (GameSnapshot Before, UnavailablePlayersException Rejection) AdvanceUntilRejected(GameManager manager)
    {
        for (var day = 0; day < MaximumDays; day++)
        {
            var before = manager.GetSnapshot();
            try
            {
                manager.AdvanceDay();
            }
            catch (UnavailablePlayersException rejection)
            {
                return (before, rejection);
            }
        }

        throw new InvalidOperationException("The managed team never dressed a player who could not play.");
    }

    private static bool ManagedTeamPlays(GameSnapshot snapshot) =>
        snapshot.Schedule.Matches.Any(match => match.Date == snapshot.Season.CurrentDate
            && (match.HomeTeamId == snapshot.ManagedTeamId || match.AwayTeamId == snapshot.ManagedTeamId));

    private static PlayerSnapshot Player(GameSnapshot snapshot, PlayerId id) =>
        snapshot.League.Teams.SelectMany(team => team.Roster).Single(player => player.Id == id);

    private static void AssertSameLineup(LineupSnapshot expected, LineupSnapshot actual) =>
        Assert.Equal(Slots(expected), Slots(actual));

    /// <summary>
    /// Every place in the preferred lineup keeps its player unless that player is out; a player
    /// who is out is replaced by the same scratch in every place they held.
    /// </summary>
    private static void AssertReplacedOnlyThePlayersOut(LineupSnapshot preferred, LineupSnapshot dressed, HashSet<PlayerId> outIds)
    {
        var replacements = new Dictionary<PlayerId, PlayerId>();
        foreach (var (wanted, actual) in Slots(preferred).Zip(Slots(dressed)))
        {
            if (!outIds.Contains(wanted))
            {
                Assert.Equal(wanted, actual);
                continue;
            }

            Assert.DoesNotContain(actual, preferred.DressedPlayerIds);
            Assert.Equal(actual, replacements.GetValueOrDefault(wanted, actual));
            replacements[wanted] = actual;
        }
    }

    /// <summary>Every place in a lineup, in a fixed order: lines, pairs, goalies, units, extra attackers.</summary>
    private static List<PlayerId> Slots(LineupSnapshot lineup) =>
    [
        .. lineup.ForwardLines.SelectMany(line => new[] { line.LeftWingId, line.CentreId, line.RightWingId }),
        .. lineup.DefencePairs.SelectMany(pair => new[] { pair.LeftDefenceId, pair.RightDefenceId }),
        lineup.StartingGoalieId,
        lineup.BackupGoalieId,
        .. lineup.SpecialSituationUnits.SelectMany(unit => unit.PlayerIds),
        .. lineup.ExtraAttackerIds,
    ];
}