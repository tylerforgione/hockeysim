using HockeySim.Domain;
using HockeySim.Management.GameManagement;
using HockeySim.Management.GameManagement.Snapshots;

namespace HockeySim.Management.Tests;

internal static class PreseasonPlay
{
    /// <summary>
    /// Plays every preseason day, leaving the game on opening day. Nobody is injured in the
    /// preseason, so no lineup needs changing.
    /// </summary>
    public static GameSnapshot PlayPreseason(this GameManager manager)
    {
        var snapshot = manager.GetSnapshot();
        while (snapshot.Season.Phase == SeasonPhase.Preseason)
        {
            snapshot = manager.AdvanceDay();
        }

        return snapshot;
    }
}