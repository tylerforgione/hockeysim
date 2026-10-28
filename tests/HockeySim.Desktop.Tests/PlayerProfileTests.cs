using System.Globalization;
using System.Text.RegularExpressions;

using HockeySim.Desktop.Game;
using HockeySim.Desktop.Players;
using HockeySim.Desktop.Roster;
using HockeySim.Desktop.Schedule;
using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;

using Xunit;

namespace HockeySim.Desktop.Tests;

/// <summary>
/// The player profile page against real games: skaters and goalies, healthy, playing hurt, and
/// out, a player who has not played, the game log, usage, the playoff switch, and how the page
/// sits in the shell. Injuries come from the real engine, so those tests play seeded days until a
/// player has the injury they need. The played season is shared with the playoff and home page tests.
/// </summary>
[Collection(PlayedSeasonCollection.Name)]
public sealed class PlayerProfileTests(PlayoffDisplayTests.PlayedSeason season)
{
    // Far more days than any of these situations takes to arise with the test seed.
    private const int MaximumDays = 120;

    private static readonly Lazy<GameSession> PlayingHurtSession = new(() =>
        SessionWhere(player => player.Position != Position.Goalie && player.Injuries.Count > 0 && player.CanPlay));

    private static readonly Lazy<GameSession> OutSession = new(() => SessionWhere(player => !player.CanPlay));

    /// <summary>Builds a profile outside the shell, whose links go nowhere.</summary>
    internal static PlayerProfilePageViewModel Profile(
        GameSession session,
        PlayerId playerId,
        PlayerProfileTab tab = PlayerProfileTab.Overview,
        StatisticsSet statistics = StatisticsSet.RegularSeason) =>
        new(session, playerId, tab, statistics, _ => { }, (_, _) => { });

    [Fact]
    public void ASkaterProfileShowsIdentityAndEverySkaterRatingButNotDurability()
    {
        var session = GameTestData.StartSession();
        var team = session.ManagedTeam;
        var centre = team.Roster.First(player => player.Position == Position.Centre);

        var profile = Profile(session, centre.Id);

        Assert.Equal($"{centre.FirstName} {centre.LastName}", profile.FullName);
        Assert.Equal($"#{centre.Number}", profile.Number);
        Assert.Equal("Centre", profile.Position);
        Assert.Equal(centre.Age, profile.Age);
        Assert.Equal(team.Name, profile.TeamName);
        Assert.Equal(team.Name, profile.Subtitle);
        Assert.Equal(centre.Overall, profile.Overall);
        Assert.False(profile.IsGoalie);
        Assert.Equal(["OFFENCE", "DEFENCE", "GAME"], profile.RatingGroups.Select(group => group.Name));
        var names = profile.RatingGroups.SelectMany(group => group.Ratings).Select(rating => rating.Name).ToList();
        Assert.Equal(14, names.Count);
        Assert.DoesNotContain("Durability", names);
        Assert.DoesNotContain("Reflexes", names);
        Assert.Equal(names, profile.OverviewRatings.Select(rating => rating.Name));
        Assert.Equal(centre.Ratings[Rating.Faceoffs], RatingValue(profile, "Faceoffs"));
        Assert.Equal(centre.Ratings[Rating.Toughness], RatingValue(profile, "Toughness"));
        Assert.Equal(centre.Ratings[Rating.Discipline], RatingValue(profile, "Discipline"));
        Assert.Equal(new RatingLineViewModel("Overall", centre.Overall, centre.Overall), profile.OverallRating);
    }

    [Fact]
    public void AGoalieProfileShowsOnlyGoalieRatings()
    {
        var session = GameTestData.StartSession();
        var team = session.ManagedTeam;
        var goalie = team.Roster.First(player => player.Position == Position.Goalie);

        var profile = Profile(session, goalie.Id);

        Assert.Equal("Goalie", profile.Position);
        Assert.True(profile.IsGoalie);
        Assert.Equal(["GOALTENDING", "GAME"], profile.RatingGroups.Select(group => group.Name));
        Assert.Equal(
            ["Reflexes", "Positioning", "Rebound Control", "Stamina", "Discipline"],
            profile.RatingGroups.SelectMany(group => group.Ratings).Select(rating => rating.Name));
        Assert.Equal(goalie.Ratings[Rating.GoalieReflex], RatingValue(profile, "Reflexes"));
        Assert.Equal(goalie.Ratings[Rating.Stamina], RatingValue(profile, "Stamina"));
    }

    [Fact]
    public void TheBiographyShowsWhetherThePlayerShootsOrCatches()
    {
        var session = GameTestData.StartSession();
        var team = session.ManagedTeam;
        var skater = team.Roster.First(player => player.Position != Position.Goalie);
        var goalie = team.Roster.First(player => player.Position == Position.Goalie);

        var skaterProfile = Profile(session, skater.Id);
        var goalieProfile = Profile(session, goalie.Id);

        Assert.Equal("SHOOTS", skaterProfile.HandednessLabel);
        Assert.Equal("CATCHES", goalieProfile.HandednessLabel);
        Assert.Equal(
            ["Born", "Birthplace", "Nationality", "Height", "Weight", "Shoots", "Position", "Number"],
            skaterProfile.Biography.Select(fact => fact.Label));
        Assert.Equal("Catches", goalieProfile.Biography[5].Label);
        Assert.Equal(
            [
                PlayerDisplay.FormatBirthDate(skater.Biography.BirthDate),
                PlayerDisplay.FormatBirthplace(skater.Biography.Birthplace),
                PlayerDisplay.CountryName(skater.Biography.Nationality),
                PlayerDisplay.FormatHeight(skater.Biography.Height),
                $"{skater.Biography.Weight.Pounds} lb",
                PlayerDisplay.HandednessName(skater.Biography.Handedness),
                PlayerDisplay.PositionName(skater.Position),
                skater.Number.ToString(CultureInfo.CurrentCulture),
            ],
            skaterProfile.Biography.Select(fact => fact.Value));
        Assert.StartsWith(skater.Biography.Birthplace.City, skaterProfile.Birthplace, StringComparison.Ordinal);
        Assert.EndsWith(PlayerDisplay.CountryName(skater.Biography.Birthplace.Country), skaterProfile.Birthplace, StringComparison.Ordinal);
    }

