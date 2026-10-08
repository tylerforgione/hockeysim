using HockeySim.Domain;

namespace HockeySim.Simulation.Play;

/// <summary>
/// One dressed skater during a match: their composite strengths, their energy, and their time on
/// the ice so far. Energy falls on the ice and recovers on the bench at rates set by stamina, and
/// a tired skater plays below their ratings.
/// </summary>
internal sealed class SkaterState
{
    private readonly double _drainFactor;
    private readonly double _recoveryFactor;

    public SkaterState(Player player)
    {
        Player = player;
        Offence = PlayerStrength.Offence(player);
        Defence = PlayerStrength.Defence(player);
        Finishing = PlayerStrength.Finishing(player);
        Accuracy = player.GetRating(Rating.ShotAccuracy).Value;
        Playmaking = PlayerStrength.Playmaking(player);
        Faceoffs = player.GetRating(Rating.Faceoffs).Value;
        ShotBlocking = player.GetRating(Rating.ShotBlocking).Value;
        StickChecking = player.GetRating(Rating.StickChecking).Value;
        PuckControl = player.GetRating(Rating.PuckControl).Value;
        Physicality = PlayerStrength.Physicality(player);
        PuckProtection = PlayerStrength.PuckProtection(player);
        Discipline = player.GetRating(Rating.Discipline).Value;
        Toughness = player.GetRating(Rating.Toughness).Value;

        // Stamina 50 is average; stamina 0 and 100 move the rates by the full spread either way.
        var stamina = (player.GetRating(Rating.Stamina).Value - 50) / 50.0;
        _drainFactor = 1 - (MatchTuning.StaminaDrainSpread * stamina);
        _recoveryFactor = 1 + (MatchTuning.StaminaRecoverySpread * stamina);
    }

    public Player Player { get; }

    public PlayerId Id => Player.Id;

    public double Offence { get; }

    public double Defence { get; }

    public double Finishing { get; }

    public double Accuracy { get; }

    public double Playmaking { get; }

    public double Faceoffs { get; }

    public double ShotBlocking { get; }

    public double StickChecking { get; }

    public double PuckControl { get; }

    public double Physicality { get; }

    public double PuckProtection { get; }

    /// <summary>How rarely the skater takes penalties.</summary>
    public double Discipline { get; }

    /// <summary>How willing and able the skater is to fight.</summary>
    public double Toughness { get; }

    /// <summary>Ejected by a game misconduct; the skater takes no further part in the match.</summary>
    public bool IsEjected { get; private set; }

    /// <summary>From one when rested to zero when exhausted.</summary>
    public double Energy { get; private set; } = 1;

    public int TimeOnIceSeconds { get; private set; }

    /// <summary>The share of their ratings the skater can bring at their current energy.</summary>
    public double Performance => MatchTuning.ExhaustedPerformance + ((1 - MatchTuning.ExhaustedPerformance) * Energy);

    public void Skate(int seconds, double drainMultiplier)
    {
        TimeOnIceSeconds += seconds;
        Energy = Math.Max(0, Energy - (seconds * MatchTuning.EnergyDrainPerSecond * drainMultiplier * _drainFactor));
    }

    public void Rest(int seconds) =>
        Energy = Math.Min(1, Energy + (seconds * MatchTuning.EnergyRecoveryPerSecond * _recoveryFactor));

    public void RestForIntermission() => Energy = Math.Min(1, Energy + MatchTuning.IntermissionRecovery);

    public void Eject() => IsEjected = true;
}