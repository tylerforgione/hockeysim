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

    public static IReadOnlyList<string> FirstNames { get; } =
    [
        "Adam", "Alex", "Andre", "Anton", "Ben", "Caleb", "Carter", "Cole",
        "Daniel", "Elias", "Emil", "Ethan", "Felix", "Gabriel", "Henrik", "Isaac",
        "Jack", "Jonah", "Julien", "Kai", "Leo", "Liam", "Logan", "Lucas",
        "Marek", "Mason", "Nathan", "Noah", "Oliver", "Owen", "Sam", "Theo",
    ];

    public static IReadOnlyList<string> LastNames { get; } =
    [
        "Andersson", "Bennett", "Bouchard", "Campbell", "Caron", "Chen", "Clarke", "Dubois",
        "Eriksson", "Fischer", "Gallagher", "Garcia", "Hansen", "Hughes", "Ivanov", "Johnson",
        "Keller", "Kim", "Larsson", "Lee", "Lefebvre", "Martin", "Miller", "Nakamura",
        "Novak", "Olsen", "Patel", "Petrov", "Roy", "Smith", "Sullivan", "Wilson",
    ];

    internal sealed record ConferenceDefinition(
        string Name,
        IReadOnlyList<DivisionDefinition> Divisions);

    internal sealed record DivisionDefinition(
        string Name,
        IReadOnlyList<string> TeamNames);
}