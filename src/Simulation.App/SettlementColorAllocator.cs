using Simulation.Core;
using Simulation.Core.Randomness;

namespace Simulation.App;

internal sealed class SettlementColorAllocator
{
    internal const int PaletteSize = 60;

    private static readonly Color[] Palette = Enumerable.Range(0, PaletteSize)
        .Select(index => FromHsv((index * 137.50776405003785) % 360, 0.58 + 0.08 * (index % 3), 0.88 + 0.04 * (index % 2)))
        .ToArray();

    private readonly Dictionary<int, int> _slotBySettlement = new();

    public void Synchronize(IReadOnlyList<SettlementProjection> settlements)
    {
        var active = settlements.Where(item => item.IsActive)
            .OrderBy(item => item.FormedTick)
            .ThenBy(item => item.Id)
            .ToArray();
        var activeIds = active.Select(item => item.Id).ToHashSet();
        foreach (var id in _slotBySettlement.Keys.Where(id => !activeIds.Contains(id)).ToArray())
        {
            _slotBySettlement.Remove(id);
        }

        foreach (var settlement in active.Where(item => !_slotBySettlement.ContainsKey(item.Id)))
        {
            var used = _slotBySettlement.Values.ToHashSet();
            var available = Enumerable.Range(0, Palette.Length).Where(slot => !used.Contains(slot)).ToArray();
            var source = available.Length == 0 ? Enumerable.Range(0, Palette.Length).ToArray() : available;
            var draw = StableHash.Hash64($"settlement-color|{settlement.Id}|{settlement.FormedTick}");
            _slotBySettlement.Add(settlement.Id, source[(int)(draw % (ulong)source.Length)]);
        }
    }

    public Color ColorFor(int settlementId)
    {
        if (_slotBySettlement.TryGetValue(settlementId, out var slot))
        {
            return Palette[slot];
        }

        return Palette[(int)(StableHash.Hash64($"settlement-color-fallback|{settlementId}") % PaletteSize)];
    }

    private static Color FromHsv(double hue, double saturation, double value)
    {
        var chroma = value * saturation;
        var x = chroma * (1 - Math.Abs(hue / 60 % 2 - 1));
        var match = value - chroma;
        var (red, green, blue) = hue switch
        {
            < 60 => (chroma, x, 0d),
            < 120 => (x, chroma, 0d),
            < 180 => (0d, chroma, x),
            < 240 => (0d, x, chroma),
            < 300 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };
        return Color.FromArgb(
            (int)Math.Round((red + match) * 255),
            (int)Math.Round((green + match) * 255),
            (int)Math.Round((blue + match) * 255));
    }
}
