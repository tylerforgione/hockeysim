using System.Collections.ObjectModel;

namespace HockeySim.Domain;

public sealed class Lineup
{
    public const int RequiredForwardLineCount = 4;
    public const int RequiredDefencePairCount = 3;
    public const int RequiredDressedPlayerCount = 20;

    private readonly ReadOnlyCollection<ForwardLine> _forwardLines;
    private readonly ReadOnlyCollection<DefencePair> _defencePairs;
    private readonly ReadOnlyCollection<Player> _dressedPlayers;

    public Lineup(
        IEnumerable<ForwardLine> forwardLines,
        IEnumerable<DefencePair> defencePairs,
        Player startingGoalie,
        Player backupGoalie)
    {
        ArgumentNullException.ThrowIfNull(forwardLines);
        ArgumentNullException.ThrowIfNull(defencePairs);
        ArgumentNullException.ThrowIfNull(startingGoalie);
        ArgumentNullException.ThrowIfNull(backupGoalie);

        var forwardLineList = forwardLines.ToList();
        if (forwardLineList.Count != RequiredForwardLineCount)
        {
            throw new ArgumentException(
                $"A lineup must contain exactly {RequiredForwardLineCount} forward lines.",
                nameof(forwardLines));
        }

        var defencePairList = defencePairs.ToList();
        if (defencePairList.Count != RequiredDefencePairCount)
        {
            throw new ArgumentException(
                $"A lineup must contain exactly {RequiredDefencePairCount} defence pairs.",
                nameof(defencePairs));
        }

        if (startingGoalie.Position != Position.Goalie || backupGoalie.Position != Position.Goalie)
        {
            throw new ArgumentException("Both lineup goalies must play the goalie position.");
        }

        var dressedPlayers = forwardLineList
            .SelectMany(line => line.Players)
            .Concat(defencePairList.SelectMany(pair => pair.Players))
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
        _defencePairs = defencePairList.AsReadOnly();
        StartingGoalie = startingGoalie;
        BackupGoalie = backupGoalie;
        _dressedPlayers = dressedPlayers.AsReadOnly();
    }

    public IReadOnlyList<ForwardLine> ForwardLines => _forwardLines;

    public IReadOnlyList<DefencePair> DefencePairs => _defencePairs;

    public Player StartingGoalie { get; }

    public Player BackupGoalie { get; }

    public IReadOnlyList<Player> DressedPlayers => _dressedPlayers;
}