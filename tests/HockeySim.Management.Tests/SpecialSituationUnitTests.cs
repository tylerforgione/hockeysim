using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Lineups;

using Xunit;

using static HockeySim.Management.Tests.SeasonAdvancementTests;

namespace HockeySim.Management.Tests;

public sealed class SpecialSituationUnitTests
{
    [Fact]
    public void EveryGeneratedTeamHasEveryUnitFilledWithItsDressedSkaters()
    {
        var snapshot = StartAtOpeningDay(new GameManager());

        Assert.All(snapshot.League.Teams, team =>
        {
            var rosterById = team.Roster.ToDictionary(player => player.Id);
            var dressedSkaters = team.Lineup.DressedPlayerIds
                .Where(id => rosterById[id].Position != Position.Goalie)
                .ToHashSet();

            foreach (var format in SpecialSituationFormat.All)
            {
                var units = team.Lineup.UnitsFor(format.Situation);
                Assert.Equal(format.UnitCount, units.Count);
                Assert.All(units, unit =>
                {
                    Assert.Equal(format.Roles.Count, unit.PlayerIds.Count);
                    Assert.Equal(unit.PlayerIds.Count, unit.PlayerIds.Distinct().Count());
                    Assert.All(unit.PlayerIds, id => Assert.Contains(id, dressedSkaters));
                });
            }

            Assert.Equal(2, team.Lineup.ExtraAttackerIds.Distinct().Count());
            Assert.All(team.Lineup.ExtraAttackerIds, id => Assert.Contains(id, dressedSkaters));
        });
    }

    [Fact]
    public void GeneratedPowerPlayUnitsComeFromTheTopLinesAndPairs()
    {
        var team = ManagedTeam(StartAtOpeningDay(new GameManager()));
        var lineup = team.Lineup;

        var units = lineup.UnitsFor(SpecialSituation.PowerPlay5On4);

        for (var index = 0; index < units.Count; index++)
        {
            var line = lineup.ForwardLines[index];
            var pair = lineup.DefencePairs[index];
            Assert.Equal(
                [line.LeftWingId, line.CentreId, line.RightWingId, pair.LeftDefenceId, pair.RightDefenceId],
                units[index].PlayerIds);
        }
    }

    [Fact]
    public void SetLineupChangesUnitsAndExtraAttackersWithAnySkaterInAnySlot()
    {
        var manager = new GameManager();
        var before = StartAtOpeningDay(manager);
        var team = ManagedTeam(before);
        var lineup = team.Lineup;
        var command = SetLineupCommand.From(lineup);
        var defence = lineup.DefencePairs.SelectMany(pair => new[] { pair.LeftDefenceId, pair.RightDefenceId }).ToList();
        var allDefencePowerPlay = new SpecialSituationUnitSelection(SpecialSituation.PowerPlay5On4, defence.Take(5).ToList());
        var killCentre = lineup.ForwardLines[3].RightWingId;
        var firstKill = command.SpecialSituationUnits.First(unit => unit.Situation == SpecialSituation.PenaltyKill3On5);
        command = command with
        {
            SpecialSituationUnits = command.SpecialSituationUnits
                .Select(unit => unit == command.SpecialSituationUnits[0]
                    ? allDefencePowerPlay
                    : unit == firstKill
                        ? unit with { PlayerIds = [killCentre, .. unit.PlayerIds.Skip(1)] }
                        : unit)
                .ToList(),
            ExtraAttackerIds = [defence[5], lineup.ForwardLines[3].LeftWingId],
        };

        var after = ManagedTeam(manager.SetLineup(command));

        Assert.Equal(defence.Take(5), after.Lineup.UnitsFor(SpecialSituation.PowerPlay5On4)[0].PlayerIds);
        Assert.Equal(killCentre, after.Lineup.UnitsFor(SpecialSituation.PenaltyKill3On5)[0].PlayerIds[0]);
        Assert.Equal([defence[5], lineup.ForwardLines[3].LeftWingId], after.Lineup.ExtraAttackerIds);
        Assert.Equal(lineup.DressedPlayerIds, after.Lineup.DressedPlayerIds);
        Assert.Equal(
            Describe(before.League.Teams.Where(other => other.Id != team.Id)),
            Describe(manager.GetSnapshot().League.Teams.Where(other => other.Id != team.Id)));
    }

