namespace HockeySim.Management.NewGame;

internal static class FictionalLeagueData
{
    public static IReadOnlyList<ConferenceDefinition> Conferences { get; } =
    [
        new(
            "Eastern Conference",
            [
                new(
                    "Northern Division",
                    [
                        "Halifax Mariners",
                        "Quebec Citadels",
                        "Montreal Voyageurs",
                        "Ottawa Owls",
                        "Toronto Towers",
                        "Boston Beacons",
                        "New York Comets",
                        "Buffalo Forge",
                    ]),
                new(
                    "Southern Division",
                    [
                        "Philadelphia Foundry",
                        "Baltimore Clippers",
                        "Washington Federals",
                        "Pittsburgh Riveters",
                        "Raleigh Redtails",
                        "Atlanta Firebirds",
                        "Nashville Notes",
                        "Miami Breakers",
                    ]),
            ]),
        new(
            "Western Conference",
            [
                new(
                    "Central Division",
                    [
                        "Chicago Zephyrs",
                        "Detroit Motors",
                        "Minneapolis Freeze",
                        "Milwaukee Stags",
                        "Winnipeg Wolves",
                        "Regina Prairie",
                        "Calgary Peaks",
                        "Edmonton Aurora",
                    ]),
                new(
                    "Pacific Division",
                    [
                        "Vancouver Orcas",
                        "Seattle Evergreens",
                        "Portland Pioneers",
                        "San Francisco Fog",
                        "Los Angeles Stars",
                        "San Diego Surf",
                        "Las Vegas Scorpions",
                        "Phoenix Roadrunners",
                    ]),
            ]),
    ];

    internal sealed record ConferenceDefinition(
        string Name,
        IReadOnlyList<DivisionDefinition> Divisions);

    internal sealed record DivisionDefinition(
        string Name,
        IReadOnlyList<string> TeamNames);
}