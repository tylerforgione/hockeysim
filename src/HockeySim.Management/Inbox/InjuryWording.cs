using System.Globalization;

using HockeySim.Domain;

namespace HockeySim.Management.Inbox;

/// <summary>
/// The head trainer's phrasing: several ways to say each thing, by injury, cause, and severity, so a
/// season's reports do not repeat one another. A phrasing is chosen by hashing what the report is
/// about, never from the game's random state, so the same game always writes the same words.
/// </summary>
internal static class InjuryWording
{
    // FNV-1a: stable across processes and platforms, unlike string.GetHashCode.
    private const uint FnvOffsetBasis = 2166136261;
    private const uint FnvPrime = 16777619;

    /// <summary>How the injury happened, naming the player and the injury.</summary>
    public static string HowItHappened(Player player, MatchInjury injury, Team opponent, DateOnly date)
    {
        var who = FullName(player);
        var what = WithArticle(injury.Type);
        var where = $"against the {opponent.Name} on {FormatDate(date)}";
        string[] phrasings = injury.Cause switch
        {
            InjuryCause.Hit =>
            [
                $"{who} took a heavy hit {where} and came away with {what}.",
                $"{who} was caught by a check {where}. The diagnosis is {what}.",
                $"{who} went down after a hit {where}: {what}.",
            ],
            InjuryCause.Collision =>
            [
                $"{who} came off worse delivering a check {where} and has {what}.",
                $"{who} finished a hit {where} and paid for it with {what}.",
                $"{who} was hurt throwing a check {where}: {what}.",
            ],
            InjuryCause.BlockedShot =>
            [
                $"{who} blocked a shot {where} and has {what}.",
                $"{who} stepped in front of a shot {where}. The result is {what}.",
                $"{who} got in the way of a point shot {where}: {what}.",
            ],
            InjuryCause.Fight =>
            [
                $"{who} came out of a fight {where} with {what}.",
                $"{who} dropped the gloves {where} and has {what} to show for it.",
                $"{who} was hurt in a fight {where}: {what}.",
            ],
            _ =>
            [
                $"{who} felt something give mid-stride {where}. It is {what}.",
                $"{who} pulled up late in a shift {where} with {what}.",
                $"{who} came off the ice {where} with {what}. No contact on the play.",
            ],
        };

        return Pick(phrasings, player.Id, date, injury.Type, salt: 1);
    }

    /// <summary>A note on this kind of injury, for injuries that keep a player out.</summary>
    public static string? AboutTheInjury(Player player, InjuryType type, DateOnly date)
    {
        var name = player.LastName;
        string[] phrasings = type switch
        {
            InjuryType.Concussion =>
            [
                $"{name} has entered concussion protocol.",
                $"{name} is resting in a dark room and will be reassessed daily.",
            ],
            InjuryType.SeparatedShoulder =>
            [
                $"{name}'s arm is in a sling for now.",
                $"{name} cannot lift the arm above the shoulder yet.",
            ],
            InjuryType.BrokenHand =>
            [
                $"{name}'s hand is in a cast.",
                $"{name} cannot grip a stick until the bone knits.",
            ],
            InjuryType.GroinStrain =>
            [
                $"{name} will rehab off the ice before skating again.",
                $"We will bring {name} back slowly so it does not flare up.",
            ],
            InjuryType.SprainedKnee =>
            [
                $"{name}'s knee is braced and will be checked again in a few days.",
                $"The scan shows ligament damage but nothing torn through.",
            ],
            InjuryType.HighAnkleSprain =>
            [
                $"{name} is in a walking boot.",
                $"High ankle sprains are slow. {name} cannot push off yet.",
            ],
            InjuryType.BrokenFoot =>
            [
                $"{name}'s foot is in a boot.",
                $"{name} cannot put weight on the foot yet.",
            ],
            _ => [],
        };

        return phrasings.Length == 0 ? null : Pick(phrasings, player.Id, date, type, salt: 2);
    }

    /// <summary>How long the injury will take to heal, judged from where its recovery time falls in its range.</summary>
    public static string HowSerious(Player player, MatchInjury injury, DateOnly date)
    {
        var name = player.LastName;
        string[] phrasings = SeverityOf(injury) switch
        {
            Severity.Mild =>
            [
                "It does not look serious.",
                $"We caught it early. {name} should not be out long.",
                "The good news is that it is a mild one.",
            ],
            Severity.Moderate =>
            [
                $"{name} will miss some time.",
                "It is a moderate case.",
                $"Expect {name} to be out for a while.",
            ],
            _ =>
            [
                "This is a bad one.",
                $"It is about as severe as these get. {name} will be out for an extended stretch.",
                $"We are looking at a long recovery for {name}.",
            ],
        };

        return Pick(phrasings, player.Id, date, injury.Type, salt: 3);
    }

