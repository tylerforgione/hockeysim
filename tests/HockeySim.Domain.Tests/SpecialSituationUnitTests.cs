using HockeySim.Domain;

using Xunit;

using static HockeySim.Domain.Tests.LineupTests;

namespace HockeySim.Domain.Tests;

public sealed class SpecialSituationUnitTests
{
    public static TheoryData<SpecialSituation> Situations() => new(Enum.GetValues<SpecialSituation>());

    [Fact]
    public void EverySituationHasAFormatWithOneCentre()
    {
        Assert.Equal(Enum.GetValues<SpecialSituation>(), SpecialSituationFormat.All.Select(format => format.Situation));
        Assert.All(SpecialSituationFormat.All, format =>
            Assert.Single(format.Roles, role => role == SkaterRole.Centre));
    }

    [Fact]
    public void FormatsMatchTheManpowerOfEachSituation()
    {
        int Skaters(SpecialSituation situation) => SpecialSituationFormat.For(situation).Roles.Count;
        int Units(SpecialSituation situation) => SpecialSituationFormat.For(situation).UnitCount;

        Assert.Equal(5, Skaters(SpecialSituation.PowerPlay5On4));
        Assert.Equal(5, Skaters(SpecialSituation.PowerPlay5On3));
        Assert.Equal(4, Skaters(SpecialSituation.PowerPlay4On3));
        Assert.Equal(4, Skaters(SpecialSituation.PenaltyKill4On5));
        Assert.Equal(3, Skaters(SpecialSituation.PenaltyKill3On5));
        Assert.Equal(3, Skaters(SpecialSituation.PenaltyKill3On4));
        Assert.Equal(4, Skaters(SpecialSituation.FourOnFour));
        Assert.Equal(3, Skaters(SpecialSituation.ThreeOnThree));
        Assert.Equal(
            [2, 2, 2, 3, 2, 2, 2, 3],
            Enum.GetValues<SpecialSituation>().Select(Units));
        Assert.Equal(
            [SkaterRole.Wing, SkaterRole.Centre, SkaterRole.Wing, SkaterRole.Defence, SkaterRole.Defence],
            SpecialSituationFormat.For(SpecialSituation.PowerPlay5On4).Roles);
    }

