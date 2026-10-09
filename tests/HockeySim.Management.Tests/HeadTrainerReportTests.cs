using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;
using HockeySim.Management.Inbox;

using Xunit;

using static HockeySim.Management.Tests.SeasonAdvancementTests;

namespace HockeySim.Management.Tests;

/// <summary>
/// The head trainer's reports over a whole season: how many there are, how their wording varies,
/// and that the same game always writes the same words. The season is played once and shared.
/// </summary>
public sealed class HeadTrainerReportTests(HeadTrainerReportTests.PlayedSeason season)
    : IClassFixture<HeadTrainerReportTests.PlayedSeason>
{
    private const string Bullet = "  • ";

    [Fact]
    public void ASeasonsReportsDoNotFloodTheInbox()
    {
        var reports = season.TrainerReports;

        // About twenty injuries a season keep a player out, each with a report and a recovery, and
        // a health report arrives at most once a week.
        Assert.InRange(reports.Count, 10, 100);
        Assert.InRange(reports.Count(report => report.Subject.StartsWith("Health report:", StringComparison.Ordinal)), 1, 27);
    }

    [Fact]
    public void InjuryReportsDescribeHowInjuriesHappenedInSeveralWays()
    {
        // Every account of how an injury happened names the player, then says what happened,
        // then where: "against the ...".
        var phrasings = season.TrainerReports
            .Where(report => report.Subject.StartsWith("Injury: ", StringComparison.Ordinal))
            .Select(report =>
            {
                var player = report.Subject["Injury: ".Length..];
                var body = report.Body[(report.Body.IndexOf(player, StringComparison.Ordinal) + player.Length)..];
                return body[..body.IndexOf(" against the ", StringComparison.Ordinal)];
            })
            .ToList();

        Assert.NotEmpty(phrasings);
        Assert.True(phrasings.Distinct().Count() >= Math.Min(4, phrasings.Count), string.Join(" | ", phrasings));
    }

    [Fact]
    public void RecoveryReportsVaryInWording()
    {
        var openings = season.TrainerReports
            .Where(report => report.Subject.StartsWith("Recovered: ", StringComparison.Ordinal))
            .Select(report =>
            {
                var player = report.Subject["Recovered: ".Length..];
                var opening = report.Body.Split('\n')[0];
                return opening[(opening.IndexOf(player, StringComparison.Ordinal) + player.Length)..opening.LastIndexOf(" the ", StringComparison.Ordinal)];
            })
            .ToList();

        Assert.True(openings.Distinct().Count() >= Math.Min(2, openings.Count), string.Join(" | ", openings));
    }

    [Fact]
    public void ReportsCoverOnlyTheManagedTeam()
    {
        var managedNames = ManagedTeam(season.Snapshot).Roster
            .Select(player => $"{player.FirstName} {player.LastName}")
            .ToHashSet();

        Assert.All(season.TrainerReports, report =>
        {
            if (report.Subject.StartsWith("Health report:", StringComparison.Ordinal))
            {
                var listed = report.Body.Split('\n')
                    .Where(line => line.StartsWith(Bullet, StringComparison.Ordinal))
                    .Select(line => line[Bullet.Length..line.IndexOf(" (#", StringComparison.Ordinal)])
                    .ToList();
                Assert.NotEmpty(listed);
                Assert.All(listed, name => Assert.Contains(name, managedNames));
            }
            else
            {
                Assert.Contains(report.Subject[(report.Subject.IndexOf(':') + 2)..], managedNames);
            }
        });
    }

    [Fact]
    public void TheSameGameWritesTheSameReportsAfterBeingSavedAndLoaded()
    {
        var uninterrupted = new GameManager();
        StartGame(uninterrupted);
        var interrupted = new GameManager();
        StartGame(interrupted);
        for (var day = 0; day < 20; day++)
        {
            uninterrupted.AdvanceDayReplacingInjured();
            interrupted.AdvanceDayReplacingInjured();
        }

        var store = new MemorySaveStore();
        interrupted.SaveGame(store);
        var loaded = new GameManager();
        loaded.LoadGame(store);
        GameSnapshot expected = uninterrupted.GetSnapshot();
        GameSnapshot actual = loaded.GetSnapshot();
        for (var day = 0; day < 40; day++)
        {
            expected = uninterrupted.AdvanceDayReplacingInjured();
            actual = loaded.AdvanceDayReplacingInjured();
        }

        Assert.Equal(Describe(expected), Describe(actual));
        Assert.Contains(expected.Inbox, report => report.SenderRole == InboxSenderRole.HeadTrainer);
    }

    private static List<(string Subject, string Body)> Describe(GameSnapshot snapshot) =>
        snapshot.Inbox.Select(message => (message.Subject, message.Body)).ToList();

    public sealed class PlayedSeason
    {
        // Far more than the calendar needs, so a season that never completes fails instead of hanging.
        private const int MaximumAdvances = 366;

        public PlayedSeason()
        {
            var manager = new GameManager();
            Snapshot = StartGame(manager);
            for (var advances = 0; !Snapshot.Season.IsComplete && advances < MaximumAdvances; advances++)
            {
                Snapshot = manager.AdvanceDayReplacingInjured();
            }

            TrainerReports = Snapshot.Inbox.Where(message => message.SenderRole == InboxSenderRole.HeadTrainer).ToList();
        }

        public GameSnapshot Snapshot { get; }

        public IReadOnlyList<InboxMessageSnapshot> TrainerReports { get; }
    }
}