    /// <summary>A player's recovery, naming the injury.</summary>
    public static string Recovered(Player player, Injury injury)
    {
        var who = FullName(player);
        var what = InjuryName(injury.Type);
        string[] phrasings =
        [
            $"{who} has recovered from the {what}.",
            $"{who} is over the {what}.",
            $"{who} has been given the all-clear on the {what}.",
        ];

        return Pick(phrasings, player.Id, injury.ReturnDate, injury.Type, salt: 4);
    }

    /// <summary>Whether a recovered player can play, or is still out with another injury.</summary>
    public static string Availability(Player player, Injury injury, bool canPlay)
    {
        var name = player.LastName;
        string[] phrasings = canPlay
            ? [$"{name} is cleared to play.", $"{name} is available for our next match.", $"{name} is ready to go."]
            : [$"{name} is still out with another injury.", $"{name} remains out with another injury."];

        return Pick(phrasings, player.Id, injury.ReturnDate, injury.Type, salt: 5);
    }

    /// <summary>The opening line of a weekly health report.</summary>
    public static string WeeklyOpening(DateOnly weekStart)
    {
        string[] phrasings =
        [
            "Here is where the team's health stands.",
            "This week's health update.",
            "The week from the training room.",
        ];

        return phrasings[Hash(weekStart.DayNumber, salt: 6) % (uint)phrasings.Length];
    }

    public static string InjuryName(InjuryType type) => type switch
    {
        InjuryType.Concussion => "concussion",
        InjuryType.BrokenNose => "broken nose",
        InjuryType.SeparatedShoulder => "separated shoulder",
        InjuryType.BruisedShoulder => "bruised shoulder",
        InjuryType.BrokenHand => "broken hand",
        InjuryType.BrokenFinger => "broken finger",
        InjuryType.BruisedRibs => "bruised ribs",
        InjuryType.BackSpasms => "back spasms",
        InjuryType.GroinStrain => "groin strain",
        InjuryType.TightGroin => "tight groin",
        InjuryType.SprainedKnee => "sprained knee",
        InjuryType.BruisedKnee => "bruised knee",
        InjuryType.HighAnkleSprain => "high ankle sprain",
        InjuryType.SprainedAnkle => "sprained ankle",
        InjuryType.BrokenFoot => "broken foot",
        InjuryType.BruisedFoot => "bruised foot",
        _ => throw new ArgumentOutOfRangeException(nameof(type), "The injury type is not recognised."),
    };

    /// <summary>How an injury was suffered, to follow its name: "bruised ribs from a hit".</summary>
    public static string HowSuffered(InjuryCause cause, Position position) => cause switch
    {
        InjuryCause.Hit => "from a hit",
        InjuryCause.Collision => "from throwing a hit",
        InjuryCause.BlockedShot => "from blocking a shot",
        InjuryCause.Fight => "in a fight",
        InjuryCause.Strain => position == Position.Goalie ? "making a save" : "while skating",
        _ => throw new ArgumentOutOfRangeException(nameof(cause), "The injury cause is not recognised."),
    };

    public static string FormatPlayer(Player player) =>
        $"{player.FirstName} {player.LastName} (#{player.Number}, {player.Position})";

    public static string FormatDate(DateOnly date) => date.ToString("MMMM d", CultureInfo.InvariantCulture);

    private static string FullName(Player player) => $"{player.FirstName} {player.LastName}";

    // Plural injuries read without an article: "has bruised ribs".
    private static string WithArticle(InjuryType type) => type switch
    {
        InjuryType.BruisedRibs or InjuryType.BackSpasms => InjuryName(type),
        _ => $"a {InjuryName(type)}",
    };

    private static Severity SeverityOf(MatchInjury injury)
    {
        var definition = injury.Definition;
        var span = definition.MaximumRecoveryDays - definition.MinimumRecoveryDays;
        var position = span == 0 ? 0 : (injury.RecoveryDays - definition.MinimumRecoveryDays) / (double)span;
        return position switch
        {
            < 1.0 / 3 => Severity.Mild,
            < 2.0 / 3 => Severity.Moderate,
            _ => Severity.Severe,
        };
    }

    private static string Pick(string[] phrasings, PlayerId player, DateOnly date, InjuryType type, int salt) =>
        phrasings[Hash(player, date, type, salt) % (uint)phrasings.Length];

    private static uint Hash(PlayerId player, DateOnly date, InjuryType type, int salt)
    {
        var hash = Hash(date.DayNumber, salt);
        foreach (var value in player.Value.ToByteArray())
        {
            hash = Mix(hash, value);
        }

        return Mix(hash, (int)type);
    }

    private static uint Hash(int dayNumber, int salt) => Mix(Mix(FnvOffsetBasis, dayNumber), salt);

    private static uint Mix(uint hash, int value)
    {
        foreach (var octet in BitConverter.GetBytes(value))
        {
            hash = unchecked((hash ^ octet) * FnvPrime);
        }

        return hash;
    }

    private enum Severity
    {
        Mild,
        Moderate,
        Severe,
    }
}