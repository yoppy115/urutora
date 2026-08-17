using System.ComponentModel;
using System.Drawing.Drawing2D;
using Simulation.Core;
using Simulation.Core.Domain;

namespace Simulation.App;

public sealed class WorldMapPanel : Panel
{
    private SimulationSnapshot? _snapshot;
    private readonly ToolTip _toolTip = new();
    private long? _selectedNpcId;
    private long? _hoverNpcId;

    public WorldMapPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        Padding = new Padding(12);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SimulationSnapshot? Snapshot
    {
        get => _snapshot;
        set
        {
            _snapshot = value;
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long? SelectedNpcId
    {
        get => _selectedNpcId;
        set
        {
            _selectedNpcId = value;
            Invalidate();
        }
    }

    public event EventHandler<NpcSelectedEventArgs>? NpcSelected;

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        if (_snapshot is null)
        {
            return;
        }

        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var availableWidth = ClientSize.Width - Padding.Horizontal;
        var availableHeight = ClientSize.Height - Padding.Vertical;
        var cell = Math.Max(1f, Math.Min(
            (float)availableWidth / _snapshot.Width,
            (float)availableHeight / _snapshot.Height));
        var mapWidth = cell * _snapshot.Width;
        var mapHeight = cell * _snapshot.Height;
        var originX = Padding.Left + (availableWidth - mapWidth) / 2;
        var originY = Padding.Top + (availableHeight - mapHeight) / 2;

        using var gridPen = new Pen(Color.FromArgb(cell >= 8 ? 32 : 14, Color.White), 1);
        for (var x = 0; x <= _snapshot.Width; x++)
        {
            eventArgs.Graphics.DrawLine(gridPen, originX + x * cell, originY, originX + x * cell, originY + mapHeight);
        }
        for (var y = 0; y <= _snapshot.Height; y++)
        {
            eventArgs.Graphics.DrawLine(gridPen, originX, originY + y * cell, originX + mapWidth, originY + y * cell);
        }

        foreach (var settlement in _snapshot.Settlements.Where(item => item.IsActive).OrderBy(item => item.Id))
        {
            var color = SettlementColor(settlement.Id);
            using var influenceBrush = new SolidBrush(Color.FromArgb(18, color));
            using var coreBrush = new SolidBrush(Color.FromArgb(45, color));
            using var corePen = new Pen(Color.FromArgb(155, color), Math.Max(1, cell * 0.08f));
            var influence = RegionRectangle(
                settlement.Center, settlement.InfluenceRadius, originX, originY, cell, _snapshot.Width, _snapshot.Height);
            var core = RegionRectangle(
                settlement.Center, settlement.CoreRadius, originX, originY, cell, _snapshot.Width, _snapshot.Height);
            eventArgs.Graphics.FillRectangle(influenceBrush, influence);
            eventArgs.Graphics.FillRectangle(coreBrush, core);
            eventArgs.Graphics.DrawRectangle(corePen, core.X, core.Y, core.Width, core.Height);
        }

        foreach (var invasion in _snapshot.Invasions.Where(item => item.IsActive).OrderBy(item => item.Id))
        {
            var attack = _snapshot.Settlements.FirstOrDefault(item => item.Id == invasion.AttackSettlementId);
            var defense = _snapshot.Settlements.FirstOrDefault(item => item.Id == invasion.DefenseSettlementId);
            if (attack is null || defense is null)
            {
                continue;
            }

            using var invasionPen = new Pen(Color.FromArgb(225, 235, 74, 74), Math.Max(2, cell * 0.16f))
            {
                DashStyle = DashStyle.Dash,
                EndCap = LineCap.ArrowAnchor
            };
            eventArgs.Graphics.DrawLine(
                invasionPen,
                originX + (attack.Center.X + 0.5f) * cell,
                originY + (attack.Center.Y + 0.5f) * cell,
                originX + (defense.Center.X + 0.5f) * cell,
                originY + (defense.Center.Y + 0.5f) * cell);
        }

        foreach (var landmark in _snapshot.Landmarks)
        {
            using var brush = new SolidBrush(LandmarkColor(landmark.Concept));
            var rectangle = CellRectangle(landmark.Position, originX, originY, cell, 0.5f);
            eventArgs.Graphics.FillRectangle(brush, rectangle);
        }

        foreach (var settlement in _snapshot.Settlements.Where(item => item.IsActive).OrderBy(item => item.Id))
        {
            using var centerBrush = new SolidBrush(SettlementColor(settlement.Id));
            using var centerPen = new Pen(Color.White, Math.Max(1, cell * 0.10f));
            var rectangle = CellRectangle(settlement.Center, originX, originY, cell, Math.Max(0.4f, cell * 0.12f));
            eventArgs.Graphics.FillRectangle(centerBrush, rectangle);
            eventArgs.Graphics.DrawRectangle(centerPen, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
        }

        foreach (var npc in _snapshot.Npcs)
        {
            using var brush = new SolidBrush(NpcColor(npc));
            var inset = Math.Max(0.6f, cell * 0.18f);
            var rectangle = CellRectangle(npc.Position, originX, originY, cell, inset);
            eventArgs.Graphics.FillEllipse(brush, rectangle);
            if (npc.SettlementId.HasValue)
            {
                using var affiliationPen = new Pen(SettlementColor(npc.SettlementId.Value), Math.Max(1, cell * 0.09f));
                eventArgs.Graphics.DrawEllipse(affiliationPen, rectangle);
            }
            if (npc.Id == _selectedNpcId)
            {
                using var selectionPen = new Pen(Color.Gold, Math.Max(2, cell * 0.12f));
                eventArgs.Graphics.DrawEllipse(selectionPen, rectangle);
                var label = $"#{npc.Id}";
                var labelSize = eventArgs.Graphics.MeasureString(label, Font);
                using var labelBackground = new SolidBrush(Color.FromArgb(220, 20, 23, 28));
                using var labelBrush = new SolidBrush(Color.White);
                var labelRectangle = new RectangleF(
                    rectangle.Left + rectangle.Width / 2 - labelSize.Width / 2 - 2,
                    rectangle.Top - labelSize.Height - 2,
                    labelSize.Width + 4,
                    labelSize.Height);
                eventArgs.Graphics.FillRectangle(labelBackground, labelRectangle);
                eventArgs.Graphics.DrawString(label, Font, labelBrush, labelRectangle.Left + 2, labelRectangle.Top);
            }
        }
    }

    protected override void OnMouseClick(MouseEventArgs eventArgs)
    {
        base.OnMouseClick(eventArgs);
        var npc = NpcAt(eventArgs.Location);
        if (npc is null)
        {
            return;
        }

        SelectedNpcId = npc.Id;
        NpcSelected?.Invoke(this, new NpcSelectedEventArgs(npc.Id));
    }

    protected override void OnMouseMove(MouseEventArgs eventArgs)
    {
        base.OnMouseMove(eventArgs);
        var npc = NpcAt(eventArgs.Location);
        Cursor = npc is null ? Cursors.Default : Cursors.Hand;
        if (npc?.Id == _hoverNpcId)
        {
            return;
        }

        _hoverNpcId = npc?.Id;
        _toolTip.SetToolTip(this, npc is null ? string.Empty :
            $"NPC #{npc.Id}  Settlement {(npc.SettlementId?.ToString() ?? "なし")}" +
            (npc.InvasionId.HasValue ? $"  Invasion #{npc.InvasionId}" : string.Empty));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private NpcProjection? NpcAt(Point location)
    {
        if (_snapshot is null)
        {
            return null;
        }

        var availableWidth = ClientSize.Width - Padding.Horizontal;
        var availableHeight = ClientSize.Height - Padding.Vertical;
        var cell = Math.Max(1f, Math.Min(
            (float)availableWidth / _snapshot.Width,
            (float)availableHeight / _snapshot.Height));
        var mapWidth = cell * _snapshot.Width;
        var mapHeight = cell * _snapshot.Height;
        var originX = Padding.Left + (availableWidth - mapWidth) / 2;
        var originY = Padding.Top + (availableHeight - mapHeight) / 2;
        if (location.X < originX || location.X >= originX + mapWidth ||
            location.Y < originY || location.Y >= originY + mapHeight)
        {
            return null;
        }

        var position = new Position(
            Math.Clamp((int)((location.X - originX) / cell), 0, _snapshot.Width - 1),
            Math.Clamp((int)((location.Y - originY) / cell), 0, _snapshot.Height - 1));
        return _snapshot.Npcs.FirstOrDefault(item => item.Position == position);
    }

    private static RectangleF CellRectangle(Position position, float originX, float originY, float cell, float inset) =>
        new(originX + position.X * cell + inset,
            originY + position.Y * cell + inset,
            Math.Max(1, cell - inset * 2),
            Math.Max(1, cell - inset * 2));

    private static RectangleF RegionRectangle(
        Position center,
        int radius,
        float originX,
        float originY,
        float cell,
        int width,
        int height)
    {
        var minimumX = Math.Max(0, center.X - radius);
        var minimumY = Math.Max(0, center.Y - radius);
        var maximumX = Math.Min(width - 1, center.X + radius);
        var maximumY = Math.Min(height - 1, center.Y + radius);
        return new RectangleF(
            originX + minimumX * cell,
            originY + minimumY * cell,
            (maximumX - minimumX + 1) * cell,
            (maximumY - minimumY + 1) * cell);
    }

    private static Color SettlementColor(int settlementId)
    {
        var palette = new[]
        {
            Color.FromArgb(64, 156, 255),
            Color.FromArgb(238, 139, 55),
            Color.FromArgb(112, 190, 109),
            Color.FromArgb(176, 112, 214),
            Color.FromArgb(226, 88, 132),
            Color.FromArgb(48, 184, 181)
        };
        return palette[Math.Abs(settlementId - 1) % palette.Length];
    }

    private static Color LandmarkColor(ConceptKind concept) => concept switch
    {
        ConceptKind.Struggle => Color.FromArgb(224, 72, 72),
        ConceptKind.Survival => Color.FromArgb(75, 185, 110),
        ConceptKind.Communication => Color.FromArgb(75, 135, 230),
        _ => Color.White
    };

    private static Color NpcColor(NpcProjection npc)
    {
        if (npc.ConceptMarks.Contains(ConceptKind.Struggle))
        {
            return Color.FromArgb(244, 164, 164);
        }
        if (npc.ConceptMarks.Contains(ConceptKind.Survival))
        {
            return Color.FromArgb(155, 225, 175);
        }
        if (npc.ConceptMarks.Contains(ConceptKind.Communication))
        {
            return Color.FromArgb(155, 190, 245);
        }

        return Color.FromArgb(238, 232, 205);
    }
}

public sealed class NpcSelectedEventArgs(long npcId) : EventArgs
{
    public long NpcId { get; } = npcId;
}
