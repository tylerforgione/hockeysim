namespace HockeySim.Simulation.Events;

/// <summary>
/// How many skaters each team has on the ice, not counting goalies: five-on-five in regulation
/// and three-on-three in regular-season overtime.
/// </summary>
public readonly record struct StrengthState(int HomeSkaters, int AwaySkaters)
{
    public bool IsEvenStrength => HomeSkaters == AwaySkaters;

    public override string ToString() => $"{HomeSkaters}v{AwaySkaters}";
}