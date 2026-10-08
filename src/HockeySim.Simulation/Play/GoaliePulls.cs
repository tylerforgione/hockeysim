namespace HockeySim.Simulation.Play;

/// <summary>
/// Late in the third period a team trailing by one or two goals pulls its goalie for an extra
/// attacker, earlier the further behind it is. It pulls once it has the puck out of its own zone,
/// or for a faceoff in the attacking zone; the goalie comes back for a faceoff in the team's own
/// zone and goes out again once the rule allows. A goal puts both goalies back (see
/// <see cref="ShotPlay.ScoreGoal"/>), and so does the end of a period.
/// </summary>
internal sealed class GoaliePulls(MatchState state)
{
    public void PullGoaliesToTieTheMatch(int periodSeconds)
    {
        if (state.Period != MatchResult.RegulationPeriodCount)
        {
            return;
        }

        var secondsLeft = periodSeconds - state.Clock;
        PullOrReturnGoalie(state.Home, state.AwayGoals - state.HomeGoals, secondsLeft);
        PullOrReturnGoalie(state.Away, state.HomeGoals - state.AwayGoals, secondsLeft);
    }

    public void ReturnGoaliesPulledToTieTheMatch()
    {
        state.Home.PullGoalieToTieTheMatch(false);
        state.Away.PullGoalieToTieTheMatch(false);
        state.ForgetOnIce();
    }

    private void PullOrReturnGoalie(MatchSide side, int deficit, int secondsLeft)
    {
        var shouldPull = deficit switch
        {
            1 => secondsLeft <= MatchTuning.PullGoalieOneGoalDownSeconds,
            2 => secondsLeft <= MatchTuning.PullGoalieTwoGoalsDownSeconds,
            _ => false,
        };

        var pulled = side.IsGoaliePulledToTieTheMatch;
        bool pull;
        if (!shouldPull)
        {
            pull = false;
        }
        else if (state.FaceoffPending)
        {
            pull = pulled ? state.FaceoffZoneOwner != side : state.FaceoffZoneOwner == state.Opponent(side);
        }
        else
        {
            pull = pulled || (state.Possessor == side && state.Zone != Zone.Defensive);
        }

        if (pull != pulled)
        {
            side.PullGoalieToTieTheMatch(pull);
            state.ForgetOnIce();
        }
    }
}