    [Fact]
    public void TheBannerNamesThePlayerInTheirTeamsColoursWithOverallPointsAndExpectedGoalShare()
    {
        var session = GameTestData.StartSession();
        var team = session.ManagedTeam;
        var wing = team.Roster.First(player => player.Position == Position.Wing);

        var banner = Assert.IsType<PlayerBannerViewModel>(Profile(session, wing.Id).Banner);

        Assert.Equal(wing.Number.ToString(CultureInfo.InvariantCulture), banner.Number);
        Assert.Equal(PlayerDisplay.FullName(wing), banner.FullName);
        Assert.Equal(
            $"Wing · Shoots {PlayerDisplay.HandednessName(wing.Biography.Handedness)} · Age {wing.Age}",
            banner.Description);
        Assert.Equal(team.Name, banner.TeamName);
        Assert.Equal(team.Colours.Primary, banner.Colours.Primary);
        Assert.Equal(team.Colours.Secondary, banner.Colours.Secondary);
        Assert.False(banner.HasInjury);
        Assert.Equal(["OVERALL", "POINTS", "5V5 xGF%"], banner.Figures.Select(figure => figure.Label));
        Assert.Equal([wing.Overall.ToString(CultureInfo.InvariantCulture), "0", "—"], banner.Figures.Select(figure => figure.Value));
        Assert.Equal(wing.Overall, banner.Figures[0].Rating);
        Assert.Null(banner.Figures[1].Rating);
    }

    [Fact]
    public void AGoalieBannerShowsOverallSavePercentageAndGoalsSavedAboveExpected()
    {
        var session = GameTestData.StartSession();
        var goalie = session.ManagedTeam.Roster.First(player => player.Position == Position.Goalie);

        var banner = Assert.IsType<PlayerBannerViewModel>(Profile(session, goalie.Id).Banner);

        Assert.StartsWith("Goalie · Catches ", banner.Description, StringComparison.Ordinal);
        Assert.Equal(["OVERALL", "SV%", "GSAx"], banner.Figures.Select(figure => figure.Label));
        Assert.Equal([goalie.Overall.ToString(CultureInfo.InvariantCulture), "—", "0.00"], banner.Figures.Select(figure => figure.Value));
    }

    [Fact]
    public void ASkaterWhoHasNotPlayedShowsZerosDashesAndAnEmptyGameLog()
    {
        var session = GameTestData.StartPreseasonSession();
        var team = session.ManagedTeam;
        var first = team.Lineup.ForwardLines[0].CentreId;

        var profile = Profile(session, first);

        Assert.Equal("2026–27 statistics", profile.StatisticsTitle);
        Assert.Equal(["SCORING", "SHOOTING AND USAGE", "ON ICE AT 5V5", "PHYSICAL"], profile.OverviewStatisticGroups.Select(group => group.Name));
        Assert.Equal(["GP", "G", "A", "P", "+/-", "PIM"], Labels(profile.OverviewStatisticGroups[0]));
        Assert.Equal(["SOG", "SH%", "ixG", "TOI/GP", "PPP", "SHP"], Labels(profile.OverviewStatisticGroups[1]));
        Assert.Equal(["CF", "CA", "CF%", "xGF", "xGA", "xGF%"], Labels(profile.OverviewStatisticGroups[2]));
        Assert.Equal(["HIT", "BLK", "TK", "GV", "FO%"], Labels(profile.OverviewStatisticGroups[3]));
        var line = profile.OverviewStatisticGroups.SelectMany(group => group.Statistics).ToDictionary(stat => stat.Label, stat => stat.Value);
        Assert.All(["SH%", "TOI/GP", "CF%", "xGF%", "FO%"], label => Assert.Equal("—", line[label]));
        Assert.All(["GP", "G", "A", "P", "+/-", "PIM", "SOG", "PPP", "SHP", "CF", "CA", "HIT", "BLK", "TK", "GV"], label => Assert.Equal("0", line[label]));
        Assert.Equal("0.00", line["ixG"]);

        Assert.Equal(
            ["SCORING", "SPECIAL TEAMS", "SHOOTING AND PLAY", "FACEOFFS AND ICE TIME", "5-ON-5 ON ICE"],
            profile.StatisticGroups.Select(group => group.Name));
        Assert.Equal(["GP", "G", "A", "P", "+/-", "PIM", "ENG"], Labels(profile.StatisticGroups[0]));

        Assert.Empty(profile.GameLog);
        Assert.Empty(profile.LastFiveGames);
        Assert.Equal(PlayerGameLog.SkaterColumns, profile.GameLogColumns);
        Assert.Equal(["Even strength / GP", "Power play / GP", "Penalty kill / GP", "Line"], profile.Usage.Select(fact => fact.Label));
        Assert.Equal(
            ["—", WithTags("—", PowerPlayTags(team.Lineup, first)), WithTags("—", PenaltyKillTags(team.Lineup, first)), "F1 C"],
            profile.Usage.Select(fact => fact.Value));
        Assert.StartsWith("— · PP1", profile.Usage[1].Value, StringComparison.Ordinal);

        // A scratched skater is on no unit.
        var scratch = team.Roster.First(player => player.Position != Position.Goalie && team.ScratchedPlayerIds.Contains(player.Id));
        Assert.Equal(["—", "—", "—", "Scratch"], Profile(session, scratch.Id).Usage.Select(fact => fact.Value));
    }

