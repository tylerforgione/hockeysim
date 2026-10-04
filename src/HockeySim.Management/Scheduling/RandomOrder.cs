using HockeySim.Simulation.Randomness;

namespace HockeySim.Management.Scheduling;

internal static class RandomOrder
{
    /// <summary>
    /// Returns the items in a Fisher-Yates shuffled order drawn from the controlled stream.
    /// </summary>
    public static List<T> Shuffle<T>(IEnumerable<T> items, ControlledRandom random)
    {
        var list = items.ToList();
        for (var index = list.Count - 1; index > 0; index--)
        {
            var swapIndex = random.NextInt(0, index + 1);
            (list[index], list[swapIndex]) = (list[swapIndex], list[index]);
        }

        return list;
    }
}