    [Fact]
    public void ScratchingAPlayerWhoFillsUnitSlotsIsRejectedUnlessTheUnitsAreChangedToo()
    {
        var manager = new GameManager();
        var before = StartAtOpeningDay(manager);
        var team = ManagedTeam(before);
        var scratchedCentreId = team.ScratchedPlayerIds.Single(
            id => team.Roster.Single(player => player.Id == id).Position == Position.Centre);
        var firstCentreId = team.Lineup.ForwardLines[0].CentreId;
        var command = SetLineupCommand.From(team.Lineup);
        command = command with
        {
            ForwardLines = [command.ForwardLines[0] with { CentreId = scratchedCentreId }, .. command.ForwardLines.Skip(1)],
        };

        AssertRejectedWithoutChange(manager, command);

        var after = ManagedTeam(manager.SetLineup(command.ReplaceInUnits(firstCentreId, scratchedCentreId)));
        Assert.Contains(firstCentreId, after.ScratchedPlayerIds);
        Assert.DoesNotContain(after.Lineup.SpecialSituationUnits, unit => unit.PlayerIds.Contains(firstCentreId));
        Assert.DoesNotContain(firstCentreId, after.Lineup.ExtraAttackerIds);
        Assert.Contains(after.Lineup.SpecialSituationUnits, unit => unit.PlayerIds.Contains(scratchedCentreId));
    }

    public static TheoryData<string> InvalidUnitChanges() =>
    [
        "goalie in a unit",
        "player twice in a unit",
        "too few skaters in a unit",
        "missing unit",
        "extra unit",
        "player outside the roster",
        "one extra attacker",
        "goalie as extra attacker",
        "same extra attacker twice",
    ];

    [Theory]
    [MemberData(nameof(InvalidUnitChanges))]
    public void SetLineupRejectsInvalidUnitsWithoutChangingState(string change)
    {
        var manager = new GameManager();
        var before = StartAtOpeningDay(manager);
        var team = ManagedTeam(before);
        var command = SetLineupCommand.From(team.Lineup);
        var units = command.SpecialSituationUnits;
        var first = units[0];
        var foreignId = before.League.Teams.First(other => other.Id != team.Id).Roster[0].Id;

        SetLineupCommand WithFirstUnit(SpecialSituationUnitSelection unit) =>
            command with { SpecialSituationUnits = [unit, .. units.Skip(1)] };

        command = change switch
        {
            "goalie in a unit" => WithFirstUnit(first with { PlayerIds = [team.Lineup.StartingGoalieId, .. first.PlayerIds.Skip(1)] }),
            "player twice in a unit" => WithFirstUnit(first with { PlayerIds = [first.PlayerIds[1], .. first.PlayerIds.Skip(1)] }),
            "too few skaters in a unit" => WithFirstUnit(first with { PlayerIds = first.PlayerIds.Skip(1).ToList() }),
            "missing unit" => command with { SpecialSituationUnits = units.Skip(1).ToList() },
            "extra unit" => command with { SpecialSituationUnits = [.. units, first] },
            "player outside the roster" => WithFirstUnit(first with { PlayerIds = [foreignId, .. first.PlayerIds.Skip(1)] }),
            "one extra attacker" => command with { ExtraAttackerIds = command.ExtraAttackerIds.Take(1).ToList() },
            "goalie as extra attacker" => command with { ExtraAttackerIds = [command.ExtraAttackerIds[0], team.Lineup.BackupGoalieId] },
            "same extra attacker twice" => command with { ExtraAttackerIds = [command.ExtraAttackerIds[0], command.ExtraAttackerIds[0]] },
            _ => throw new ArgumentOutOfRangeException(nameof(change)),
        };

        AssertRejectedWithoutChange(manager, command);
    }

    [Fact]
    public void UnitSnapshotsCannotBeMutatedOrChangedByLaterCommands()
    {
        var manager = new GameManager();
        var team = ManagedTeam(StartAtOpeningDay(manager));
        var units = team.Lineup.SpecialSituationUnits;
        var description = Describe([team]);

        Assert.Throws<NotSupportedException>(() => ((IList<SpecialSituationUnitSnapshot>)units).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<PlayerId>)units[0].PlayerIds).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<PlayerId>)team.Lineup.ExtraAttackerIds).Clear());

        var command = SetLineupCommand.From(team.Lineup);
        manager.SetLineup(command with { ExtraAttackerIds = command.ExtraAttackerIds.Reverse().ToList() });

        Assert.Equal(description, Describe([team]));
    }

    private static void AssertRejectedWithoutChange(GameManager manager, SetLineupCommand command)
    {
        var before = Describe(manager.GetSnapshot().League.Teams);

        Assert.ThrowsAny<ArgumentException>(() => manager.SetLineup(command));

        Assert.Equal(before, Describe(manager.GetSnapshot().League.Teams));
    }

    private static string Describe(IEnumerable<TeamSnapshot> teams) =>
        string.Join(
            Environment.NewLine,
            teams.Select(team =>
                $"{team.Id} {string.Join(",", team.Lineup.DressedPlayerIds)} "
                + string.Join(";", team.Lineup.SpecialSituationUnits.Select(unit => $"{unit.Situation}:{string.Join(",", unit.PlayerIds)}"))
                + $" ea {string.Join(",", team.Lineup.ExtraAttackerIds)}"));
}