    [Fact]
    public void AGoalieWhoHasNotPlayedShowsZerosDashesAndAnEmptyGameLog()
    {
        var session = GameTestData.StartPreseasonSession();
        var backup = session.ManagedTeam.Lineup.BackupGoalieId;

        var profile = Profile(session, backup);

        Assert.Equal(["GOALTENDING", "EXPECTED AND USAGE"], profile.OverviewStatisticGroups.Select(group => group.Name));
        Assert.Equal(["GP", "SA", "SV", "GA", "SV%", "GAA"], Labels(profile.OverviewStatisticGroups[0]));
        Assert.Equal(["0", "0", "0", "0", "—", "—"], profile.OverviewStatisticGroups[0].Statistics.Select(stat => stat.Value));
        Assert.Equal(["xGA", "GSAx", "xSV%", "SO", "TOI/GP"], Labels(profile.OverviewStatisticGroups[1]));
        Assert.Equal(["0.00", "0.00", "—", "0", "—"], profile.OverviewStatisticGroups[1].Statistics.Select(stat => stat.Value));
        Assert.Equal(["GP", "SA", "SV", "GA", "SV%", "GAA", "SO"], Labels(profile.StatisticGroups[0]));
        Assert.Equal(["0", "0", "0", "0", "—", "—", "0"], profile.StatisticGroups[0].Statistics.Select(stat => stat.Value));
        Assert.Equal(["0.00", "0.00", "0:00", "—"], profile.StatisticGroups[1].Statistics.Select(stat => stat.Value));
        Assert.Empty(profile.GameLog);
        Assert.Equal(PlayerGameLog.GoalieColumns, profile.GameLogColumns);
        Assert.Equal([("Starts", "0"), ("TOI/GP", "—"), ("Lineup", "Backup")], profile.Usage.Select(fact => (fact.Label, fact.Value)));
    }

    [Fact]
    public void AHealthyPlayerShowsNoInjuryAndAFullBar()
    {
        var session = GameTestData.StartSession();
        var player = session.ManagedTeam.Roster.First(candidate => candidate.Position != Position.Goalie);

        var profile = Profile(session, player.Id);

        Assert.Equal("Healthy", profile.Health);
        Assert.True(profile.IsHealthy);
        Assert.False(profile.HasInjuries);
        Assert.Null(profile.InjuredUntil);
        Assert.False(Assert.IsType<PlayerBannerViewModel>(profile.Banner).HasInjury);
        Assert.All(profile.OverviewRatings, rating =>
        {
            Assert.False(rating.HasLoss);
            Assert.Equal(rating.Healthy, rating.Value);
            Assert.Equal(string.Empty, rating.LossText);
        });
    }

    [Fact]
    public void APlayerPlayingHurtShowsTheirCurrentRatingsWithTheHealthyValueAndPointsLost()
    {
        var session = PlayingHurtSession.Value;
        var player = AllPlayers(session).First(candidate => candidate.Position != Position.Goalie && candidate.Injuries.Count > 0 && candidate.CanPlay);
        var expectedLoss = new Dictionary<Rating, int>();
        foreach (var injury in player.Injuries)
        {
            foreach (var (rating, points) in InjuryCatalogue.For(injury.Type).RatingReductions)
            {
                expectedLoss[rating] = expectedLoss.GetValueOrDefault(rating) + points;
            }
        }

        var profile = Profile(session, player.Id);

        Assert.Equal("Playing through injury", profile.Health);
        Assert.True(profile.IsPlayingHurt);
        Assert.False(profile.IsOut);
        var byName = profile.OverviewRatings.ToDictionary(rating => rating.Name);
        Assert.Equal(14, byName.Count);
        foreach (var rating in SkaterRatings)
        {
            var line = byName[PlayerDisplay.RatingName(rating)];
            var healthy = player.Ratings[rating];
            var current = Math.Max(0, healthy - expectedLoss.GetValueOrDefault(rating));
            Assert.Equal(healthy, line.Healthy);
            Assert.Equal(current, line.Value);
            Assert.Equal(healthy - current, line.Lost);
            Assert.Equal(line.Lost > 0, line.HasLoss);
            Assert.Equal(line.Lost > 0 ? $"−{line.Lost}" : string.Empty, line.LossText);
        }

        Assert.Contains(profile.OverviewRatings, rating => rating.HasLoss);
        Assert.Equal(player.Overall, profile.Overall);
        Assert.Equal(profile.OverviewRatings, profile.RatingGroups.SelectMany(group => group.Ratings));

        var badge = Assert.IsType<PlayerBannerViewModel>(profile.Banner).InjuryBadge;
        Assert.StartsWith("INJ · ", badge, StringComparison.Ordinal);
        Assert.EndsWith(" · Playing through", badge, StringComparison.Ordinal);
        Assert.False(Assert.IsType<PlayerBannerViewModel>(profile.Banner).IsOut);
        Assert.StartsWith("Injured until ", profile.InjuredUntil, StringComparison.Ordinal);
        Assert.Equal(InjuryDisplay.InjuredUntil(player.Injuries), profile.InjuredUntil);
    }

    [Fact]
    public void TheInjuredUntilLineGivesTheLatestReturnAsADateOrARange()
    {
        static InjurySnapshot Injury(DateOnly earliest, DateOnly latest) =>
            new(InjuryType.SprainedAnkle, BodyPart.Ankle, CanPlayThrough: true, new DateOnly(2026, 11, 10), new ExpectedReturn(earliest, latest));

        var nov20 = new DateOnly(2026, 11, 20);
        var nov23 = new DateOnly(2026, 11, 23);
        var nov26 = new DateOnly(2026, 11, 26);

        Assert.Null(InjuryDisplay.InjuredUntil([]));
        Assert.Equal("Injured until Mon 23 Nov", InjuryDisplay.InjuredUntil([Injury(nov23, nov23)]));
        Assert.Equal("Injured until Mon 23 Nov – Thu 26 Nov", InjuryDisplay.InjuredUntil([Injury(nov23, nov26)]));

        // With several injuries it follows the one that heals last.
        Assert.Equal("Injured until Mon 23 Nov – Thu 26 Nov", InjuryDisplay.InjuredUntil([Injury(nov20, nov20), Injury(nov23, nov26)]));
        Assert.Equal("Injured until Mon 23 Nov – Thu 26 Nov", InjuryDisplay.InjuredUntil([Injury(nov23, nov26), Injury(nov20, nov20)]));
    }

