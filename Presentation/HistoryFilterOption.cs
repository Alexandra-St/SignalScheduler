namespace SignalScheduler.Presentation;

public enum HistoryFilterKind { All, Sent, Cancelled, NewMissed, Missed, Uncertain, Blocked }

public sealed class HistoryFilterOption(HistoryFilterKind kind, string label) : ObservableObject
{
    private string label = label;
    public HistoryFilterKind Kind { get; } = kind;
    public string Label { get => label; private set => Set(ref label, value); }
    internal void UpdateCount(int count) => Label = $"Missed ({count})";
    public override string ToString() => Label;

    public static IReadOnlyList<HistoryFilterOption> Create(int newMissedCount, bool includeNewMissed)
    {
        var options = new List<HistoryFilterOption>
        {
            new(HistoryFilterKind.All, "All statuses"),
            new(HistoryFilterKind.Sent, "Sent"),
            new(HistoryFilterKind.Cancelled, "Cancelled")
        };
        if (includeNewMissed) options.Add(new(HistoryFilterKind.NewMissed, $"Missed ({newMissedCount})"));
        options.AddRange(new[]
        {
            new HistoryFilterOption(HistoryFilterKind.Missed, "Missed"),
            new HistoryFilterOption(HistoryFilterKind.Uncertain, "Unknown / Failed"),
            new HistoryFilterOption(HistoryFilterKind.Blocked, "Blocked")
        });
        return options;
    }
}
