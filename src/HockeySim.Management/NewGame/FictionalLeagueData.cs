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
                        new("Halifax Mariners", "Harbour Lights Arena", "#0B2545", "#3FA7A3"),
                        new("Quebec Citadels", "Rampart Gardens", "#1F4FA0", "#E4E8EE"),
                        new("Montreal Voyageurs", "Saint-Laurent Pavilion", "#A6192E", "#EADBC8"),
                        new("Ottawa Owls", "Rideau Canal Arena", "#6B4226", "#E9C46A"),
                        new("Toronto Towers", "Lakeshore Gardens", "#00205B", "#A7B1BC"),
                        new("Boston Beacons", "Beacon Hill Arena", "#0E4D2F", "#F7A35C"),
                        new("New York Comets", "Empire Pavilion", "#1B3A8C", "#F79A4E"),
                        new("Buffalo Forge", "Niagara Ironworks Arena", "#5A1A2B", "#9FA8B3"),
                    ]),
                new(
                    "Southern Division",
                    [
                        new("Philadelphia Foundry", "Schuylkill Arena", "#1A1A1A", "#F26B21"),
                        new("Baltimore Clippers", "Inner Harbor Arena", "#3B1F6B", "#E0B341"),
                        new("Washington Federals", "Potomac Centre", "#0A2240", "#E8E3D6"),
                        new("Pittsburgh Riveters", "Allegheny Ice Palace", "#111111", "#FFC72C"),
                        new("Raleigh Redtails", "Oak City Arena", "#9E2A2B", "#F4C095"),
                        new("Atlanta Firebirds", "Peachtree Coliseum", "#7A1915", "#F5A623"),
                        new("Nashville Notes", "Music Row Arena", "#F2B705", "#041E42"),
                        new("Miami Breakers", "Biscayne Bay Arena", "#004E55", "#FFA494"),
                    ]),
            ]),
        new(
            "Western Conference",
            [
                new(
                    "Central Division",
                    [
                        new("Chicago Zephyrs", "Windy City Gardens", "#13294B", "#6CACE4"),
                        new("Detroit Motors", "Assembly Line Arena", "#C8102E", "#F2F2F2"),
                        new("Minneapolis Freeze", "Mill City Arena", "#24597E", "#CFE8F5"),
                        new("Milwaukee Stags", "Cream City Arena", "#1E4D2B", "#E6D3A3"),
                        new("Winnipeg Wolves", "Red River Arena", "#3D4450", "#8EC9E8"),
                        new("Regina Prairie", "Queen City Coliseum", "#255725", "#EBD27A"),
                        new("Calgary Peaks", "Bow River Arena", "#1C3D5A", "#F1BE48"),
                        new("Edmonton Aurora", "Northern Lights Centre", "#0B1F4B", "#3DDC97"),
                    ]),
                new(
                    "Pacific Division",
                    [
                        new("Vancouver Orcas", "False Creek Arena", "#121417", "#2E9CCA"),
                        new("Seattle Evergreens", "Puget Sound Arena", "#0B4F3C", "#99D9C1"),
                        new("Portland Pioneers", "Rose City Arena", "#8B2F1F", "#F0E2C4"),
                        new("San Francisco Fog", "Golden Gate Pavilion", "#A82E25", "#E3E6E9"),
                        new("Los Angeles Stars", "Sunset Gardens", "#24135F", "#C5CBD3"),
                        new("San Diego Surf", "Mission Bay Arena", "#005F86", "#F5D59A"),
                        new("Las Vegas Scorpions", "Mojave Arena", "#2A2A2A", "#B9975B"),
                        new("Phoenix Roadrunners", "Sonoran Arena", "#973F29", "#F3D9A4"),
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

    /// <param name="Arena">The fictional home arena.</param>
    /// <param name="PrimaryColour">The leading identity colour, written as #RRGGBB.</param>
    /// <param name="SecondaryColour">
    /// The accent, written as #RRGGBB. Desktop sets primary-coloured text on it, so each pair
    /// keeps at least the 4.5:1 contrast that WCAG asks of body text.
    /// </param>
    internal sealed record TeamDefinition(string Name, string Arena, string PrimaryColour, string SecondaryColour)
    {
        public TeamColours Colours => new(Colour.FromHex(PrimaryColour), Colour.FromHex(SecondaryColour));
    }
}