    [Fact]
    public void APlayerWhoIsOutShowsTheBadgeAndWhyTheyCannotPlay()
    {
        var session = OutSession.Value;
        var player = AllPlayers(session).First(candidate => !candidate.CanPlay);

        var profile = Profile(session, player.Id);

        Assert.Equal("Out injured", profile.Health);
        Assert.True(profile.IsOut);
        var banner = Assert.IsType<PlayerBannerViewModel>(profile.Banner);
        Assert.True(banner.IsOut);
        Assert.StartsWith("OUT · ", banner.InjuryBadge, StringComparison.Ordinal);
        Assert.Equal(InjuryDisplay.Summary(player), banner.InjurySummary);
        Assert.Equal(InjuryDisplay.InjuredUntil(player.Injuries), profile.InjuredUntil);
        var outInjury = profile.Injuries.First(injury => injury.IsOut);
        Assert.Equal("Out", outInjury.Status);
        Assert.Equal("Cannot play until healed", outInjury.Effect);
    }

    [Fact]
    public void TheHealthTabListsEachInjuryWithItsDetailsAndNothingHidden()
    {
        var session = PlayingHurtSession.Value;
        var player = AllPlayers(session).First(candidate => candidate.Injuries.Count > 0 && candidate.CanPlay);

        var profile = Profile(session, player.Id, PlayerProfileTab.Health);

        Assert.True(profile.ShowsHealth);
        Assert.Equal(player.Injuries.Count, profile.Injuries.Count);
        foreach (var (injury, line) in player.Injuries.Zip(profile.Injuries))
        {
            Assert.Equal(InjuryDisplay.Name(injury.Type), line.Name);
            Assert.Equal(injury.BodyPart.ToString(), line.BodyPart);
            Assert.Equal("Playing through", line.Status);
            Assert.Equal(injury.Date.ToString("ddd d MMM", CultureInfo.CurrentCulture), line.Suffered);
            Assert.Equal(InjuryDisplay.ExpectedReturn(injury.ExpectedReturn), line.ExpectedReturn);
            Assert.StartsWith("Plays at −", line.Effect, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheGameLogListsEveryPhaseNewestFirstEachLabelledAsTheScheduleLabelsIt()
    {
        var session = season.Load(season.SecondRound);
        var snapshot = session.Snapshot;
        var playoffs = snapshot.Season.Playoffs!;
        var statistics = playoffs.SkaterStatistics.Where(skater => skater.GamesPlayed >= 2).First();
        var team = session.GetTeam(statistics.TeamId);
        var regular = snapshot.Season.SkaterStatistics.Single(skater => skater.PlayerId == statistics.PlayerId);
        var preseason = snapshot.Season.PreseasonResults.Count(result => TeamSide(result, team.Id).Skaters.Any(skater => skater.PlayerId == statistics.PlayerId));

        var profile = Profile(session, statistics.PlayerId, PlayerProfileTab.GameLog);

        var rows = profile.GameLog;
        Assert.Equal(PlayerGameLog.SkaterColumns, profile.GameLogColumns);
        Assert.Equal(preseason + regular.GamesPlayed + statistics.GamesPlayed, rows.Count);
        Assert.Equal(statistics.GamesPlayed, rows.Count(row => Regex.IsMatch(row.PhaseLabel, @"^(R1|R2|CF|F) G\d$")));
        Assert.Equal(regular.GamesPlayed, rows.Count(row => row.PhaseLabel == "REG"));
        Assert.Equal(preseason, rows.Count(row => row.PhaseLabel == "PRE"));
        Assert.True(preseason > 0);

        // Newest first: the playoffs, then the regular season, then the preseason.
        Assert.Equal(rows.Select(row => row.Date).OrderDescending(), rows.Select(row => row.Date));
        Assert.Equal(rows.Select(row => row.Date).Distinct().Count(), rows.Count);
        var phases = rows.Select(row => row.PhaseLabel.Length > 3 ? "playoffs" : row.PhaseLabel).Distinct().ToList();
        Assert.Equal(["playoffs", "REG", "PRE"], phases);

        // The newest row is the player's latest playoff game.
        var newest = rows[0];
        var latest = playoffs.Results.Last(result => TeamSide(result, team.Id).Skaters.Any(skater => skater.PlayerId == statistics.PlayerId));
        Assert.Equal(latest.Date, newest.Date);
        Assert.Equal(team.Id, newest.TeamId);
        Assert.Equal(MatchDisplay.ResultFor(latest, team.Id), newest.Result);
        Assert.Equal(latest.WinnerId == team.Id, newest.IsWin);
        var opponent = latest.Home.TeamId == team.Id ? latest.Away.TeamId : latest.Home.TeamId;
        Assert.Equal($"{(latest.Home.TeamId == team.Id ? "vs" : "@")} {session.GetTeam(opponent).Name}", newest.Opponent);
        Assert.Equal(MatchDisplay.ShortDate(latest.Date), newest.DateLabel);
        var box = TeamSide(latest, team.Id).Skaters.Single(skater => skater.PlayerId == statistics.PlayerId);
        Assert.Equal(
            [
                Count(box.Goals), Count(box.Assists), Count(box.Points), MatchDisplay.PlusMinus(box.PlusMinus), Count(box.PenaltyMinutes),
                Count(box.Shots), MatchDisplay.ExpectedGoals(box.ExpectedGoals), Count(box.Hits), Count(box.BlockedShots), MatchDisplay.TimeOnIce(box.TimeOnIce),
            ],
            newest.Cells);
        Assert.Equal(PlayerGameLog.SkaterColumns.Count, newest.Cells.Count);

        // The last five games are the newest five.
        Assert.Equal(rows.Take(5), profile.LastFiveGames);
    }

    [Fact]
    public void AGoaliesGameLogShowsTheirStartsWithGoalieColumns()
    {
        var session = season.Load(season.SecondRound);
        var snapshot = session.Snapshot;
        var statistics = snapshot.Season.GoalieStatistics.Where(goalie => goalie.GamesPlayed >= 5).First();
        var team = session.GetTeam(statistics.TeamId);

        var profile = Profile(session, statistics.PlayerId, PlayerProfileTab.GameLog);

        Assert.Equal(["SA", "GA", "SV", "SV%", "xGA", "GSAx", "TOI"], profile.GameLogColumns);
        var playoffStarts = snapshot.Season.Playoffs!.GoalieStatistics.SingleOrDefault(goalie => goalie.PlayerId == statistics.PlayerId)?.GamesPlayed ?? 0;
        Assert.Equal(statistics.GamesPlayed, profile.GameLog.Count(row => row.PhaseLabel == "REG"));
        Assert.Equal(playoffStarts, profile.GameLog.Count(row => row.PhaseLabel.Length > 3));
        var newest = profile.GameLog[0];
        var result = snapshot.Season.Playoffs!.Results.Concat(snapshot.Season.Results).Concat(snapshot.Season.PreseasonResults)
            .Where(candidate => TeamSide(candidate, team.Id).Goalie.PlayerId == statistics.PlayerId)
            .OrderByDescending(candidate => candidate.Date)
            .First();
        var goalie = TeamSide(result, team.Id).Goalie;
        Assert.Equal(result.Date, newest.Date);
        Assert.Equal(
            [
                Count(goalie.ShotsAgainst), Count(goalie.GoalsAgainst), Count(goalie.Saves), MatchDisplay.SavePercentage(goalie.Saves, goalie.ShotsAgainst),
                MatchDisplay.ExpectedGoals(goalie.ExpectedGoalsAgainst), MatchDisplay.GoalsSavedAboveExpected(goalie.ExpectedGoalsAgainst - goalie.GoalsAgainst),
                MatchDisplay.TimeOnIce(goalie.TimeOnIce),
            ],
            newest.Cells);
    }

    [Fact]
    public void ChoosingAGameLogRowOpensItsBoxScoreInTheSchedule()
    {
        var session = season.Load(season.SecondRound);
        var shell = new GameShellViewModel(session);
        var playoffs = session.Snapshot.Season.Playoffs!;
        var statistics = playoffs.SkaterStatistics.First(skater => skater.GamesPlayed >= 2);
        shell.OpenPlayer(statistics.PlayerId, PlayerProfileTab.GameLog);
        var profile = shell.Profile!;
        var row = profile.GameLog[1];

        profile.SelectedGame = row;

        Assert.Equal(ShellPage.LeagueSchedule, shell.CurrentPageKind);
        Assert.Equal(statistics.TeamId, shell.Schedule.SelectedTeam.Id);
        Assert.Equal(row.Date, shell.Schedule.SelectedMatch!.Date);
        Assert.NotNull(shell.Schedule.SelectedResult);
        Assert.Null(profile.SelectedGame);

        // The managed team's games open in its own schedule.
        var managed = new GameShellViewModel(GameTestData.StartSession());
        var managedPlayer = managed.Session.ManagedTeam.Lineup.ForwardLines[0].CentreId;
        managed.OpenPlayer(managedPlayer, PlayerProfileTab.GameLog);
        managed.Profile!.SelectedGame = managed.Profile.GameLog[0];
        Assert.Equal(ShellPage.TeamSchedule, managed.CurrentPageKind);
        Assert.Equal("PRE", managed.Schedule.SelectedMatch!.PhaseLabel);
    }

    [Fact]
    public void UsageShowsPerGameTimeByStrengthWithPowerPlayAndPenaltyKillUnits()
    {
        var session = season.Load(season.SecondRound);
        var snapshot = session.Snapshot;
        var team = snapshot.League.Teams.First(candidate => snapshot.Season.SkaterStatistics.Any(skater => skater.TeamId == candidate.Id && skater.GamesPlayed > 30));
        var powerPlay = team.Lineup.UnitsFor(SpecialSituation.PowerPlay5On4);
        var penaltyKill = team.Lineup.UnitsFor(SpecialSituation.PenaltyKill4On5);
        SkaterSeasonStatisticsSnapshot Stats(PlayerId id) => snapshot.Season.SkaterStatistics.Single(skater => skater.PlayerId == id);

        // A power-play unit-one skater who also kills penalties on the second unit.
        var both = powerPlay[0].PlayerIds.Intersect(penaltyKill[1].PlayerIds).Concat(powerPlay[0].PlayerIds).First(id => Stats(id).GamesPlayed > 0);
        var perGame = Stats(both).TimeOnIceBySituationPerGame!;
        var usage = Profile(session, both).Usage.ToDictionary(fact => fact.Label, fact => fact.Value);

        Assert.Equal(["Even strength / GP", "Power play / GP", "Penalty kill / GP", "Line"], usage.Keys);
        Assert.Equal(MatchDisplay.TimeOnIce(perGame.FiveOnFive + perGame.Other), usage["Even strength / GP"]);
        Assert.Equal(WithTags(MatchDisplay.TimeOnIce(perGame.PowerPlay), PowerPlayTags(team.Lineup, both)), usage["Power play / GP"]);
        Assert.StartsWith($"{MatchDisplay.TimeOnIce(perGame.PowerPlay)} · PP1", usage["Power play / GP"], StringComparison.Ordinal);
        Assert.Equal(PlayerDisplay.LineupRole(both, team.Lineup), usage["Line"]);
        Assert.Equal(WithTags(MatchDisplay.TimeOnIce(perGame.PenaltyKill), PenaltyKillTags(team.Lineup, both)), usage["Penalty kill / GP"]);

        // A penalty-killing skater of the second unit who is not on a power-play unit.
        var killer = penaltyKill[1].PlayerIds.Except(penaltyKill[0].PlayerIds).Except(penaltyKill[2].PlayerIds)
            .First(id => Stats(id).GamesPlayed > 0);
        var killerUsage = Profile(session, killer).Usage.ToDictionary(fact => fact.Label, fact => fact.Value);
        Assert.StartsWith(
            $"{MatchDisplay.TimeOnIce(Stats(killer).TimeOnIceBySituationPerGame!.PenaltyKill)} · PK2",
            killerUsage["Penalty kill / GP"],
            StringComparison.Ordinal);

        // Every unit of the shorthanded and two-man-advantage situations is tagged too.
        foreach (var (situation, prefix, label) in new[]
        {
            (SpecialSituation.PowerPlay5On3, "5v3 U", "Power play / GP"),
            (SpecialSituation.PowerPlay4On3, "4v3 U", "Power play / GP"),
            (SpecialSituation.PenaltyKill3On5, "3v5 U", "Penalty kill / GP"),
            (SpecialSituation.PenaltyKill3On4, "3v4 U", "Penalty kill / GP"),
        })
        {
            var units = team.Lineup.UnitsFor(situation);
            for (var index = 0; index < units.Count; index++)
            {
                var member = units[index].PlayerIds.First(id => units.Take(index).All(earlier => !earlier.PlayerIds.Contains(id)));
                var tags = Profile(session, member).Usage.Single(fact => fact.Label == label).Value;
                Assert.Contains($"{prefix}{index + 1}", tags.Split(" · "));
            }
        }

        // A skater on no special unit shows the times alone.
        var specialUnits = new[]
        {
            SpecialSituation.PowerPlay5On4, SpecialSituation.PowerPlay5On3, SpecialSituation.PowerPlay4On3,
            SpecialSituation.PenaltyKill4On5, SpecialSituation.PenaltyKill3On5, SpecialSituation.PenaltyKill3On4,
        }.SelectMany(team.Lineup.UnitsFor).ToList();
        var neither = team.Roster.First(player => player.Position != Position.Goalie
            && Stats2(player.Id) is { GamesPlayed: > 0 }
            && !specialUnits.Any(unit => unit.PlayerIds.Contains(player.Id)));
        var plain = Profile(session, neither.Id).Usage.ToDictionary(fact => fact.Label, fact => fact.Value);
        Assert.DoesNotContain("·", plain["Power play / GP"], StringComparison.Ordinal);
        Assert.DoesNotContain("·", plain["Penalty kill / GP"], StringComparison.Ordinal);

        SkaterSeasonStatisticsSnapshot? Stats2(PlayerId id) => snapshot.Season.SkaterStatistics.SingleOrDefault(skater => skater.PlayerId == id);
    }

    [Fact]
    public void AGoaliesUsageShowsStartsTimeInNetAndTheirRole()
    {
        var session = season.Load(season.SecondRound);
        var snapshot = session.Snapshot;
        var statistics = snapshot.Season.GoalieStatistics.First(goalie => goalie.GamesPlayed >= 5);
        var team = session.GetTeam(statistics.TeamId);

        var usage = Profile(session, statistics.PlayerId).Usage;

        Assert.Equal(
            [
                ("Starts", Count(statistics.GamesPlayed)),
                ("TOI/GP", MatchDisplay.TimeOnIce(statistics.TimeOnIcePerGame)),
                ("Lineup", PlayerDisplay.LineupRole(statistics.PlayerId, team.Lineup)),
            ],
            usage.Select(fact => (fact.Label, fact.Value)));
    }

    [Fact]
    public void TheStatisticSwitchDrivesTheBannerOverviewAndStatisticsTogether()
    {
        var session = season.Load(season.SecondRound);
        var snapshot = session.Snapshot;
        var playoff = snapshot.Season.Playoffs!.SkaterStatistics.First(skater => skater.GamesPlayed >= 3);
        var regular = snapshot.Season.SkaterStatistics.Single(skater => skater.PlayerId == playoff.PlayerId);

        var profile = Profile(session, playoff.PlayerId);
        var banner = Assert.IsType<PlayerBannerViewModel>(profile.Banner);

        Assert.True(profile.HasPlayoffStatistics);
        Assert.True(profile.ShowsRegularSeason);
        Assert.Equal(Count(regular.Points), banner.Figures[1].Value);
        Assert.Equal(Count(regular.GamesPlayed), profile.OverviewStatisticGroups[0].Statistics[0].Value);

        profile.ShowStatisticsSetCommand.Execute(StatisticsSet.Playoffs);

        Assert.True(profile.ShowsPlayoffs);
        Assert.Equal(Count(playoff.Points), banner.Figures[1].Value);
        Assert.Equal(MatchDisplay.Percentage(playoff.OnIce.FiveOnFive.ExpectedGoalsPercentage), banner.Figures[2].Value);
        Assert.Equal(Count(playoff.GamesPlayed), profile.OverviewStatisticGroups[0].Statistics[0].Value);
        Assert.Equal(Count(playoff.GamesPlayed), profile.StatisticGroups[0].Statistics[0].Value);
        Assert.Equal("2026–27 statistics", profile.StatisticsTitle);

        profile.ShowStatisticsSetCommand.Execute(StatisticsSet.RegularSeason);
        Assert.Equal(Count(regular.Points), banner.Figures[1].Value);

        // A player without playoff games shows zeros and dashes.
        var missed = snapshot.League.Teams.First(team => !snapshot.Season.Playoffs!.Series.Any(series =>
            series.HigherRanked.TeamId == team.Id || series.LowerRanked.TeamId == team.Id));
        var none = Profile(session, missed.Lineup.ForwardLines[0].CentreId, statistics: StatisticsSet.Playoffs);
        Assert.True(none.ShowsPlayoffs);
        Assert.Equal("0", none.StatisticGroups[0].Statistics[0].Value);
        Assert.Equal("—", Assert.IsType<PlayerBannerViewModel>(none.Banner).Figures[2].Value);
    }

    [Fact]
    public void TheSwitchIsAbsentBeforeThePlayoffsAndAPlayoffRequestShowsTheRegularSeason()
    {
        var session = GameTestData.StartSession();
        var player = session.ManagedTeam.Lineup.ForwardLines[0].CentreId;

        var profile = Profile(session, player, statistics: StatisticsSet.Playoffs);

        Assert.False(profile.HasPlayoffStatistics);
        Assert.True(profile.ShowsRegularSeason);
        Assert.False(profile.ShowsPlayoffs);
        profile.ShowStatisticsSetCommand.Execute(StatisticsSet.Playoffs);
        Assert.True(profile.ShowsRegularSeason);
    }

    [Fact]
    public void ThePageSuppliesItsOwnBannerAndTabsAndKeepsTheSectionItWasOpenedFrom()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        var player = session.ManagedTeam.Lineup.ForwardLines[0].CentreId;
        Assert.False(shell.OpenPageCommand.CanExecute(ShellPage.PlayerProfile));
        Assert.Null(shell.Profile);

        shell.OpenPageCommand.Execute(ShellPage.Injuries);
        Assert.Equal(ShellSection.Team, shell.CurrentSection.Section);
        shell.OpenPlayer(player);

        var profile = Assert.IsType<PlayerProfilePageViewModel>(shell.CurrentPage);
        Assert.Same(profile, shell.Profile);
        Assert.Equal(ShellPage.PlayerProfile, shell.CurrentPageKind);
        Assert.Equal(ShellSection.Team, shell.CurrentSection.Section);
        Assert.Same(profile.Banner, shell.Banner);
        Assert.IsType<PlayerBannerViewModel>(shell.Banner);
        Assert.Equal(session.ManagedTeam.Name, profile.Subtitle);
        Assert.Equal(["Overview", "Ratings", "Statistics", "Game log", "Health", "Career"], shell.PageTabs.Select(tab => tab.Label));
        Assert.Equal("Overview", Assert.Single(shell.PageTabs, tab => tab.IsActive).Label);
        Assert.False(shell.PageTabs[5].Command.CanExecute(null));
        Assert.All(shell.PageTabs.Take(5), tab => Assert.True(tab.Command.CanExecute(null)));
        Assert.Single(shell.Sections.SelectMany(section => section.Items).Where(item => item.IsActive).DefaultIfEmpty());

        shell.PageTabs[3].Command.Execute(null);
        Assert.Equal(PlayerProfileTab.GameLog, profile.Tab);
        Assert.True(profile.ShowsGameLog);
        Assert.False(profile.ShowsOverview);
        Assert.Equal("Game log", Assert.Single(shell.PageTabs, tab => tab.IsActive).Label);
        shell.PageTabs[4].Command.Execute(null);
        Assert.True(profile.ShowsHealth);
        shell.PageTabs[1].Command.Execute(null);
        Assert.True(profile.ShowsRatings);
        shell.PageTabs[2].Command.Execute(null);
        Assert.True(profile.ShowsStatistics);

        // Leaving the page restores the team banner and the section's own tabs.
        shell.OpenPageCommand.Execute(ShellPage.Roster);
        Assert.IsType<TeamBannerViewModel>(shell.Banner);
        Assert.Equal(["Roster", "Lines", "Schedule", "Team statistics", "Injuries"], shell.PageTabs.Select(tab => tab.Label));
    }

    [Fact]
    public void AProfileOpenedFromHomeKeepsTheHomeSection()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);

        shell.OpenPlayer(session.ManagedTeam.Lineup.StartingGoalieId);

        Assert.True(shell.IsHomeSection);
        Assert.Equal(ShellPage.PlayerProfile, shell.CurrentPageKind);
    }

