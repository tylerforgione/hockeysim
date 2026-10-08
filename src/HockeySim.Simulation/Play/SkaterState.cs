using HockeySim.Domain;

namespace HockeySim.Simulation.Play;

/// <summary>
/// One dressed skater during a match: their composite strengths, their energy, and their time on
/// the ice so far. Energy falls on the ice and recovers on the bench at rates set by stamina, and
/// a tired skater plays below their ratings, as does one playing through an injury.
/// </summary>
internal sealed class SkaterState
{
    private double _drainFactor;
    private double _recoveryFactor;

    /// <param name="reductions">Rating points taken off by injuries the skater is playing through.</param>
    public SkaterState(Player player, IReadOnlyDictionary<Rating, int> reductions)
    {
        Player = player;
        Rate(new EffectiveRatings(player, reductions));
    }

    public Player Player { get; }

    public PlayerId Id => Player.Id;

    public double Offence { get; private set; }

    public double Defence { get; private set; }

    public double Finishing { get; private set; }

    public double Accuracy { get; private set; }

    public double Playmaking { get; private set; }

    public double Faceoffs { get; private set; }

    public double ShotBlocking { get; private set; }

    public double StickChecking { get; private set; }

    public double PuckControl { get; private set; }

    public double Physicality { get; private set; }

    public double PuckProtection { get; private set; }

    /// <summary>How rarely the skater takes penalties.</summary>
    public double Discipline { get; private set; }

    /// <summary>How willing and able the skater is to fight.</summary>
    public double Toughness { get; private set; }

    /// <summary>Ability to beat a goalie one-on-one, in a shootout or on a penalty shot.</summary>
    public double Shootout { get; private set; }

    /// <summary>Ejected by a game misconduct; the skater takes no further part in the match.</summary>
    public bool IsEjected { get; private set; }

    /// <summary>
    /// Out with an injury that cannot be played through, from the start of the match or since
    /// being hurt in it; the skater takes no further shifts.
    /// </summary>
    public bool IsInjured { get; private set; }

    /// <summary>Unable to play from the start of the match, so the skater does not appear.</summary>
    public bool MissedMatch { get; private set; }

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

    public void MissMatch()
    {
        IsInjured = true;
        MissedMatch = true;
    }

    public void LeaveInjured() => IsInjured = true;

    /// <summary>Plays on through an injury suffered in the match, at the reduced ratings.</summary>
    public void PlayThrough(IReadOnlyDictionary<Rating, int> reductions) => Rate(new EffectiveRatings(Player, reductions));

    private void Rate(EffectiveRatings ratings)
    {
        Offence = PlayerStrength.Offence(ratings);
        Defence = PlayerStrength.Defence(ratings);
        Finishing = PlayerStrength.Finishing(ratings);
        Accuracy = ratings[Rating.ShotAccuracy];
        Playmaking = PlayerStrength.Playmaking(ratings);
        Faceoffs = ratings[Rating.Faceoffs];
        ShotBlocking = ratings[Rating.ShotBlocking];
        StickChecking = ratings[Rating.StickChecking];
        PuckControl = ratings[Rating.PuckControl];
        Physicality = PlayerStrength.Physicality(ratings);
        PuckProtection = PlayerStrength.PuckProtection(ratings);
        Discipline = ratings[Rating.Discipline];
        Toughness = ratings[Rating.Toughness];
        Shootout = PlayerStrength.Shootout(ratings);

        // Stamina 50 is average; stamina 0 and 100 move the rates by the full spread either way.
        var stamina = (ratings[Rating.Stamina] - 50) / 50.0;
        _drainFactor = 1 - (MatchTuning.StaminaDrainSpread * stamina);
        _recoveryFactor = 1 + (MatchTuning.StaminaRecoverySpread * stamina);
    }
}