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
    private readonly WorldStatisticsChartPanel _statisticsChart = new() { Dock = DockStyle.Fill };
    private WorldSession _world;
    private long? _selectedNpcId;
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
            new SpeedOption("通常", 1),
            new SpeedOption("高速 ×10", 10),
            new SpeedOption("高速 ×50", 50),
            new SpeedOption("Max", 200)
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
        if (_tabs.TabPages.Count != 3 || _diagnosticStatistics.Columns.Count != 3 || !_newWorldButton.Enabled)
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

        var tables = new TabControl { Dock = DockStyle.Fill };
        tables.TabPages.Add(new TabPage("行動選択") { Controls = { _actionStatistics } });
        tables.TabPages.Add(new TabPage("死因") { Controls = { _deathCauseStatistics } });
        tables.TabPages.Add(new TabPage("年齢分布") { Controls = { _ageDistributionStatistics } });
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
            _map.SelectedNpcId = null;
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
        _populationLabel.Text = $"  人口 {snapshot.Npcs.Count}";
        _seedLabel.Text = $"  Seed {_world.Info.Seed}";
        Text = $"{ReleaseIdentity.DisplayName} — 世界 #{worldNumber}";

        _events.BeginUpdate();
        _events.Items.Clear();
        foreach (var item in snapshot.RecentEvents.Reverse())
        {
            _events.Items.Add(FormatEvent(item));
        }
        _events.EndUpdate();
        RefreshNpcDetails();
        RefreshStatistics(statistics, ageDistribution);
    }

    private void SelectNpc(long npcId)
    {
        _selectedNpcId = npcId;
        _map.SelectedNpcId = npcId;
        _tabs.SelectedIndex = 1;
        RefreshNpcDetails();
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
            $"人口 {statistics.Population}    平均年齢 {statistics.AverageAgeYears:0.00}年    " +
            $"死亡 {totalDeaths:N0}件    行動選択 {total:N0}回";
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

        _statisticsChart.DaysPerYear = _world.Engine.Config.World.DaysPerYear;
        _statisticsChart.Metrics = _world.Metrics;
    }

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
        SimulationEventType.ConceptMarkAcquired => "概念刻印",
        SimulationEventType.Flee => "逃走",
        SimulationEventType.Pursuit => "追撃",
        SimulationEventType.Move => "移動",
        SimulationEventType.MoveFailed => "移動失敗",
        SimulationEventType.Idle => "待機",
        SimulationEventType.TargetPositionInvalidated => "対象位置無効化",
        SimulationEventType.IntentReplaced => "意図再決定",
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