    [Fact]
    public async Task ARefreshAfterADayRebuildsTheSamePlayerKeepingTheTab()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        var player = session.ManagedTeam.Lineup.ForwardLines[0].CentreId;
        shell.OpenPlayer(player, PlayerProfileTab.Statistics);
        var profile = shell.Profile!;
        var before = profile.GameLog.Count;
        Assert.Equal("0", profile.StatisticGroups[0].Statistics[0].Value);

        await shell.AdvanceDayCommand.ExecuteAsync(null);

        Assert.Same(profile, shell.Profile);
        Assert.Same(profile, shell.CurrentPage);
        Assert.Equal(player, profile.Id);
        Assert.Equal(PlayerProfileTab.Statistics, profile.Tab);
        Assert.Equal(StatisticsSet.RegularSeason, profile.Statistics);
        Assert.Equal("1", profile.StatisticGroups[0].Statistics[0].Value);
        Assert.Equal(before + 1, profile.GameLog.Count);
        Assert.Equal("REG", profile.GameLog[0].PhaseLabel);
    }

    [Fact]
    public void ARefreshKeepsThePlayoffStatisticSet()
    {
        var session = season.Load(season.SecondRound);
        var playoff = session.Snapshot.Season.Playoffs!.SkaterStatistics.First(skater => skater.GamesPlayed >= 3);
        var profile = Profile(session, playoff.PlayerId, PlayerProfileTab.Ratings, StatisticsSet.Playoffs);

        profile.Refresh();

        Assert.Equal(StatisticsSet.Playoffs, profile.Statistics);
        Assert.Equal(PlayerProfileTab.Ratings, profile.Tab);
        Assert.Equal(Count(playoff.GamesPlayed), profile.StatisticGroups[0].Statistics[0].Value);
    }

    [Fact]
    public void OpeningAPlayerDefaultsToTheStatisticSetTheHomePageShowsForTheirTeam()
    {
        var session = season.Load(season.SecondRound);
        var shell = new GameShellViewModel(session);
        var playoffs = session.Snapshot.Season.Playoffs!;
        var qualifier = playoffs.SkaterStatistics.First(skater => skater.GamesPlayed >= 1);
        var missed = session.Snapshot.League.Teams.First(team => !playoffs.Series.Any(series =>
            series.HigherRanked.TeamId == team.Id || series.LowerRanked.TeamId == team.Id));

        shell.OpenPlayer(qualifier.PlayerId);
        Assert.Equal(StatisticsSet.Playoffs, shell.Profile!.Statistics);

        shell.OpenPlayer(missed.Lineup.ForwardLines[0].CentreId);
        Assert.Equal(StatisticsSet.RegularSeason, shell.Profile!.Statistics);

        shell.OpenPlayer(qualifier.PlayerId, PlayerProfileTab.Health, StatisticsSet.RegularSeason);
        Assert.Equal(StatisticsSet.RegularSeason, shell.Profile!.Statistics);
        Assert.Equal(PlayerProfileTab.Health, shell.Profile.Tab);
    }

    [Fact]
    public void TheRosterStripOpensTheProfileAndTheGameLogWithTheRostersStatisticSet()
    {
        var session = season.Load(season.SecondRound);
        var shell = new GameShellViewModel(session);
        var playoffs = session.Snapshot.Season.Playoffs!;
        var skater = playoffs.SkaterStatistics.First(statistics => statistics.GamesPlayed >= 2);
        shell.OpenTeam(skater.TeamId);
        var roster = shell.TeamPage!.Roster;
        roster.SelectPlayer(skater.PlayerId);

        Assert.True(roster.OpenProfileCommand.CanExecute(null));
        Assert.True(roster.OpenGameLogCommand.CanExecute(null));
        roster.OpenProfileCommand.Execute(null);

        var profile = Assert.IsType<PlayerProfilePageViewModel>(shell.CurrentPage);
        Assert.Equal(skater.PlayerId, profile.Id);
        Assert.Equal(PlayerProfileTab.Overview, profile.Tab);
        Assert.Equal(StatisticsSet.RegularSeason, profile.Statistics);

        // The profile of another team's player wears that team's colours, and its team link opens the team.
        var team = session.GetTeam(skater.TeamId);
        var banner = Assert.IsType<PlayerBannerViewModel>(shell.Banner);
        Assert.Equal(team.Colours.Primary, banner.Colours.Primary);
        Assert.NotEqual(session.ManagedTeam.Colours.Primary, banner.Colours.Primary);
        shell.OpenPageCommand.Execute(ShellPage.Standings);
        banner.OpenTeamCommand.Execute(null);
        Assert.Equal(ShellPage.TeamPage, shell.CurrentPageKind);
        Assert.Equal(team.Id, shell.TeamPage!.Id);

        shell.TeamPage!.Roster.ShowStatisticsSetCommand.Execute(StatisticsSet.Playoffs);
        shell.TeamPage!.Roster.OpenGameLogCommand.Execute(null);

        profile = Assert.IsType<PlayerProfilePageViewModel>(shell.CurrentPage);
        Assert.Equal(PlayerProfileTab.GameLog, profile.Tab);
        Assert.Equal(StatisticsSet.Playoffs, profile.Statistics);
        Assert.Equal(shell.TeamPage!.Roster.SelectedStrip!.PlayerId, profile.Id);
    }

    [Fact]
    public void TheManagedRostersLinksOpenTheSelectedPlayer()
    {
        var session = GameTestData.StartSession();
        var shell = new GameShellViewModel(session);
        shell.OpenPageCommand.Execute(ShellPage.Roster);
        var roster = shell.Roster.Roster;
        var goalie = roster.Goalies[1].Player.Id;
        roster.SelectPlayer(goalie);

        roster.OpenGameLogCommand.Execute(null);

        Assert.Equal(ShellPage.PlayerProfile, shell.CurrentPageKind);
        Assert.Equal(goalie, shell.Profile!.Id);
        Assert.Equal(PlayerProfileTab.GameLog, shell.Profile.Tab);
        Assert.Equal(ShellSection.Team, shell.CurrentSection.Section);
    }

    [Fact]
    public void TheRosterLinksNeedAPageToOpenOn()
    {
        var session = GameTestData.StartSession();
        var roster = new TeamRosterViewModel(session, session.ManagedTeam.Id);

        Assert.False(roster.OpenProfileCommand.CanExecute(null));
        Assert.False(roster.OpenGameLogCommand.CanExecute(null));
    }

    private static GameSession SessionWhere(Func<PlayerSnapshot, bool> condition)
    {
        var manager = new GameManager();
        GameTestData.StartSession(manager);
        for (var day = 0; day < MaximumDays; day++)
        {
            if (manager.GetSnapshot().League.Teams.SelectMany(team => team.Roster).Any(condition))
            {
                return new GameSession(manager, "Profiles");
            }

            manager.AdvanceDayReplacingInjured();
        }

        throw new InvalidOperationException("No player ever had the injury the test needs.");
    }

    private static IEnumerable<PlayerSnapshot> AllPlayers(GameSession session) =>
        session.Snapshot.League.Teams.SelectMany(team => team.Roster);

    private static readonly Rating[] SkaterRatings =
    [
        Rating.Skating, Rating.PuckControl, Rating.Passing, Rating.ShotPower, Rating.ShotAccuracy, Rating.OffensiveAwareness,
        Rating.DefensiveAwareness, Rating.Checking, Rating.ShotBlocking, Rating.StickChecking,
        Rating.Faceoffs, Rating.Stamina, Rating.Toughness, Rating.Discipline,
    ];

    private static IEnumerable<string> Labels(SeasonStatGroupViewModel group) => group.Statistics.Select(stat => stat.Label);

    private static int RatingValue(PlayerProfilePageViewModel profile, string name) =>
        profile.RatingGroups.SelectMany(group => group.Ratings).Single(rating => rating.Name == name).Value;

    private static CompletedMatchTeamSnapshot TeamSide(CompletedMatchSnapshot result, TeamId teamId) =>
        result.Home.TeamId == teamId ? result.Home : result.Away;

    private static string Count(int value) => value.ToString(CultureInfo.CurrentCulture);

    private static string WithTags(string time, IReadOnlyList<string> tags) =>
        tags.Count == 0 ? time : $"{time} · {string.Join(" · ", tags)}";

    private static List<string> PowerPlayTags(LineupSnapshot lineup, PlayerId playerId) =>
        Tags(lineup, playerId, (SpecialSituation.PowerPlay5On4, "PP"), (SpecialSituation.PowerPlay5On3, "5v3 U"), (SpecialSituation.PowerPlay4On3, "4v3 U"));

    private static List<string> PenaltyKillTags(LineupSnapshot lineup, PlayerId playerId) =>
        Tags(lineup, playerId, (SpecialSituation.PenaltyKill4On5, "PK"), (SpecialSituation.PenaltyKill3On5, "3v5 U"), (SpecialSituation.PenaltyKill3On4, "3v4 U"));

    /// <summary>The player's first unit in each situation, numbered from one, in the order given.</summary>
    private static List<string> Tags(LineupSnapshot lineup, PlayerId playerId, params (SpecialSituation Situation, string Prefix)[] situations) =>
        situations
            .Select(entry => (entry.Prefix, Index: lineup.UnitsFor(entry.Situation).ToList().FindIndex(unit => unit.PlayerIds.Contains(playerId))))
            .Where(entry => entry.Index >= 0)
            .Select(entry => $"{entry.Prefix}{entry.Index + 1}")
            .ToList();
}