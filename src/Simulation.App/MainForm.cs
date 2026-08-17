using System.Globalization;
using Simulation.Core;
using Simulation.Core.Configuration;
using Simulation.Core.Domain;

namespace Simulation.App;

public sealed class MainForm : Form
{
    private readonly WorldSessionStore _worldStore;
    private readonly SimulationConfig _simulationConfig;
    private readonly string _simulationConfigPath;
    private readonly long _baseSeed;
    private readonly ObservationAppConfig _appConfig;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly WorldMapPanel _map = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(23, 26, 31) };
    private readonly Label _worldLabel = new() { AutoSize = true, Font = new Font("Yu Gothic UI", 11, FontStyle.Bold) };
    private readonly Label _timeLabel = new() { AutoSize = true, Font = new Font("Yu Gothic UI", 11, FontStyle.Bold) };
    private readonly Label _populationLabel = new() { AutoSize = true };
    private readonly Label _seedLabel = new() { AutoSize = true };
    private readonly Button _newWorldButton = new() { Text = "世界生成", AutoSize = true };
    private readonly Button _runButton = new() { Text = "再生", AutoSize = true };
    private readonly Button _stepButton = new() { Text = "1日進める", AutoSize = true };
    private readonly ComboBox _speed = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly SplitContainer _mainSplit = new()
    {
        Dock = DockStyle.Fill,
        Orientation = Orientation.Vertical,
        FixedPanel = FixedPanel.Panel2
    };
    private readonly ListBox _events = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true };
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly DataGridView _npcProperties = CreateReadOnlyGrid();
    private readonly ListBox _actionHistory = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true };
    private readonly Label _npcTitle = new() { Dock = DockStyle.Top, Height = 32, Text = "マップ上のNPCをクリックしてください" };
    private readonly Label _statisticsSummary = new() { Dock = DockStyle.Top, Height = 34 };
    private readonly DataGridView _actionStatistics = CreateReadOnlyGrid();
    private readonly DataGridView _deathCauseStatistics = CreateReadOnlyGrid();
    private readonly DataGridView _ageDistributionStatistics = CreateReadOnlyGrid();
    private readonly DataGridView _diagnosticStatistics = CreateReadOnlyGrid();
    private readonly DataGridView _socialStatistics = CreateReadOnlyGrid();
    private readonly WorldStatisticsChartPanel _statisticsChart = new() { Dock = DockStyle.Fill };
    private readonly DataGridView _settlementProperties = CreateReadOnlyGrid();
    private readonly DataGridView _settlementFrictionStatistics = CreateReadOnlyGrid();
    private readonly Label _settlementTitle = new()
        { Dock = DockStyle.Top, Height = 32, Text = "マップ上のSettlement中心をクリックしてください" };
    private WorldSession _world;
    private long? _selectedNpcId;
    private int? _selectedSettlementId;
    private volatile bool _running;
    private volatile bool _advancing;
    private bool _worldCreationRequested;

    internal Exception? SmokeFailure;

    public MainForm(
        WorldSessionStore worldStore,
        WorldSession initialWorld,
        SimulationConfig simulationConfig,
        string simulationConfigPath,
        long baseSeed,
        ObservationAppConfig appConfig)
    {
        _worldStore = worldStore;
        _world = initialWorld;
        _simulationConfig = simulationConfig;
        _simulationConfigPath = simulationConfigPath;
        _baseSeed = baseSeed;
        _appConfig = appConfig;

        Text = $"{ReleaseIdentity.DisplayName} — 観測窓";
        MinimumSize = new Size(960, 680);
        Size = new Size(1360, 860);
        Font = new Font("Yu Gothic UI", 9);
        BackColor = Color.FromArgb(245, 246, 248);
        KeyPreview = true;

        _speed.Items.AddRange(new object[]
        {
            new SpeedOption("1倍", 1),
            new SpeedOption("2倍", 2),
            new SpeedOption("3倍", 3),
            new SpeedOption("5倍", 5),
            new SpeedOption("10倍", 10),
            new SpeedOption("50倍", 50)
        });
        _speed.SelectedIndex = 0;

        var toolbar = BuildToolbar();
        _mainSplit.SplitterDistance = 900;
        _mainSplit.Panel1.Controls.Add(_map);
        _mainSplit.Panel2.Controls.Add(BuildObservationTabs());

        Controls.Add(_mainSplit);
        Controls.Add(toolbar);

        _newWorldButton.Click += (_, _) => RequestWorldCreation();
        _runButton.Click += (_, _) => ToggleRunning();
        _stepButton.Click += async (_, _) => await AdvanceOneDayAsync();
        _map.NpcSelected += (_, eventArgs) => SelectNpc(eventArgs.NpcId);
        _map.SettlementSelected += (_, eventArgs) => SelectSettlement(eventArgs.SettlementId);
        KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.Control && eventArgs.KeyCode == Keys.N)
            {
                RequestWorldCreation();
                eventArgs.SuppressKeyPress = true;
            }
        };

        Shown += (_, _) =>
        {
            _mainSplit.Panel1MinSize = 420;
            _mainSplit.Panel2MinSize = 340;
            ApplyObservationPanelWidth();
        };
        Resize += (_, _) => ApplyObservationPanelWidth();

        _timer = new System.Windows.Forms.Timer { Interval = 100 };
        _timer.Tick += async (_, _) => await AdvanceForRenderFrameAsync();
        _timer.Start();
        FormClosing += (_, _) =>
        {
            _running = false;
            _worldCreationRequested = false;
            _timer.Stop();
        };
        FormClosed += (_, _) =>
        {
            if (!_advancing)
            {
                _world.Dispose();
            }
        };
        RefreshProjection();
        UpdateCommandState();
    }

    internal async Task RunUiSmokeChecksAsync()
    {
        if (_tabs.TabPages.Count != 4 || _diagnosticStatistics.Columns.Count != 3 ||
            _settlementFrictionStatistics.Columns.Count != 6 || !_newWorldButton.Enabled ||
            _speed.Items.Cast<SpeedOption>().Select(item => item.TicksPerFrame)
                .SequenceEqual(new[] { 1, 2, 3, 5, 10, 50 }) is false)
        {
            throw new InvalidOperationException("Required observation controls were not initialized.");
        }

        var firstWorldNumber = _world.Info.WorldNumber;
        await AdvanceOneDayAsync();
        if (_world.Engine.GetSnapshot().Tick != 1)
        {
            throw new InvalidOperationException("The one-day command did not advance the World.");
        }

        var observedNpc = _world.Engine.GetSnapshot().Npcs
            .Select(item => _world.Engine.GetNpcDetails(item.Id, _appConfig.NpcActionHistoryDisplayLimit))
            .FirstOrDefault(item => item?.ActionHistory.Count > 0);
        if (observedNpc is null)
        {
            throw new InvalidOperationException("NPC action history was not projected.");
        }

        SelectNpc(observedNpc.Id);
        if (_actionHistory.Items.Count == 0)
        {
            throw new InvalidOperationException("NPC action history was not rendered.");
        }

        _newWorldButton.PerformClick();
        if (_world.Info.WorldNumber != firstWorldNumber + 1)
        {
            throw new InvalidOperationException("The World generation command did not create the next numbered World.");
        }

        _tabs.SelectedIndex = 2;
        RefreshProjection();
        if (_deathCauseStatistics.Rows.Count == 0 || _ageDistributionStatistics.Rows.Count == 0)
        {
            throw new InvalidOperationException("World death causes or age distribution were not rendered.");
        }
    }

    private void ApplyObservationPanelWidth()
    {
        var availableWidth = _mainSplit.ClientSize.Width;
        if (availableWidth < 760)
        {
            return;
        }

        var observationWidth = Math.Clamp((int)Math.Round(availableWidth * 0.42), 430, 560);
        var desiredDistance = availableWidth - observationWidth - _mainSplit.SplitterWidth;
        if (Math.Abs(_mainSplit.SplitterDistance - desiredDistance) > 1)
        {
            _mainSplit.SplitterDistance = desiredDistance;
        }
    }

    private Control BuildToolbar()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(10, 11, 10, 7),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.White
        };
        panel.Controls.Add(_newWorldButton);
        panel.Controls.Add(_runButton);
        panel.Controls.Add(_stepButton);
        panel.Controls.Add(new Label { Text = "速度", AutoSize = true, Margin = new Padding(16, 7, 4, 0) });
        panel.Controls.Add(_speed);
        panel.Controls.Add(_worldLabel);
        panel.Controls.Add(_timeLabel);
        panel.Controls.Add(_populationLabel);
        panel.Controls.Add(_seedLabel);
        return panel;
    }

    private Control BuildObservationTabs()
    {
        _tabs.TabPages.Add(new TabPage("出来事") { Controls = { BuildEventPanel() } });
        _tabs.TabPages.Add(new TabPage("NPC詳細") { Controls = { BuildNpcPanel() } });
        _tabs.TabPages.Add(new TabPage("世界統計") { Controls = { BuildStatisticsPanel() } });
        _tabs.TabPages.Add(new TabPage("Settlement詳細") { Controls = { BuildSettlementPanel() } });
        return _tabs;
    }

    private Control BuildEventPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        panel.Controls.Add(_events);
        panel.Controls.Add(new Label
        {
            Text = "最近の出来事",
            Dock = DockStyle.Top,
            Height = 30,
            Font = new Font(Font, FontStyle.Bold)
        });
        return panel;
    }

    private Control BuildNpcPanel()
    {
        _npcProperties.Columns.Add("property", "項目");
        _npcProperties.Columns.Add("value", "値");
        _npcProperties.Columns[0].Width = 150;
        _npcProperties.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

        var historyPanel = new Panel { Dock = DockStyle.Fill };
        historyPanel.Controls.Add(_actionHistory);
        historyPanel.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "行動履歴（移動を除く）",
            Font = new Font(Font, FontStyle.Bold)
        });
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 430
        };
        split.Panel1.Controls.Add(_npcProperties);
        split.Panel2.Controls.Add(historyPanel);
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        panel.Controls.Add(split);
        panel.Controls.Add(_npcTitle);
        return panel;
    }

    private Control BuildStatisticsPanel()
    {
        _actionStatistics.Columns.Add("action", "行動コマンド");
        _actionStatistics.Columns.Add("count", "選択回数");
        _actionStatistics.Columns.Add("ratio", "比率");
        _actionStatistics.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _actionStatistics.Columns[1].Width = 90;
        _actionStatistics.Columns[2].Width = 70;

        _deathCauseStatistics.Columns.Add("cause", "死因");
        _deathCauseStatistics.Columns.Add("count", "件数");
        _deathCauseStatistics.Columns.Add("ratio", "比率");
        _deathCauseStatistics.Columns.Add("averageAge", "平均死亡年齢");
        _deathCauseStatistics.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _deathCauseStatistics.Columns[1].Width = 70;
        _deathCauseStatistics.Columns[2].Width = 70;
        _deathCauseStatistics.Columns[3].Width = 110;

        _ageDistributionStatistics.Columns.Add("range", "現在年齢");
        _ageDistributionStatistics.Columns.Add("count", "人数");
        _ageDistributionStatistics.Columns.Add("ratio", "構成比");
        _ageDistributionStatistics.Columns.Add("bar", "分布");
        _ageDistributionStatistics.Columns[0].Width = 115;
        _ageDistributionStatistics.Columns[1].Width = 65;
        _ageDistributionStatistics.Columns[2].Width = 70;
        _ageDistributionStatistics.Columns[3].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

        _diagnosticStatistics.Columns.Add("category", "分類");
        _diagnosticStatistics.Columns.Add("metric", "指標");
        _diagnosticStatistics.Columns.Add("value", "値");
        _diagnosticStatistics.Columns[0].Width = 95;
        _diagnosticStatistics.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _diagnosticStatistics.Columns[2].Width = 95;

        _socialStatistics.Columns.Add("category", "分類");
        _socialStatistics.Columns.Add("subject", "対象");
        _socialStatistics.Columns.Add("value", "状態");
        _socialStatistics.Columns[0].Width = 90;
        _socialStatistics.Columns[1].Width = 115;
        _socialStatistics.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

        var tables = new TabControl { Dock = DockStyle.Fill };
        tables.TabPages.Add(new TabPage("行動選択") { Controls = { _actionStatistics } });
        tables.TabPages.Add(new TabPage("死因") { Controls = { _deathCauseStatistics } });
        tables.TabPages.Add(new TabPage("年齢分布") { Controls = { _ageDistributionStatistics } });
        tables.TabPages.Add(new TabPage("社会") { Controls = { _socialStatistics } });
        tables.TabPages.Add(new TabPage($"{ReleaseIdentity.VersionDirectoryName}診断")
            { Controls = { _diagnosticStatistics } });

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 440
        };
        split.Panel1.Controls.Add(_statisticsChart);
        split.Panel2.Controls.Add(tables);
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        panel.Controls.Add(split);
        panel.Controls.Add(_statisticsSummary);
        return panel;
    }

    private Control BuildSettlementPanel()
    {
        _settlementProperties.Columns.Add("property", "項目");
        _settlementProperties.Columns.Add("value", "値");
        _settlementProperties.Columns[0].Width = 155;
        _settlementProperties.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

        _settlementFrictionStatistics.Columns.Add("other", "相手");
        _settlementFrictionStatistics.Columns.Add("current", "現在値");
        _settlementFrictionStatistics.Columns.Add("collisions", "Collision");
        _settlementFrictionStatistics.Columns.Add("threats", "Threat");
        _settlementFrictionStatistics.Columns.Add("decay", "累積減衰");
        _settlementFrictionStatistics.Columns.Add("last", "最終変動日");
        _settlementFrictionStatistics.Columns[0].Width = 80;
        _settlementFrictionStatistics.Columns[1].Width = 80;
        _settlementFrictionStatistics.Columns[2].Width = 85;
        _settlementFrictionStatistics.Columns[3].Width = 70;
        _settlementFrictionStatistics.Columns[4].Width = 90;
        _settlementFrictionStatistics.Columns[5].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

        var frictionPanel = new Panel { Dock = DockStyle.Fill };
        frictionPanel.Controls.Add(_settlementFrictionStatistics);
        frictionPanel.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "Friction",
            Font = new Font(Font, FontStyle.Bold)
        });
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 360
        };
        split.Panel1.Controls.Add(_settlementProperties);
        split.Panel2.Controls.Add(frictionPanel);
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        panel.Controls.Add(split);
        panel.Controls.Add(_settlementTitle);
        return panel;
    }

    private void RequestWorldCreation()
    {
        if (_worldCreationRequested)
        {
            return;
        }

        _worldCreationRequested = true;
        _running = false;
        UpdateCommandState();
        if (!_advancing)
        {
            CompleteWorldCreationRequest();
        }
    }

    private void CompleteWorldCreationRequest()
    {
        if (!_worldCreationRequested || IsDisposed || Disposing)
        {
            return;
        }

        _worldCreationRequested = false;
        try
        {
            var next = _worldStore.CreateNextWorld(_simulationConfig, _simulationConfigPath, _baseSeed);
            var previous = _world;
            _world = next;
            previous.Dispose();
            _selectedNpcId = null;
            _selectedSettlementId = null;
            _map.SelectedNpcId = null;
            _map.SelectedSettlementId = null;
            RefreshProjection();
        }
        catch (Exception exception)
        {
            ShowOperationError("世界生成に失敗しました。", exception);
        }
        finally
        {
            UpdateCommandState();
        }
    }

    private void ToggleRunning()
    {
        _running = !_running;
        UpdateCommandState();
    }

    private async Task AdvanceOneDayAsync()
    {
        if (_running || _advancing)
        {
            return;
        }

        _advancing = true;
        UpdateCommandState();
        try
        {
            await Task.Run(_world.AdvanceOneDay);
            RefreshProjection();
        }
        catch (Exception exception)
        {
            ShowOperationError("世界の進行またはログ保存に失敗しました。", exception);
        }
        finally
        {
            _advancing = false;
            if (_worldCreationRequested)
            {
                CompleteWorldCreationRequest();
            }
            else
            {
                UpdateCommandState();
            }
        }
    }

    private async Task AdvanceForRenderFrameAsync()
    {
        if (!_running || _advancing)
        {
            return;
        }

        _advancing = true;
        UpdateCommandState();
        var option = (SpeedOption)_speed.SelectedItem!;
        try
        {
            await Task.Run(() =>
            {
                for (var index = 0; index < option.TicksPerFrame && _running; index++)
                {
                    _world.AdvanceOneDay();
                }
            });
            if (!IsDisposed)
            {
                RefreshProjection();
            }
        }
        catch (Exception exception)
        {
            _running = false;
            ShowOperationError("世界の進行またはログ保存に失敗しました。", exception);
        }
        finally
        {
            _advancing = false;
            if (_worldCreationRequested)
            {
                CompleteWorldCreationRequest();
            }
            else
            {
                UpdateCommandState();
            }
        }
    }

    private void RefreshProjection()
    {
        var snapshot = _world.Engine.GetSnapshot(_appConfig.RecentEventDisplayLimit);
        var statistics = _world.Engine.GetWorldStatistics();
        var ageDistribution = _world.Engine.GetCurrentAgeDistribution(AgeDistributionBucketDays());
        var worldNumber = _world.Info.WorldNumber.ToString(
            $"D{_appConfig.WorldNumberPadding}", CultureInfo.InvariantCulture);
        _map.Snapshot = snapshot;
        _worldLabel.Text = $"  {_world.Info.ReleaseVersion} 世界 #{worldNumber}";
        _timeLabel.Text = $"  第{snapshot.Year}年 {snapshot.Day}日";
        _populationLabel.Text =
            $"  {snapshot.Phase} / 人口 {snapshot.Npcs.Count} / Settlement {snapshot.Settlements.Count(item => item.IsActive)}";
        _seedLabel.Text = $"  Seed {_world.Info.Seed}";
        Text = $"{ReleaseIdentity.DisplayName} — 世界 #{worldNumber}";

        _events.BeginUpdate();
        _events.Items.Clear();
        foreach (var item in ObservationDisplayPolicy.VisibleRecentEvents(snapshot.RecentEvents).Reverse())
        {
            _events.Items.Add(FormatEvent(item));
        }
        _events.EndUpdate();
        RefreshNpcDetails();
        RefreshStatistics(statistics, ageDistribution);
        RefreshSettlementDetails(statistics);
    }

    private void SelectNpc(long npcId)
    {
        _selectedNpcId = npcId;
        _selectedSettlementId = null;
        _map.SelectedNpcId = npcId;
        _map.SelectedSettlementId = null;
        _tabs.SelectedIndex = 1;
        RefreshNpcDetails();
    }

    private void SelectSettlement(int settlementId)
    {
        _selectedSettlementId = settlementId;
        _selectedNpcId = null;
        _map.SelectedSettlementId = settlementId;
        _map.SelectedNpcId = null;
        _tabs.SelectedIndex = 3;
        RefreshSettlementDetails(_world.Engine.GetWorldStatistics());
    }

    private void RefreshNpcDetails()
    {
        _npcProperties.Rows.Clear();
        _actionHistory.Items.Clear();
        if (!_selectedNpcId.HasValue)
        {
            _npcTitle.Text = "マップ上のNPCをクリックしてください";
            return;
        }

        var details = _world.Engine.GetNpcDetails(
            _selectedNpcId.Value, _appConfig.NpcActionHistoryDisplayLimit);
        if (details is null)
        {
            _npcTitle.Text = $"NPC #{_selectedNpcId.Value} — データなし";
            return;
        }

        _npcTitle.Text = $"NPC #{details.Id} — {(details.IsAlive ? "生存" : "死亡")}";
        AddNpcRow("位置", details.Position.ToString());
        AddNpcRow("年齢", $"{details.AgeYears:0.00}年 ({details.AgeDays}日)");
        AddNpcRow("成熟", details.IsMature ? "はい" : "いいえ");
        AddNpcRow("HP", $"{details.CurrentHp:0.00} / {details.EffectiveStats.MaxHp:0.00}");
        AddNpcRow("Base MaxHP", Format(details.BaseStats.MaxHp));
        AddNpcRow("Base Action", Format(details.BaseStats.Action));
        AddNpcRow("Base Combat", Format(details.BaseStats.Combat));
        AddNpcRow("Base Communication", Format(details.BaseStats.Communication));
        AddNpcRow("Effective Action", Format(details.EffectiveStats.Action));
        AddNpcRow("Effective Combat", Format(details.EffectiveStats.Combat));
        AddNpcRow("Effective Communication", Format(details.EffectiveStats.Communication));
        AddNpcRow("Risk Preference", Format(details.RiskPreference));
        AddNpcRow("Need 生存", Format(details.Needs.Survival));
        AddNpcRow("Need 休息", Format(details.Needs.Rest));
        AddNpcRow("Need 活動", Format(details.Needs.Activity));
        AddNpcRow("Need 交流", Format(details.Needs.Communication));
        AddNpcRow("Need 繁殖", Format(details.Needs.Reproduction));
        AddNpcRow("繁殖Cooldown", $"{details.ReproductionCooldownDays}日");
        AddNpcRow("親", FormatIds(details.ParentAId, details.ParentBId));
        AddNpcRow("子", details.ChildIds.Count == 0 ? "なし" : string.Join(", ", details.ChildIds.Select(id => $"#{id}")));
        AddNpcRow("ConceptMark", details.ConceptMarks.Count == 0
            ? "なし"
            : string.Join(", ", details.ConceptMarks.OrderBy(item => item).Select(TranslateConcept)));
        AddNpcRow("Concept Aura", details.ActiveAuras.Count == 0
            ? "なし"
            : string.Join(", ", details.ActiveAuras.OrderBy(item => item).Select(TranslateConcept)));
        AddNpcRow("Settlement", details.SettlementId.HasValue ? $"#{details.SettlementId}" : "無所属");
        AddNpcRow("Affinity", details.SettlementAffinities.Count == 0
            ? "なし"
            : string.Join(", ", details.SettlementAffinities.Select(item =>
                $"#{item.SettlementId}={item.Affinity:0.00}{(item.IsActiveMembership ? "*" : string.Empty)}")));
        AddNpcRow("Invasion", details.InvasionId.HasValue
            ? $"#{details.InvasionId} / {details.InvasionRole}"
            : "なし");
        AddNpcRow("キル数", details.KillCount.ToString("N0"));
        AddNpcRow("Held Information", $"{details.HeldInformationCount}件");

        foreach (var record in details.ActionHistory
                     .OrderByDescending(item => item.Tick)
                     .ThenByDescending(item => item.MicroRound))
        {
            var when = record.MicroRound > 0
                ? $"D{record.Tick + 1}/R{record.MicroRound}"
                : $"D{record.Tick + 1}";
            var role = record.IsActor ? string.Empty : "（対象）";
            var other = record.OtherNpcId.HasValue ? $" 相手 #{record.OtherNpcId}" : string.Empty;
            var result = record.Success ? string.Empty : "（不成立）";
            var detail = string.IsNullOrWhiteSpace(record.Detail) ? string.Empty : $" [{record.Detail}]";
            _actionHistory.Items.Add($"{when} {Translate(record.Type)}{role}{other}{result}{detail}");
        }

        if (_actionHistory.Items.Count == 0)
        {
            _actionHistory.Items.Add("履歴なし");
        }
    }

    private void RefreshStatistics(
        WorldStatisticsProjection statistics,
        AgeDistributionProjection ageDistribution)
    {
        var total = statistics.ActionSelections.Sum(item => item.Count);
        var totalDeaths = statistics.DeathCauses.Sum(item => item.Count);
        _statisticsSummary.Text =
            $"{statistics.WorldPhase.CurrentPhase}    人口 {statistics.Population}    " +
            $"所属 {statistics.AffiliatedPopulation:N0} ({(statistics.Population == 0 ? 0 : (double)statistics.AffiliatedPopulation / statistics.Population):P1})    " +
            $"Settlement {statistics.Settlements.Count(item => item.IsActive)}    平均年齢 {statistics.AverageAgeYears:0.00}年";
        _actionStatistics.Rows.Clear();
        foreach (var item in statistics.ActionSelections.OrderByDescending(item => item.Count).ThenBy(item => item.Action))
        {
            var ratio = total == 0 ? 0 : (double)item.Count / total;
            _actionStatistics.Rows.Add(TranslateAction(item.Action), item.Count.ToString("N0"), ratio.ToString("P1"));
        }

        _deathCauseStatistics.Rows.Clear();
        foreach (var item in statistics.DeathCauses
                     .OrderByDescending(item => item.Count)
                     .ThenBy(item => item.Cause, StringComparer.Ordinal))
        {
            var ratio = totalDeaths == 0 ? 0 : (double)item.Count / totalDeaths;
            _deathCauseStatistics.Rows.Add(
                TranslateDeathCause(item.Cause),
                item.Count.ToString("N0"),
                ratio.ToString("P1"),
                $"{item.AverageAgeYears:0.00}年");
        }

        if (_deathCauseStatistics.Rows.Count == 0)
        {
            _deathCauseStatistics.Rows.Add("死亡なし", "0", "0.0%", "—");
        }

        _ageDistributionStatistics.Rows.Clear();
        var maximumBucketCount = ageDistribution.Buckets.Count == 0
            ? 0
            : ageDistribution.Buckets.Max(item => item.Count);
        foreach (var bucket in ageDistribution.Buckets)
        {
            var minimumYears = (double)bucket.MinimumAgeDays / _simulationConfig.World.DaysPerYear;
            var maximumYears = (double)bucket.MaximumAgeDaysExclusive / _simulationConfig.World.DaysPerYear;
            var ratio = ageDistribution.Population == 0
                ? 0
                : (double)bucket.Count / ageDistribution.Population;
            var barLength = maximumBucketCount == 0
                ? 0
                : (int)Math.Round(16d * bucket.Count / maximumBucketCount, MidpointRounding.AwayFromZero);
            _ageDistributionStatistics.Rows.Add(
                $"{minimumYears:0.0}–<{maximumYears:0.0}年",
                bucket.Count.ToString("N0"),
                ratio.ToString("P1"),
                barLength == 0 ? string.Empty : new string('■', barLength));
        }

        if (_ageDistributionStatistics.Rows.Count == 0)
        {
            _ageDistributionStatistics.Rows.Add("人口なし", "0", "0.0%", string.Empty);
        }

        _socialStatistics.Rows.Clear();
        AddSocialRow("World", "Phase", statistics.WorldPhase.CurrentPhase.ToString());
        AddSocialRow("World", "安定条件",
            $"CV {statistics.WorldPhase.PopulationCv:0.000} / 不均衡 {statistics.WorldPhase.DemographicImbalance:0.000} / " +
            $"連続 {statistics.WorldPhase.StabilityConsecutiveDays}日");
        AddSocialRow("World", "所属 / 無所属",
            $"{statistics.AffiliatedPopulation:N0} / {statistics.UnaffiliatedPopulation:N0}");
        foreach (var window in statistics.OrderTransitionWindows)
        {
            AddSocialRow("Order比較", window.Window,
                $"{window.Days}日 / 人口平均{window.AveragePopulation:0.0} / 出生{window.Births} 死亡{window.Deaths} " +
                $"戦闘死{window.CombatDeaths} 衝突攻撃{window.CollisionAttacks} / 所属率{window.AverageAffiliationRate:P1}");
        }
        foreach (var group in statistics.AffiliationGroups)
        {
            AddSocialRow("所属比較", group.Group,
                $"人口{group.Population} 年齢{group.AverageAgeYears:0.00} HP{group.AverageHp:0.0} / " +
                $"戦闘死{group.CombatDeaths} 生命力死{group.VitalityDeaths} / " +
                $"休息率{group.RestActionRate:P1} 繁殖{group.ReproductionSuccesses}/{group.ReproductionAttempts} 出生{group.Births}");
        }
        var visibleSettlements = ObservationDisplayPolicy.VisibleSocialSettlements(statistics.Settlements);
        foreach (var settlement in visibleSettlements)
        {
            var status = settlement.IsActive ? "Active" : "Pending";
            AddSocialRow("Settlement", $"#{settlement.Id}",
                $"{status} Center {settlement.Center} / 人口 {settlement.Population} ({settlement.WorldPopulationRatio:P1}) / " +
                $"Core {settlement.CoreOccupancy:P0} / Crowding {settlement.CrowdingPressure:0.00}");
        }
        foreach (var invasion in statistics.Invasions.OrderBy(item => item.Id))
        {
            var status = invasion.EndTick.HasValue ? invasion.Outcome.ToString() :
                invasion.EffectiveTick <= statistics.Tick ? "Active" : "Pending";
            AddSocialRow("Invasion", $"#{invasion.Id}",
                $"#{invasion.AttackSettlementId} → #{invasion.DefenseSettlementId} / {status} / " +
                $"兵力 {invasion.InitialForceSize} / 最大占有 {invasion.MaximumCoreOccupationRate:P1}");
        }
        if (visibleSettlements.Count == 0)
        {
            AddSocialRow("Settlement", "—", "まだ形成されていません");
        }

        _diagnosticStatistics.Rows.Clear();
        AddDiagnosticRow("人口", "現在 / 最小", $"{statistics.Population} / {statistics.MinimumPopulation}");

        foreach (var item in statistics.ReproductionOutcomes)
        {
            AddDiagnosticRow("繁殖", item.Reason, $"{item.Count:N0}件");
        }

        foreach (var item in statistics.TargetedActions)
        {
            AddDiagnosticRow("Targeted", TranslateAction(item.Action),
                $"{item.Attempts:N0} / absent {item.TargetAbsent:N0}");
        }

        foreach (var item in statistics.CombatTypes)
        {
            AddDiagnosticRow("戦闘", Translate(item.Type),
                $"{item.Hits:N0}/{item.Attempts:N0} 平均{item.AverageDamage:0.00}");
        }

        AddDiagnosticRow("知覚", "位置無効化 / Subject purge",
            $"{statistics.Perception.PositionInvalidations:N0} / {statistics.Perception.SubjectPurges:N0}");
        AddDiagnosticRow("知覚", "FIFO eviction", $"{statistics.Perception.HeldInformationEvictions:N0}");
        AddDiagnosticRow("知覚", "Held Info 総/平均/最大",
            $"{statistics.Perception.HeldInformationTotal:N0} / " +
            $"{statistics.Perception.HeldInformationAverage:0.0} / {statistics.Perception.HeldInformationMaximum:N0}");
        foreach (var item in statistics.ConceptMarks)
        {
            AddDiagnosticRow("Concept", TranslateConcept(item.Concept),
                $"mark {item.Holders:N0} / 取得 {item.Acquisitions:N0}");
            AddDiagnosticRow("Exposure", TranslateConcept(item.Concept),
                $"総{item.ExposureTotal:0.0} 平均{item.ExposureAverage:0.00} 最大{item.ExposureMaximum:0.0}");
        }
        AddDiagnosticRow("Hotspot", "候補 / 競合 / 棄却",
            $"{statistics.HotspotCandidates:N0} / {statistics.HotspotConflicts:N0} / {statistics.HotspotRejections:N0}");
        AddDiagnosticRow("暴力", "衝突攻撃 / 抑制",
            $"{statistics.Violence.CollisionAttacks:N0} / " +
            $"{statistics.Violence.SameSettlementSuppressions + statistics.Violence.UnaffiliatedProtectionCollisions + statistics.Violence.OtherSettlementCollisions:N0}");
        AddDiagnosticRow("暴力", "Attack保護 候補/Resolution",
            $"{statistics.Violence.AttackCandidateSuppressions:N0} / {statistics.Violence.AttackResolutionSuppressions:N0}");
        foreach (var scope in statistics.ReproductionScopes)
        {
            AddDiagnosticRow("繁殖空間", scope.Scope,
                $"試行{scope.Attempts:N0} / 成立{scope.Successes:N0} / 失敗{scope.Failures:N0}");
        }
        AddDiagnosticRow("Aura", "適用 / 解除 / 現在",
            $"{statistics.Auras.Applied:N0} / {statistics.Auras.Expired:N0} / {statistics.Auras.CurrentRecipients:N0}");

        _statisticsChart.DaysPerYear = _world.Engine.Config.World.DaysPerYear;
        _statisticsChart.Metrics = _world.Metrics;
    }

    private void RefreshSettlementDetails(WorldStatisticsProjection statistics)
    {
        _settlementProperties.Rows.Clear();
        _settlementFrictionStatistics.Rows.Clear();
        if (!_selectedSettlementId.HasValue)
        {
            _settlementTitle.Text = "マップ上のSettlement中心をクリックしてください";
            return;
        }

        var settlement = statistics.Settlements.FirstOrDefault(item => item.Id == _selectedSettlementId.Value);
        if (settlement is null)
        {
            _settlementTitle.Text = $"Settlement #{_selectedSettlementId.Value} — データなし";
            return;
        }

        var status = settlement.IsActive ? "Active" : settlement.DissolvedTick.HasValue ? "消滅" : "Pending";
        _settlementTitle.Text = $"Settlement #{settlement.Id} — {status}";
        AddSettlementRow("中心", settlement.Center.ToString());
        AddSettlementRow("形成日", $"D{settlement.FormedTick + 1}");
        AddSettlementRow("Founder数", settlement.FounderCount.ToString("N0"));
        AddSettlementRow("人口", $"{settlement.Population:N0} ({settlement.WorldPopulationRatio:P1})");
        AddSettlementRow("Core占有率", settlement.CoreOccupancy.ToString("P1"));
        AddSettlementRow("Crowding", settlement.CrowdingPressure.ToString("0.000"));
        AddSettlementRow("Crowding連続", $"{settlement.CrowdingConsecutiveDays:N0}日");
        if (settlement.DissolvedTick.HasValue)
        {
            AddSettlementRow("消滅日", $"D{settlement.DissolvedTick.Value + 1}");
            AddSettlementRow("消滅理由", settlement.DissolutionReason ?? "—");
            AddSettlementRow("統合先", settlement.IntegratedIntoSettlementId.HasValue
                ? $"#{settlement.IntegratedIntoSettlementId}"
                : "なし");
        }

        foreach (var friction in ObservationDisplayPolicy.FrictionsForSettlement(statistics.Frictions, settlement.Id))
        {
            var other = friction.FirstSettlementId == settlement.Id
                ? friction.SecondSettlementId
                : friction.FirstSettlementId;
            _settlementFrictionStatistics.Rows.Add(
                $"#{other}",
                friction.CurrentFriction.ToString("0.0"),
                friction.CollisionEvents.ToString("N0"),
                friction.ExplicitThreatEvents.ToString("N0"),
                friction.LifetimeDecay.ToString("0.0"),
                friction.LastFrictionEventTick < 0 ? "—" : $"D{friction.LastFrictionEventTick + 1}");
        }

        if (_settlementFrictionStatistics.Rows.Count == 0)
        {
            _settlementFrictionStatistics.Rows.Add("なし", "0.0", "0", "0", "0.0", "—");
        }
    }

    private void AddSettlementRow(string property, string value) =>
        _settlementProperties.Rows.Add(property, value);

    private void UpdateCommandState()
    {
        _runButton.Text = _running ? "一時停止" : "再生";
        _stepButton.Enabled = !_running && !_advancing;
        _newWorldButton.Enabled = !_worldCreationRequested;
        _speed.Enabled = !_advancing;
    }

    private void AddNpcRow(string name, string value) => _npcProperties.Rows.Add(name, value);

    private void AddDiagnosticRow(string category, string metric, string value) =>
        _diagnosticStatistics.Rows.Add(category, metric, value);

    private void AddSocialRow(string category, string subject, string value) =>
        _socialStatistics.Rows.Add(category, subject, value);

    private int AgeDistributionBucketDays() => Math.Max(
        1,
        (int)Math.Round(
            _simulationConfig.World.DaysPerYear * _appConfig.AgeDistributionBinYears,
            MidpointRounding.AwayFromZero));

    private void ShowOperationError(string message, Exception exception)
    {
        MessageBox.Show(this, $"{message}{Environment.NewLine}{exception.Message}",
            ReleaseIdentity.DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static DataGridView CreateReadOnlyGrid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle
    };

    private static string Format(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatIds(params long?[] ids)
    {
        var values = ids.Where(item => item.HasValue).Select(item => $"#{item!.Value}").ToArray();
        return values.Length == 0 ? "なし" : string.Join(", ", values);
    }

    private static string FormatEvent(SimulationEvent item)
    {
        var actor = item.ActorId.HasValue ? $"#{item.ActorId}" : "世界";
        var target = item.TargetId.HasValue ? $" → #{item.TargetId}" : string.Empty;
        var result = item.Success ? string.Empty : "（不成立）";
        return $"D{item.Tick + 1} {Translate(item.Type)} {actor}{target}{result}";
    }

    private static string Translate(SimulationEventType type) => type switch
    {
        SimulationEventType.Birth => "誕生",
        SimulationEventType.BirthFailure => "出生失敗",
        SimulationEventType.Death => "死亡",
        SimulationEventType.Attack => "攻撃",
        SimulationEventType.CollisionAttack => "衝突攻撃",
        SimulationEventType.Counterattack => "反撃",
        SimulationEventType.Communication => "交流",
        SimulationEventType.ReproductionAttempt => "繁殖試行",
        SimulationEventType.ReproductionSuccess => "繁殖成立",
        SimulationEventType.ReproductionFailure => "繁殖不成立",
        SimulationEventType.Rest => "休息",
        SimulationEventType.ConceptMarkAcquired => "概念刻印",
        SimulationEventType.Flee => "逃走",
        SimulationEventType.Pursuit => "追撃",
        SimulationEventType.Move => "移動",
        SimulationEventType.MoveFailed => "移動失敗",
        SimulationEventType.Idle => "待機",
        SimulationEventType.TargetPositionInvalidated => "対象位置無効化",
        SimulationEventType.IntentReplaced => "意図再決定",
        SimulationEventType.SettlementFormed => "Settlement成立",
        SimulationEventType.SettlementDissolved => "Settlement消滅",
        SimulationEventType.SettlementIntegrated => "Settlement統合",
        SimulationEventType.AffiliationChanged => "所属変更",
        SimulationEventType.WorldPhaseChanged => "World Phase移行",
        SimulationEventType.CollisionSuppressed => "衝突抑制",
        SimulationEventType.AttackSuppressed => "攻撃抑制",
        SimulationEventType.SettlementFrictionChanged => "Friction変動",
        SimulationEventType.InvasionStarted => "Invasion開始",
        SimulationEventType.InvasionEnded => "Invasion終結",
        SimulationEventType.AuraApplied => "Aura適用",
        SimulationEventType.AuraExpired => "Aura解除",
        _ => type.ToString()
    };

    private static string TranslateAction(ActionKind action) => action switch
    {
        ActionKind.Idle => "待機",
        ActionKind.Move => "移動",
        ActionKind.Rest => "休息",
        ActionKind.Communication => "交流",
        ActionKind.Attack => "攻撃",
        ActionKind.Flee => "逃走",
        ActionKind.Reproduction => "繁殖",
        _ => action.ToString()
    };

    private static string TranslateDeathCause(string cause)
    {
        if (string.Equals(cause, "vitality", StringComparison.Ordinal))
        {
            return "加齢・生命力";
        }

        const string combatPrefix = "combat:";
        if (cause.StartsWith(combatPrefix, StringComparison.Ordinal) &&
            Enum.TryParse<SimulationEventType>(cause[combatPrefix.Length..], out var combatType))
        {
            return $"戦闘（{Translate(combatType)}）";
        }

        return cause;
    }

    private static string TranslateConcept(ConceptKind concept) => concept switch
    {
        ConceptKind.Struggle => "闘争",
        ConceptKind.Survival => "生存",
        ConceptKind.Communication => "交流",
        _ => concept.ToString()
    };

    private sealed record SpeedOption(string Label, int TicksPerFrame)
    {
        public override string ToString() => Label;
    }
}
