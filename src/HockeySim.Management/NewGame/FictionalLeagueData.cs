using HockeySim.Domain;

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
                        new("Halifax Mariners", "#0B2545", "#3FA7A3"),
                        new("Quebec Citadels", "#1F4FA0", "#E4E8EE"),
                        new("Montreal Voyageurs", "#A6192E", "#EADBC8"),
                        new("Ottawa Owls", "#6B4226", "#E9C46A"),
                        new("Toronto Towers", "#00205B", "#A7B1BC"),
                        new("Boston Beacons", "#0E4D2F", "#F7A35C"),
                        new("New York Comets", "#1B3A8C", "#F79A4E"),
                        new("Buffalo Forge", "#5A1A2B", "#9FA8B3"),
                    ]),
                new(
                    "Southern Division",
                    [
                        new("Philadelphia Foundry", "#1A1A1A", "#F26B21"),
                        new("Baltimore Clippers", "#3B1F6B", "#E0B341"),
                        new("Washington Federals", "#0A2240", "#E8E3D6"),
                        new("Pittsburgh Riveters", "#111111", "#FFC72C"),
                        new("Raleigh Redtails", "#9E2A2B", "#F4C095"),
                        new("Atlanta Firebirds", "#7A1915", "#F5A623"),
                        new("Nashville Notes", "#F2B705", "#041E42"),
                        new("Miami Breakers", "#004E55", "#FFA494"),
                    ]),
            ]),
        new(
            "Western Conference",
            [
                new(
                    "Central Division",
                    [
                        new("Chicago Zephyrs", "#13294B", "#6CACE4"),
                        new("Detroit Motors", "#C8102E", "#F2F2F2"),
                        new("Minneapolis Freeze", "#24597E", "#CFE8F5"),
                        new("Milwaukee Stags", "#1E4D2B", "#E6D3A3"),
                        new("Winnipeg Wolves", "#3D4450", "#8EC9E8"),
                        new("Regina Prairie", "#255725", "#EBD27A"),
                        new("Calgary Peaks", "#1C3D5A", "#F1BE48"),
                        new("Edmonton Aurora", "#0B1F4B", "#3DDC97"),
                    ]),
                new(
                    "Pacific Division",
                    [
                        new("Vancouver Orcas", "#121417", "#2E9CCA"),
                        new("Seattle Evergreens", "#0B4F3C", "#99D9C1"),
                        new("Portland Pioneers", "#8B2F1F", "#F0E2C4"),
                        new("San Francisco Fog", "#A82E25", "#E3E6E9"),
                        new("Los Angeles Stars", "#24135F", "#C5CBD3"),
                        new("San Diego Surf", "#005F86", "#F5D59A"),
                        new("Las Vegas Scorpions", "#2A2A2A", "#B9975B"),
                        new("Phoenix Roadrunners", "#973F29", "#F3D9A4"),
                    ]),
            ]),
    ];

    internal sealed record ConferenceDefinition(
        string Name,
        IReadOnlyList<DivisionDefinition> Divisions);

    internal sealed record DivisionDefinition(
        string Name,
        IReadOnlyList<TeamDefinition> Teams)
    {
        public IEnumerable<string> TeamNames => Teams.Select(team => team.Name);
    }

    /// <param name="PrimaryColour">The leading identity colour, written as #RRGGBB.</param>
    /// <param name="SecondaryColour">
    /// The accent, written as #RRGGBB. Desktop sets primary-coloured text on it, so each pair
    /// keeps at least the 4.5:1 contrast that WCAG asks of body text.
    /// </param>
    internal sealed record TeamDefinition(string Name, string PrimaryColour, string SecondaryColour)
    {
        public TeamColours Colours => new(Colour.FromHex(PrimaryColour), Colour.FromHex(SecondaryColour));
    }
}