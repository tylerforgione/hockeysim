using System.Collections.ObjectModel;

namespace HockeySim.Domain;

public sealed class Lineup
{
    public const int RequiredForwardLineCount = 4;
    public const int RequiredDefensePairCount = 3;
    public const int RequiredDressedPlayerCount = 20;

    private readonly ReadOnlyCollection<ForwardLine> _forwardLines;
    private readonly ReadOnlyCollection<DefensePair> _defensePairs;
    private readonly ReadOnlyCollection<Player> _dressedPlayers;

    public Lineup(
        IEnumerable<ForwardLine> forwardLines,
        IEnumerable<DefensePair> defensePairs,
        Player startingGoalie,
        Player backupGoalie)
    {
        ArgumentNullException.ThrowIfNull(forwardLines);
        ArgumentNullException.ThrowIfNull(defensePairs);
        ArgumentNullException.ThrowIfNull(startingGoalie);
        ArgumentNullException.ThrowIfNull(backupGoalie);

        var forwardLineList = forwardLines.ToList();
        if (forwardLineList.Count != RequiredForwardLineCount)
        {
            throw new ArgumentException(
                $"A lineup must contain exactly {RequiredForwardLineCount} forward lines.",
                nameof(forwardLines));
        }

        var defensePairList = defensePairs.ToList();
        if (defensePairList.Count != RequiredDefensePairCount)
        {
            throw new ArgumentException(
                $"A lineup must contain exactly {RequiredDefensePairCount} defense pairs.",
                nameof(defensePairs));
        }

        if (startingGoalie.Position != Position.Goalie || backupGoalie.Position != Position.Goalie)
        {
            throw new ArgumentException("Both lineup goalies must play the goalie position.");
        }

        var dressedPlayers = forwardLineList
            .SelectMany(line => line.Players)
            .Concat(defensePairList.SelectMany(pair => pair.Players))
            .Append(startingGoalie)
            .Append(backupGoalie)
            .ToList();

        if (dressedPlayers.Count != RequiredDressedPlayerCount
            || dressedPlayers.Select(player => player.Id).Distinct().Count() != dressedPlayers.Count)
        {
            throw new ArgumentException(
                $"A lineup must contain {RequiredDressedPlayerCount} distinct dressed players.");
        }

        _forwardLines = forwardLineList.AsReadOnly();
        _defensePairs = defensePairList.AsReadOnly();
        StartingGoalie = startingGoalie;
        BackupGoalie = backupGoalie;
        _dressedPlayers = dressedPlayers.AsReadOnly();
    }

    public IReadOnlyList<ForwardLine> ForwardLines => _forwardLines;

    public IReadOnlyList<DefensePair> DefensePairs => _defensePairs;

    public Player StartingGoalie { get; }

    public Player BackupGoalie { get; }

    public IReadOnlyList<Player> DressedPlayers => _dressedPlayers;
}