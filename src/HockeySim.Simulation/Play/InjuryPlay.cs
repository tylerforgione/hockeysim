using HockeySim.Domain;
using HockeySim.Simulation.Events;

namespace HockeySim.Simulation.Play;

/// <summary>
/// Applies the injuries that contacts and strains cause during play: records each one and takes
/// the player out of the match or plays them on at reduced ratings. <see cref="MatchInjuries"/>
/// decides whether a contact or strain injures, and how.
/// </summary>
internal sealed class InjuryPlay(MatchState state, MatchInjuries injuries)
{
    /// <summary>A contact that may injure a skater, unless they have already left the match injured.</summary>
    public void Contact(MatchSide side, SkaterState skater, InjuryCause cause)
    {
        if (skater.IsInjured || injuries.TryInjure(skater.Player, cause, 1, canLeave: true, side.AbleSkaters) is not { } injury)
        {
            return;
        }

        RecordInjury(side, skater.Player, injury);
        side.Injure(skater, injury.Definition, injury.Reductions);
    }

    /// <summary>
    /// Each second played gives every skater on the ice and each goalie in net a small chance of a
    /// moment of strain, which may injure them; tired skaters strain more often. The goalie in net
    /// suffers only strains they can play through.
    /// </summary>
    public void Strain(int seconds)
    {
        if (!injuries.InjuriesPossible)
        {
            return;
        }

        var players = new List<(MatchSide Side, SkaterState? Skater)>();
        foreach (var side in new[] { state.Home, state.Away })
        {
            players.AddRange(side.OnIce.Select(slot => (side, (SkaterState?)slot.Skater)));
            if (!side.IsGoaliePulled)
            {
                players.Add((side, null));
            }
        }

        if (!state.Random.Chance(InjuryTuning.StrainMomentsPerPlayerSecond * seconds * players.Count))
        {
            return;
        }

        var (strainedSide, skater) = players[state.Random.NextInt(0, players.Count)];
        if (skater is null)
        {
            if (injuries.TryInjure(strainedSide.Goalie, InjuryCause.Strain, 1, canLeave: false, strainedSide.AbleGoalies) is { } goalieInjury)
            {
                RecordInjury(strainedSide, strainedSide.Goalie, goalieInjury);
                strainedSide.InjureGoalie(goalieInjury.Reductions);
            }

            return;
        }

        var fatigue = 1 + (InjuryTuning.ExhaustedStrainIncrease * (1 - skater.Energy));
        if (injuries.TryInjure(skater.Player, InjuryCause.Strain, fatigue, canLeave: true, strainedSide.AbleSkaters) is { } injury)
        {
            RecordInjury(strainedSide, skater.Player, injury);
            strainedSide.Injure(skater, injury.Definition, injury.Reductions);
        }
    }

    private void RecordInjury(MatchSide side, Player player, DrawnInjury injury)
    {
        state.Record(new InjuryEvent(state.Period, state.Now, state.OnIce(), side.TeamId, player.Id, injury.Definition.Type, injury.Cause, injury.RecoveryDays));
        state.ForgetOnIce();
    }
}