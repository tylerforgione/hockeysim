namespace HockeySim.Simulation.Events;

/// <summary>
/// How many skaters each team has on the ice, not counting goalies: five-on-five in regulation
/// and three-on-three in regular-season overtime unless penalties are being served, and one more
/// for a team that has pulled its goalie for an extra attacker during a delayed penalty.
/// </summary>
public readonly record struct StrengthState(int HomeSkaters, int AwaySkaters)
{
    public bool IsEvenStrength => HomeSkaters == AwaySkaters;

    public override string ToString() => $"{HomeSkaters}v{AwaySkaters}";
}