    [Fact]
    public void AnUndefinedSituationHasNoFormat()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SpecialSituationFormat.For((SpecialSituation)99));
    }

    [Fact]
    public void AnySkaterCanFillAnyUnitSlot()
    {
        var roster = CreateRoster();
        var defence = roster.Where(player => player.Position == Position.Defence).Take(3).ToList();
        var centre = roster.First(player => player.Position == Position.Centre);
        var wing = roster.First(player => player.Position == Position.Wing);

        var unit = new SpecialSituationUnit(
            SpecialSituation.PowerPlay5On4,
            [defence[0], wing, centre, defence[1], defence[2]]);

        Assert.Same(wing, unit.Centre);
        Assert.Equal([defence[0], wing, centre, defence[1], defence[2]], unit.Players);
    }

    [Theory]
    [MemberData(nameof(Situations))]
    public void AUnitNeedsOneSkaterPerSlot(SpecialSituation situation)
    {
        var skaters = CreateRoster().Where(player => player.Position != Position.Goalie).ToList();
        var slotCount = SpecialSituationFormat.For(situation).Roles.Count;

        Assert.Throws<ArgumentException>(() => new SpecialSituationUnit(situation, skaters.Take(slotCount - 1)));
        Assert.Throws<ArgumentException>(() => new SpecialSituationUnit(situation, skaters.Take(slotCount + 1)));
        Assert.Equal(slotCount, new SpecialSituationUnit(situation, skaters.Take(slotCount)).Players.Count);
    }

    [Fact]
    public void AUnitRejectsAGoalie()
    {
        var roster = CreateRoster();
        var goalie = roster.First(player => player.Position == Position.Goalie);
        var skaters = roster.Where(player => player.Position != Position.Goalie).Take(2);

        Assert.Throws<ArgumentException>(() => new SpecialSituationUnit(
            SpecialSituation.PenaltyKill3On5,
            [goalie, .. skaters]));
    }

    [Fact]
    public void AUnitRejectsTheSamePlayerTwice()
    {
        var skaters = CreateRoster().Where(player => player.Position != Position.Goalie).ToList();

        Assert.Throws<ArgumentException>(() => new SpecialSituationUnit(
            SpecialSituation.ThreeOnThree,
            [skaters[0], skaters[1], skaters[0]]));
    }

    [Fact]
    public void DefaultUnitsFillEverySituationWithDressedSkaters()
    {
        var lineup = CreateLineup(CreateRoster());
        var dressedSkaters = lineup.ForwardLines.SelectMany(line => line.Players)
            .Concat(lineup.DefencePairs.SelectMany(pair => pair.Players))
            .ToList();

        foreach (var format in SpecialSituationFormat.All)
        {
            var units = lineup.UnitsFor(format.Situation);
            Assert.Equal(format.UnitCount, units.Count);
            Assert.All(units, unit => Assert.Equal(format.Situation, unit.Situation));
        }

        Assert.All(lineup.SpecialSituationUnits.SelectMany(unit => unit.Players), player => Assert.Contains(player, dressedSkaters));
        Assert.Equal(2, lineup.ExtraAttackers.Count);
        Assert.All(lineup.ExtraAttackers, player => Assert.Contains(player, dressedSkaters));
    }

    [Fact]
    public void DefaultUnitsFollowTheLinesAndPairs()
    {
        var lineup = CreateLineup(CreateRoster());
        var lines = lineup.ForwardLines;
        var pairs = lineup.DefencePairs;

        var firstPowerPlay = lineup.UnitsFor(SpecialSituation.PowerPlay5On4)[0];
        Assert.Equal([.. lines[0].Players, .. pairs[0].Players], firstPowerPlay.Players);
        Assert.Same(lines[0].Centre, firstPowerPlay.Centre);
        Assert.Equal(
            [lines[1].Centre, lines[1].LeftWing, pairs[0].LeftDefence, pairs[0].RightDefence],
            lineup.UnitsFor(SpecialSituation.PenaltyKill4On5)[0].Players);
        Assert.Equal([lines[0].Centre, lines[1].Centre], lineup.ExtraAttackers);
    }

    [Fact]
    public void APlayerMayAppearInSeveralUnits()
    {
        var lineup = CreateLineup(CreateRoster());
        var firstCentre = lineup.ForwardLines[0].Centre;

        Assert.True(lineup.SpecialSituationUnits.Count(unit => unit.Players.Contains(firstCentre)) > 1);
    }

    [Fact]
    public void UnitsKeepTheirGivenOrderWithinASituation()
    {
        var valid = CreateLineup(CreateRoster());
        var reordered = valid.SpecialSituationUnits.Reverse().ToList();

        var lineup = Rebuild(valid, units: reordered);

        Assert.Equal(
            valid.UnitsFor(SpecialSituation.PenaltyKill4On5).Reverse(),
            lineup.UnitsFor(SpecialSituation.PenaltyKill4On5));
        Assert.Equal(
            Enum.GetValues<SpecialSituation>().SelectMany(situation => Enumerable.Repeat(situation, SpecialSituationFormat.For(situation).UnitCount)),
            lineup.SpecialSituationUnits.Select(unit => unit.Situation));
    }

    [Theory]
    [MemberData(nameof(Situations))]
    public void ALineupNeedsExactlyTheRequiredUnitsForEverySituation(SpecialSituation situation)
    {
        var valid = CreateLineup(CreateRoster());
        var situationUnits = valid.UnitsFor(situation);

        var missing = valid.SpecialSituationUnits.Except([situationUnits[0]]);
        var extra = valid.SpecialSituationUnits.Append(situationUnits[0]);

        Assert.Throws<ArgumentException>(() => Rebuild(valid, units: missing));
        Assert.Throws<ArgumentException>(() => Rebuild(valid, units: extra));
    }

    [Fact]
    public void ScratchingAPlayerWhoFillsUnitSlotsIsRejectedUntilTheUnitsChange()
    {
        var roster = CreateRoster();
        var valid = CreateLineup(roster);
        var scratchedCentre = roster.Single(player => player.Position == Position.Centre && !valid.DressedPlayers.Contains(player));
        var replaced = valid.ForwardLines[0].Centre;
        var lines = valid.ForwardLines.ToList();
        lines[0] = new ForwardLine(lines[0].LeftWing, scratchedCentre, lines[0].RightWing);

        Assert.Throws<ArgumentException>(() => Rebuild(valid, forwardLines: lines));

        var substituted = valid.SpecialSituationUnits.Select(unit => new SpecialSituationUnit(
            unit.Situation,
            unit.Players.Select(player => player == replaced ? scratchedCentre : player)));
        var extraAttackers = valid.ExtraAttackers.Select(player => player == replaced ? scratchedCentre : player);
        Assert.Throws<ArgumentException>(() => Rebuild(valid, forwardLines: lines, units: substituted));

        var lineup = Rebuild(valid, forwardLines: lines, units: substituted, extraAttackers: extraAttackers);
        Assert.DoesNotContain(lineup.SpecialSituationUnits, unit => unit.Players.Contains(replaced));
        Assert.Contains(scratchedCentre, lineup.ExtraAttackers);
    }

    [Fact]
    public void ALineupRejectsAUnitSkaterFromOutsideTheLineup()
    {
        var roster = CreateRoster();
        var valid = CreateLineup(roster);
        var scratch = roster.First(player => player.Position == Position.Defence && !valid.DressedPlayers.Contains(player));
        var first = valid.SpecialSituationUnits[0];
        var units = valid.SpecialSituationUnits
            .Select(unit => unit == first
                ? new SpecialSituationUnit(unit.Situation, [.. unit.Players.SkipLast(1), scratch])
                : unit);

        Assert.Throws<ArgumentException>(() => Rebuild(valid, units: units));
    }

    [Fact]
    public void ALineupRejectsInvalidExtraAttackers()
    {
        var roster = CreateRoster();
        var valid = CreateLineup(roster);
        var first = valid.ExtraAttackers[0];
        var scratch = roster.First(player => player.Position == Position.Centre && !valid.DressedPlayers.Contains(player));

        Assert.Throws<ArgumentException>(() => Rebuild(valid, extraAttackers: [first]));
        Assert.Throws<ArgumentException>(() => Rebuild(valid, extraAttackers: [first, valid.ExtraAttackers[1], valid.ForwardLines[3].Centre]));
        Assert.Throws<ArgumentException>(() => Rebuild(valid, extraAttackers: [first, first]));
        Assert.Throws<ArgumentException>(() => Rebuild(valid, extraAttackers: [first, scratch]));
        Assert.Throws<ArgumentException>(() => Rebuild(valid, extraAttackers: [first, valid.StartingGoalie]));
    }

    [Fact]
    public void AnyDressedSkaterCanBeAnExtraAttacker()
    {
        var valid = CreateLineup(CreateRoster());
        var defence = valid.DefencePairs[2].RightDefence;

        var lineup = Rebuild(valid, extraAttackers: [defence, valid.ExtraAttackers[0]]);

        Assert.Equal([defence, valid.ExtraAttackers[0]], lineup.ExtraAttackers);
    }

    [Fact]
    public void UnitCollectionsCannotBeMutated()
    {
        var lineup = CreateLineup(CreateRoster());

        Assert.Throws<NotSupportedException>(() => ((IList<SpecialSituationUnit>)lineup.SpecialSituationUnits).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<Player>)lineup.ExtraAttackers).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<Player>)lineup.SpecialSituationUnits[0].Players).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<SkaterRole>)SpecialSituationFormat.All[0].Roles).Clear());
    }

    private static Lineup Rebuild(
        Lineup lineup,
        IEnumerable<ForwardLine>? forwardLines = null,
        IEnumerable<SpecialSituationUnit>? units = null,
        IEnumerable<Player>? extraAttackers = null) =>
        new(
            forwardLines ?? lineup.ForwardLines,
            lineup.DefencePairs,
            lineup.StartingGoalie,
            lineup.BackupGoalie,
            units ?? lineup.SpecialSituationUnits,
            extraAttackers ?? lineup.ExtraAttackers);
}