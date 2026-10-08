using HockeySim.Simulation.Events;

namespace HockeySim.Simulation.Play;

/// <summary>
/// Moves the puck between the teams: giving it to a team in a zone, stopping play for a faceoff,
/// and taking that faceoff. While a delayed penalty is pending, the offenders touching the puck or
/// any stoppage calls it.
/// </summary>
internal sealed class Possession(MatchState state, PenaltyAssessment penalties)
{
    public void GiveTo(MatchSide side, Zone zone)
    {
        // The offenders touching the puck stops play for a delayed penalty.
        if (side == state.DelayedAgainst)
        {
            Stoppage(zoneOwner: side);
            return;
        }

        state.Possessor = side;
        state.Zone = zone;
        state.Rush = false;
        state.Rebound = false;
    }

    /// <summary>Stops play for a faceoff at centre ice, or in the defensive zone of <paramref name="zoneOwner"/>.</summary>
    public void Stoppage(MatchSide? zoneOwner)
    {
        // A stoppage during a delayed penalty calls it, with the faceoff in the offenders' zone.
        if (state.DelayedAgainst is { } offenders)
        {
            zoneOwner = offenders;
            penalties.EndDelayedPenalty(state.DelayedPenalties.ToList());
        }

        state.FaceoffPending = true;
        state.FaceoffZoneOwner = zoneOwner;
        state.Rush = false;
        state.Rebound = false;
    }

    public void TakeFaceoff()
    {
        state.FaceoffPending = false;
        var homeCentre = state.Home.Centre;
        var awayCentre = state.Away.Centre;
        var homeWins = state.Random.Chance(Probability.Adjust(
            0.5,
            MatchTuning.FaceoffSensitivity * (homeCentre.Faceoffs - awayCentre.Faceoffs)));
        var (home, away) = (homeCentre.Skater, awayCentre.Skater);
        var (winner, winnerCentre, loserCentre) = homeWins ? (state.Home, home, away) : (state.Away, away, home);
        state.Record(new FaceoffEvent(state.Period, state.Now, state.OnIce(), winner.TeamId, winnerCentre.Id, loserCentre.Id, state.FaceoffZoneOwner?.TeamId));

        var zone = state.FaceoffZoneOwner is null
            ? Zone.Neutral
            : state.FaceoffZoneOwner == winner ? Zone.Defensive : Zone.Offensive;
        GiveTo(winner, zone);
    }
}