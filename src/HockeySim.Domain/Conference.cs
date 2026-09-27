using System.Collections.ObjectModel;

namespace HockeySim.Domain;

public sealed class Conference
{
    public const int RequiredDivisionCount = 2;

    private readonly ReadOnlyCollection<Division> _divisions;

    public Conference(string name, IEnumerable<Division> divisions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(divisions);

        var divisionList = divisions.ToList();
        if (divisionList.Count != RequiredDivisionCount)
        {
            throw new ArgumentException(
                $"A conference must contain exactly {RequiredDivisionCount} divisions.",
                nameof(divisions));
        }

        if (divisionList.Select(division => division.Name).Distinct(StringComparer.Ordinal).Count()
            != divisionList.Count)
        {
            throw new ArgumentException("Division names must be unique within a conference.", nameof(divisions));
        }

        Name = name;
        _divisions = divisionList.AsReadOnly();
    }

    public string Name { get; }

    public IReadOnlyList<Division> Divisions => _divisions;
}