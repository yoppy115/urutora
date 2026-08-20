using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Text.RegularExpressions;

[assembly: AssemblyTitle("ARK Server Manager")]
[assembly: AssemblyDescription("Local control center for an ARK dedicated server")]
[assembly: AssemblyCompany("Local Tools")]
[assembly: AssemblyProduct("ARK Server Manager")]
[assembly: AssemblyVersion("1.16.0.0")]
[assembly: AssemblyFileVersion("1.16.0.0")]

namespace ArkServerManager
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            string renderResourcePreview = args.FirstOrDefault(delegate(string a) { return a.StartsWith("--render-resource-preview=", StringComparison.OrdinalIgnoreCase); });
            if (renderResourcePreview != null)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                MainForm.RenderResourcePreview(renderResourcePreview.Substring(renderResourcePreview.IndexOf('=') + 1));
                return;
            }
            string renderPreview = args.FirstOrDefault(delegate(string a) { return a.StartsWith("--render-map-preview=", StringComparison.OrdinalIgnoreCase); });
            if (renderPreview != null)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                MainForm.RenderMapPreview(renderPreview.Substring(renderPreview.IndexOf('=') + 1));
                return;
            }
            if (args.Any(delegate(string a) { return a == "--map-preview"; }))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                MainForm.RunMapPreview();
                return;
            }
            if (args.Any(delegate(string a) { return a == "--self-test"; }))
            {
                try
                {
                    AppSettings s = AppSettings.Load();
                    Console.WriteLine(File.Exists(s.ExecutablePath) ? "SELF_TEST_OK" : "SELF_TEST_FAILED: executable not found");
                    Environment.ExitCode = File.Exists(s.ExecutablePath) ? 0 : 2;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SELF_TEST_FAILED: " + ex.Message);
                    Environment.ExitCode = 1;
                }
                return;
            }

            bool created;
            using (Mutex mutex = new Mutex(true, "Local\\ARKServerManager-Yoppy115", out created))
            {
                if (!created)
                {
                    MessageBox.Show("ARK Server Manager はすでに起動しています。", "ARK Server Manager",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    MessageBox.Show(e.Exception.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };
                MainForm form = new MainForm();
                if (args.Any(delegate(string a) { return a == "--settings"; })) form.OpenSettings();
                if (args.Any(delegate(string a) { return a == "--game-settings"; })) form.OpenGameSettings();
                if (args.Any(delegate(string a) { return a == "--server-control"; })) form.OpenServerControl();
                Application.Run(form);
            }
        }
    }

    internal sealed class DinoCatalogEntry
    {
        public readonly string Name;
        public readonly string ClassName;
        public DinoCatalogEntry(string name, string className) { Name = name; ClassName = className; }
    }

    internal static class FjordurDinoCatalog
    {
        private static DinoCatalogEntry E(string name, string className) { return new DinoCatalogEntry(name, className); }

        public static readonly DinoCatalogEntry[] Entries = new DinoCatalogEntry[] {
            E("アフリカマイマイ（全種）", "Achatina_Character"),
            E("アロサウルス（全種）", "Allo_Character"),
            E("アンモナイト", "Ammonite_Character_C"),
            E("アンドリューサルクス", "Andrewsarchus_Character_BP_C"),
            E("アンコウ", "Angler_Character_BP_C"),
            E("アンキロサウルス（全種）", "Ankylo_Character"),
            E("アラネオ（クモ）", "SpiderS_Character_BP_C"),
            E("始祖鳥", "Archa_Character_BP_C"),
            E("アルゲンタヴィス（全種）", "Argent_Character"),
            E("アースロプレウラ（全種）", "Arthro_Character"),
            E("バリオニクス", "Baryonyx_Character_BP_C"),
            E("バジリスク", "Basilisk_Character_BP_C"),
            E("バシロサウルス", "Basilosaurus_Character_BP_C"),
            E("ベールゼブフォ", "Toad_Character_BP_C"),
            E("ブロントサウルス（全種）", "Sauropod_Character"),
            E("バルブドッグ", "LanternPug_Character_BP_C"),
            E("カルボネミス（全種）", "Turtle_Character"),
            E("カルカロドントサウルス", "Carcha_Character_BP_C"),
            E("カルノタウルス", "Carno_Character_BP_C"),
            E("カストロイデス", "Beaver_Character_BP_C"),
            E("カリコテリウム", "Chalico_Character_BP_C"),
            E("クニダリア", "Cnidaria_Character_BP_C"),
            E("シーラカンス（全種）", "Coel_Character"),
            E("コンプソグナトゥス", "Compy_Character_BP_C"),
            E("ダエオドン（全種）", "Daeodon_Character"),
            E("デイノニクス", "Deinonychus_Character_BP_C"),
            E("ディロフォサウルス（全種）", "Dilo_Character"),
            E("ディメトロドン", "Dimetro_Character_BP_C"),
            E("ディモルフォドン", "Dimorph_Character_BP_C"),
            E("ディプロカウルス", "Diplocaulus_Character_BP_C"),
            E("ディプロドクス（全種）", "Diplodocus_Character"),
            E("ダイアベア（全種）", "Direbear_Character"),
            E("ダイアウルフ（全種）", "Direwolf_Character"),
            E("ドードー（全種）", "Dodo_Character"),
            E("ドエディクルス（全種）", "Doed_Character"),
            E("フンコロガシ（全種）", "DungBeetle_Character"),
            E("ダンクルオステウス（全種）", "Dunkle_Character"),
            E("デンキウナギ", "Eel_Character_BP_C"),
            E("エクウス（全種）", "Equus_Character"),
            E("ウミサソリ", "Euryp_Character_C"),
            E("フェザーライト", "LanternBird_Character_BP_C"),
            E("フェロクス", "Shapeshifter_Small_Character_BP_C"),
            E("ガチャ", "Gacha_Character_BP_C"),
            E("ガリミムス", "Galli_Character_BP_C"),
            E("ガスバッグ（全種）", "GasBags_Character"),
            E("ジャイアントクイーンビー", "Bee_Queen_Character_BP_C"),
            E("ギガノトサウルス（全種）", "Gigant_Character"),
            E("ギガントピテクス", "Bigfoot_Character_BP_C"),
            E("グロウバグ", "Lightbug_Character_BaseBP_C"),
            E("グローテール", "LanternLizard_Character_BP_C"),
            E("グリフィン", "Griffin_Character_BP_C"),
            E("ヘスペロルニス", "Hesperornis_Character_BP_C"),
            E("ヒエノドン", "Hyaenodon_Character_BP_C"),
            E("イクチオルニス", "Ichthyornis_Character_BP_C"),
            E("イクチオサウルス", "Dolphin_Character_BP_C"),
            E("イグアノドン（全種）", "Iguanodon_Character"),
            E("カイルクペンギン", "Kairuku_Character_BP_C"),
            E("カプロスクス", "Kaprosuchus_Character_BP_C"),
            E("カルキノス", "Crab_Character_BP_C"),
            E("ケントロサウルス", "Kentro_Character_BP_C"),
            E("ヤツメウナギ", "Lamprey_Character_C"),
            E("ヒル（全種）", "Leech_Character"),
            E("リードシクティス", "Leedsichthys_Character_BP_C"),
            E("リオプレウロドン", "Liopleurodon_Character_BP_C"),
            E("リストロサウルス（全種）", "Lystro_Character"),
            E("メイウィング", "MilkGlider_Character_BP_C"),
            E("マグマサウルス", "Cherufe_Character_BP_C"),
            E("マンモス", "Mammoth_Character_BP_C"),
            E("マンタ", "Manta_Character_BP_C"),
            E("カマキリ", "Mantis_Character_BP_C"),
            E("メガケロン", "GiantTurtle_Character_BP_C"),
            E("メガラニア（全種）", "Megalania_Character"),
            E("メガロケロス", "Stag_Character_BP_C"),
            E("メガロドン", "Megalodon_Character_BP_C"),
            E("メガロサウルス（全種）", "Megalosaurus_Character"),
            E("メガネウラ（全種）", "Dragonfly_Character"),
            E("メガテリウム", "Megatherium_Character_BP_C"),
            E("メソピテクス", "Monkey_Character_BP_C"),
            E("ミクロラプトル", "Microraptor_Character_BP_C"),
            E("モササウルス", "Mosa_Character_BP_C"),
            E("モスコプス（全種）", "Moschops_Character"),
            E("オニコニクテリス", "Bat_Character_BP_C"),
            E("カワウソ（全種）", "Otter_Character"),
            E("オヴィラプトル", "Oviraptor_Character_BP_C"),
            E("ヒツジ（全種）", "Sheep_Character"),
            E("パキケファロサウルス", "Pachy_Character_BP_C"),
            E("パキリノサウルス", "Pachyrhino_Character_BP_C"),
            E("パラケラテリウム（全種）", "Paracer_Character"),
            E("パラキートの魚群", "MicrobeSwarmChar_BP_C"),
            E("パラサウロロフス（全種）", "Para_Character"),
            E("ペゴマスタクス", "Pegomastax_Character_BP_C"),
            E("ペラゴルニス", "Pela_Character_BP_C"),
            E("フィオミア", "Phiomia_Character_BP_C"),
            E("ピラニア（全種）", "Piranha_Character"),
            E("プレシオサウルス", "Plesiosaur_Character_BP_C"),
            E("ショートフェイスベア", "Direbear_Character_Polar_C"),
            E("プロコプトドン（全種）", "Procoptodon_Character"),
            E("プテラノドン", "Ptero_Character_BP_C"),
            E("プルモノスコルピウス（全種）", "Scorpion_Character"),
            E("プルロヴィア（全種）", "Purlovia_Character"),
            E("ケツァルコアトルス（全種）", "Quetz_Character"),
            E("ラベジャー", "CaveWolf_Character_BP_C"),
            E("ラプトル（全種）", "Raptor_Character"),
            E("ティラノサウルス（全種）", "Rex_Character"),
            E("ロックドレイク", "RockDrake_Character_BP_C"),
            E("ロールラット", "MoleRat_Character_BP_C"),
            E("サーベルタイガー（全種）", "Saber_Character"),
            E("セイバートゥースサーモン（全種）", "Salmon_Character"),
            E("サルコスクス（全種）", "Sarco_Character"),
            E("シーカー", "Pteroteuthis_Character_BP_C"),
            E("シャドウメイン", "LionfishLion_Character_BP_C"),
            E("シャインホーン", "LanternGoat_Character_BP_C"),
            E("雪フクロウ（全種）", "Owl_Character"),
            E("スピノサウルス（全種）", "Spino_Character"),
            E("ステゴサウルス（全種）", "Stego_Character"),
            E("タペヤラ（全種）", "Tapejara_Character"),
            E("テラーバード", "TerrorBird_Character_BP_C"),
            E("テリジノサウルス", "Therizino_Character_BP_C"),
            E("モロクトカゲ", "SpineyLizard_Character_BP_C"),
            E("ティラコレオ（全種）", "Thylacoleo_Character"),
            E("ティタノボア（全種）", "BoaFrill_Character"),
            E("ティタノミルマ（全種）", "Ant_Character"),
            E("ティタノサウルス", "Titanosaur_Character_BP_C"),
            E("トリケラトプス（全種）", "Trike_Character"),
            E("三葉虫", "Trilobite_Character_C"),
            E("トロオドン", "Troodon_Character_BP_C"),
            E("トロペオグナトゥス", "Tropeognathus_Character_BP_C"),
            E("トゥソテウティス", "Tusoteuthis_Character_BP_C"),
            E("ユニコーン", "Equus_Character_BP_Unicorn_C"),
            E("ベロナサウルス", "Spindles_Character_BP_C"),
            E("ハゲワシ", "Vulture_Character_BP_C"),
            E("ケブカサイ（全種）", "Rhino_Character"),
            E("イエティ", "Yeti_Character_BP_C"),
            E("ユウティラヌス（全種）", "Yutyrannus_Character"),
            E("デスモダス", "Desmodus_Character_BP_C"),
            E("フィヨルドホーク", "Fjordhawk_Character_BP_C"),

            E("ファイアワイバーン", "Wyvern_Character_BP_Fire_C"),
            E("ライトニングワイバーン", "Wyvern_Character_BP_Lightning_C"),
            E("ポイズンワイバーン", "Wyvern_Character_BP_Poison_C"),
            E("アイスワイバーン", "Ragnarok_Wyvern_Override_Ice_C"),
            E("ワイバーン（全種）", "Wyvern_"),
            E("オイルジャグバグ", "Jugbug_Oil_Character_BP_C"),
            E("ウォータージャグバグ", "Jugbug_Water_Character_BP_C"),

            E("アルファ・カルノタウルス", "MegaCarno_Character_BP_C"),
            E("アルファ・カルキノス", "MegaCrab_Character_BP_C"),
            E("アルファ・リードシクティス", "Alpha_Leedsichthys_Character_BP_C"),
            E("アルファ・メガロドン", "MegaMegalodon_Character_BP_C"),
            E("アルファ・モササウルス", "Mosa_Character_BP_Mega_C"),
            E("アルファ・ラプトル", "MegaRaptor_Character_BP_C"),
            E("アルファ・ティラノサウルス", "MegaRex_Character_BP_C"),
            E("アルファ・トゥソテウティス", "Mega_Tusoteuthis_Character_BP_C"),
            E("アルファ・バジリスク", "MegaBasilisk_Character_BP_C"),

            E("変種アフリカマイマイ", "Achatina_Character_BP_Aberrant_C"),
            E("変種アースロプレウラ", "Arthro_Character_BP_Aberrant_C"),
            E("変種カルボネミス", "Turtle_Character_BP_Aberrant_C"),
            E("変種シーラカンス", "Coel_Character_BP_Aberrant_C"),
            E("変種ディプロドクス", "Diplodocus_Character_BP_Aberrant_C"),
            E("変種ダイアベア", "Direbear_Character_BP_Aberrant_C"),
            E("変種ドードー", "Dodo_Character_BP_Aberrant_C"),
            E("変種ドエディクルス", "Doed_Character_BP_Aberrant_C"),
            E("変種フンコロガシ", "DungBeetle_Character_BP_Aberrant_C"),
            E("変種エクウス", "Equus_Character_BP_Aberrant_C"),
            E("変種イグアノドン", "Iguanodon_Character_BP_Aberrant_C"),
            E("変種リストロサウルス", "Lystro_Character_BP_Aberrant_C"),
            E("変種メガラニア", "Megalania_Character_BP_Aberrant_C"),
            E("変種メガロサウルス", "Megalosaurus_Character_BP_Aberrant_C"),
            E("変種メガネウラ", "Dragonfly_Character_BP_Aberrant_C"),
            E("変種モスコプス", "Moschops_Character_BP_Aberrant_C"),
            E("変種カワウソ", "Otter_Character_BP_Aberrant_C"),
            E("変種ヒツジ", "Sheep_Character_BP_Aberrant_C"),
            E("変種パラケラテリウム", "Paracer_Character_BP_Aberrant_C"),
            E("変種パラサウロロフス", "Para_Character_BP_Aberrant_C"),
            E("変種ピラニア", "Piranha_Character_BP_Aberrant_C"),
            E("変種プルモノスコルピウス", "Scorpion_Character_BP_Aberrant_C"),
            E("変種プルロヴィア", "Purlovia_Character_BP_Aberrant_C"),
            E("変種ラプトル", "Raptor_Character_BP_Aberrant_C"),
            E("変種セイバートゥースサーモン", "Salmon_Character_Aberrant_C"),
            E("変種サルコスクス", "Sarco_Character_BP_Aberrant_C"),
            E("変種スピノサウルス", "Spino_Character_BP_Aberrant_C"),
            E("変種ステゴサウルス", "Stego_Character_BP_Aberrant_C"),
            E("変種ティタノボア", "BoaFrill_Character_BP_Aberrant_C"),
            E("変種トリケラトプス", "Trike_Character_BP_Aberrant_C"),

            E("Rアロサウルス", "Allo_Character_BP_Rockwell_C"),
            E("Rブロントサウルス", "Sauropod_Character_BP_Rockwell_C"),
            E("Rカルボネミス", "Turtle_Character_BP_Rockwell_C"),
            E("Rダエオドン", "Daeodon_Character_BP_Eden_C"),
            E("Rディロフォサウルス", "Dilo_Character_BP_Rockwell_C"),
            E("Rダイアウルフ", "Direwolf_Character_BP_Eden_C"),
            E("Rエクウス", "Equus_Character_BP_Eden_C"),
            E("Rガスバッグ", "GasBags_Character_BP_Eden_C"),
            E("Rパラサウロロフス", "Para_Character_BP_Eden_C"),
            E("Rプロコプトドン", "Procoptodon_Character_BP_Eden_C"),
            E("R雪フクロウ", "Owl_Character_BP_Eden_C"),
            E("Rティラコレオ", "Thylacoleo_Character_BP_Eden_C"),

            E("Xアロサウルス", "Volcano_Allo_Character_BP_C"),
            E("Xアンキロサウルス", "Volcano_Ankylo_Character_BP_C"),
            E("Xダンクルオステウス", "Ocean_Dunkle_Character_BP_C"),
            E("Xパラサウロロフス", "Bog_Para_Character_BP_C"),
            E("Xラプトル", "Bog_Raptor_Character_BP_C"),
            E("Xロックエレメンタル", "Volcano_Golem_Character_BP_C"),
            E("Xサーベルタイガー", "Snow_Saber_Character_BP_C"),
            E("Xセイバートゥースサーモン", "Lunar_Salmon_Character_BP_C"),
            E("Xタペヤラ", "Bog_Tapejara_Character_BP_C"),
            E("Xトリケラトプス", "Volcano_Trike_Character_BP_C"),
            E("Xケブカサイ", "Snow_Rhino_Character_BP_C"),
            E("Xユウティラヌス", "Snow_Yutyrannus_Character_BP_C"),

            E("TEKパラサウロロフス", "BionicPara_Character_BP_C"),
            E("TEKケツァルコアトルス", "BionicQuetz_Character_BP_C"),
            E("TEKラプトル", "BionicRaptor_Character_BP_C"),
            E("TEKティラノサウルス", "BionicRex_Character_BP_C"),
            E("TEKステゴサウルス", "BionicStego_Character_BP_C"),

            E("ベイラ", "Fjordur_Beyla_Character_BP_C"),
            E("フェンリル", "Fenrir_Character_BP_C"),
            E("ハティ／スコル", "Fjordur_WolfTwins_Character_BP_C"),
            E("スタインヨルン", "Fjordur_IceBear_Character_BP_C"),
            E("フェンリルサル", "Fjordur_Fenrir_Character_BP_Boss_C")
        };

        public static DinoCatalogEntry[] GetJapaneseOrderedEntries()
        {
            DinoCatalogEntry[] ordered = (DinoCatalogEntry[])Entries.Clone();
            Array.Sort(ordered, delegate(DinoCatalogEntry left, DinoCatalogEntry right)
            {
                return CultureInfo.GetCultureInfo("ja-JP").CompareInfo.Compare(
                    JapaneseSortKey(left.Name), JapaneseSortKey(right.Name),
                    CompareOptions.IgnoreCase | CompareOptions.IgnoreKanaType | CompareOptions.IgnoreWidth);
            });
            return ordered;
        }

        internal static string JapaneseSortKey(string name)
        {
            string value = name ?? "";
            if (value.StartsWith("TEK", StringComparison.OrdinalIgnoreCase)) value = "てっく" + value.Substring(3);
            else if (value.StartsWith("R", StringComparison.OrdinalIgnoreCase)) value = "あーる" + value.Substring(1);
            else if (value.StartsWith("X", StringComparison.OrdinalIgnoreCase)) value = "えっくす" + value.Substring(1);
            value = value.Replace("始祖鳥", "しそちょう")
                .Replace("三葉虫", "さんようちゅう")
                .Replace("雪フクロウ", "ゆきふくろう")
                .Replace("変種", "へんしゅ");
            return value;
        }
    }

    internal sealed class DinoLocationRecord
    {
        public int Level;
        public string DinoId = "";
        public bool IsWild;
        public string Area = "";
        public double Latitude;
        public double Longitude;
        public double X;
        public double Y;
        public double Z;
        public bool HasGps;
    }

    internal sealed class DinoSaveSnapshot
    {
        public int Count = -1;
        public DateTimeOffset? SavedAt;
        public readonly List<DinoLocationRecord> Locations = new List<DinoLocationRecord>();
    }

    internal static class DinoHistoryLogic
    {
        internal const int RequiredSaveCount = 5;
        private const double StationaryDistance = 100D;

        public static HashSet<string> FindStationaryWildDinoIds(DinoSaveSnapshot current, IList<DinoSaveSnapshot> olderSnapshots)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.Ordinal);
            if (current == null || olderSnapshots == null || olderSnapshots.Count != RequiredSaveCount - 1) return result;

            foreach (DinoLocationRecord candidate in current.Locations)
            {
                if (!candidate.IsWild || String.IsNullOrWhiteSpace(candidate.DinoId) || candidate.DinoId == "0:0") continue;
                bool stationary = true;
                foreach (DinoSaveSnapshot snapshot in olderSnapshots)
                {
                    DinoLocationRecord previous = snapshot.Locations.FirstOrDefault(delegate(DinoLocationRecord item)
                    {
                        return item.IsWild && String.Equals(item.DinoId, candidate.DinoId, StringComparison.Ordinal);
                    });
                    if (previous == null || DistanceSquared(candidate, previous) >= StationaryDistance * StationaryDistance)
                    {
                        stationary = false;
                        break;
                    }
                }
                if (stationary) result.Add(candidate.DinoId);
            }
            return result;
        }

        private static double DistanceSquared(DinoLocationRecord left, DinoLocationRecord right)
        {
            double dx = left.X - right.X;
            double dy = left.Y - right.Y;
            double dz = left.Z - right.Z;
            return dx * dx + dy * dy + dz * dz;
        }
    }

    internal sealed class AppSettings
    {
        public string ServerRoot = @"D:\arkserver";
        public string MapName = "Fjordur";
        public string SessionName = "yoppy115";
        public int MaxPlayers = 70;
        public int GamePort = 7777;
        public int QueryPort = 27015;
        public int RconPort = 27020;
        public string ServerPassword = "";
        public string AdminPassword = "";
        public string AdditionalArguments = "-server -log -NoBattlEye";
        public bool AutoRestart = false;
        public int RestartDelaySeconds = 15;
        public bool ServerPVE = false;
        public bool UseSingleplayerSettings = false;
        public double DifficultyOffset = 1.0;
        public double OverrideOfficialDifficulty = 5.0;
        public double XPMultiplier = 1.0;
        public double TamingSpeedMultiplier = 1.0;
        public double HarvestAmountMultiplier = 1.0;
        public double ResourcesRespawnPeriodMultiplier = 1.0;
        public double DinoCountMultiplier = 1.0;
        public double DayCycleSpeedScale = 1.0;
        public double DayTimeSpeedScale = 1.0;
        public double NightTimeSpeedScale = 1.0;
        public double MatingIntervalMultiplier = 1.0;
        public double EggHatchSpeedMultiplier = 1.0;
        public double BabyMatureSpeedMultiplier = 1.0;
        public bool AllowThirdPersonPlayer = true;
        public bool ServerCrosshair = true;
        public bool ShowMapPlayerLocation = true;
        public bool AllowFlyerCarryPVE = false;
        public bool DisableStructurePlacementCollision = false;
        public double ResourceNoReplenishRadiusStructures = 1.0;
        public bool DisableCryopodCooldown = true;
        public bool ScheduledStartEnabled = false;
        public DateTime ScheduledStartAt = DateTime.Now.AddHours(1);
        public bool ScheduledStopEnabled = false;
        public DateTime ScheduledStopAt = DateTime.Now.AddHours(2);
        public bool DailyStartEnabled = false;
        public TimeSpan DailyStartTime = new TimeSpan(8, 0, 0);
        public DateTime DailyStartLastRunAt = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local);
        public bool DailyStopEnabled = false;
        public TimeSpan DailyStopTime = new TimeSpan(23, 0, 0);
        public DateTime DailyStopLastRunAt = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local);
        public int RemotePort = 8765;
        public string RemotePin = "";
        public string RemoteTailnetIp = "100.100.176.17";

        public string ExecutablePath
        {
            get { return Path.Combine(ServerRoot ?? "", @"ShooterGame\Binaries\Win64\ShooterGameServer.exe"); }
        }

        public string ConfigDirectory
        {
            get { return Path.Combine(ServerRoot ?? "", @"ShooterGame\Saved\Config\WindowsServer"); }
        }

        public string LogPath
        {
            get { return Path.Combine(ServerRoot ?? "", @"ShooterGame\Saved\Logs\ShooterGame.log"); }
        }

        private static string SettingsPath
        {
            get
            {
                string overrideDirectory = Environment.GetEnvironmentVariable("ARK_MANAGER_SETTINGS_DIR");
                if (!String.IsNullOrWhiteSpace(overrideDirectory)) return Path.Combine(overrideDirectory, "settings.xml");
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ARK Server Manager", "settings.xml");
            }
        }

        public static AppSettings Load()
        {
            AppSettings s = new AppSettings();
            if (!File.Exists(SettingsPath))
            {
                s.ImportExistingIni();
                return s;
            }

            XElement x = XElement.Load(SettingsPath);
            s.ServerRoot = Value(x, "ServerRoot", s.ServerRoot);
            if (x.Element("ServerPVE") == null || x.Element("DisableStructurePlacementCollision") == null) s.ImportGameplayIni();
            s.MapName = Value(x, "MapName", s.MapName);
            s.SessionName = Value(x, "SessionName", s.SessionName);
            s.MaxPlayers = IntValue(x, "MaxPlayers", s.MaxPlayers);
            s.GamePort = IntValue(x, "GamePort", s.GamePort);
            s.QueryPort = IntValue(x, "QueryPort", s.QueryPort);
            s.RconPort = IntValue(x, "RconPort", s.RconPort);
            s.ServerPassword = Unprotect(Value(x, "ServerPassword", ""));
            s.AdminPassword = Unprotect(Value(x, "AdminPassword", ""));
            s.AdditionalArguments = Value(x, "AdditionalArguments", s.AdditionalArguments);
            s.AutoRestart = BoolValue(x, "AutoRestart", s.AutoRestart);
            s.RestartDelaySeconds = IntValue(x, "RestartDelaySeconds", s.RestartDelaySeconds);
            s.ServerPVE = BoolValue(x, "ServerPVE", s.ServerPVE);
            s.UseSingleplayerSettings = BoolValue(x, "UseSingleplayerSettings", s.UseSingleplayerSettings);
            s.DifficultyOffset = DoubleValue(x, "DifficultyOffset", s.DifficultyOffset);
            s.OverrideOfficialDifficulty = DoubleValue(x, "OverrideOfficialDifficulty", s.OverrideOfficialDifficulty);
            s.XPMultiplier = DoubleValue(x, "XPMultiplier", s.XPMultiplier);
            s.TamingSpeedMultiplier = DoubleValue(x, "TamingSpeedMultiplier", s.TamingSpeedMultiplier);
            s.HarvestAmountMultiplier = DoubleValue(x, "HarvestAmountMultiplier", s.HarvestAmountMultiplier);
            s.ResourcesRespawnPeriodMultiplier = DoubleValue(x, "ResourcesRespawnPeriodMultiplier", s.ResourcesRespawnPeriodMultiplier);
            s.DinoCountMultiplier = DoubleValue(x, "DinoCountMultiplier", s.DinoCountMultiplier);
            s.DayCycleSpeedScale = DoubleValue(x, "DayCycleSpeedScale", s.DayCycleSpeedScale);
            s.DayTimeSpeedScale = DoubleValue(x, "DayTimeSpeedScale", s.DayTimeSpeedScale);
            s.NightTimeSpeedScale = DoubleValue(x, "NightTimeSpeedScale", s.NightTimeSpeedScale);
            s.MatingIntervalMultiplier = DoubleValue(x, "MatingIntervalMultiplier", s.MatingIntervalMultiplier);
            s.EggHatchSpeedMultiplier = DoubleValue(x, "EggHatchSpeedMultiplier", s.EggHatchSpeedMultiplier);
            s.BabyMatureSpeedMultiplier = DoubleValue(x, "BabyMatureSpeedMultiplier", s.BabyMatureSpeedMultiplier);
            s.AllowThirdPersonPlayer = BoolValue(x, "AllowThirdPersonPlayer", s.AllowThirdPersonPlayer);
            s.ServerCrosshair = BoolValue(x, "ServerCrosshair", s.ServerCrosshair);
            s.ShowMapPlayerLocation = BoolValue(x, "ShowMapPlayerLocation", s.ShowMapPlayerLocation);
            s.AllowFlyerCarryPVE = BoolValue(x, "AllowFlyerCarryPVE", s.AllowFlyerCarryPVE);
            s.DisableStructurePlacementCollision = BoolValue(x, "DisableStructurePlacementCollision", s.DisableStructurePlacementCollision);
            s.ResourceNoReplenishRadiusStructures = DoubleValue(x, "ResourceNoReplenishRadiusStructures", s.ResourceNoReplenishRadiusStructures);
            s.DisableCryopodCooldown = BoolValue(x, "DisableCryopodCooldown", s.DisableCryopodCooldown);
            s.ScheduledStartEnabled = BoolValue(x, "ScheduledStartEnabled", s.ScheduledStartEnabled);
            s.ScheduledStartAt = DateTimeValue(x, "ScheduledStartAt", s.ScheduledStartAt);
            s.ScheduledStopEnabled = BoolValue(x, "ScheduledStopEnabled", s.ScheduledStopEnabled);
            s.ScheduledStopAt = DateTimeValue(x, "ScheduledStopAt", s.ScheduledStopAt);
            s.DailyStartEnabled = BoolValue(x, "DailyStartEnabled", s.DailyStartEnabled);
            s.DailyStartTime = TimeSpanValue(x, "DailyStartTime", s.DailyStartTime);
            s.DailyStartLastRunAt = DateTimeValue(x, "DailyStartLastRunAt", s.DailyStartLastRunAt);
            s.DailyStopEnabled = BoolValue(x, "DailyStopEnabled", s.DailyStopEnabled);
            s.DailyStopTime = TimeSpanValue(x, "DailyStopTime", s.DailyStopTime);
            s.DailyStopLastRunAt = DateTimeValue(x, "DailyStopLastRunAt", s.DailyStopLastRunAt);
            s.RemotePort = IntValue(x, "RemotePort", s.RemotePort);
            s.RemotePin = Unprotect(Value(x, "RemotePin", ""));
            s.RemoteTailnetIp = Value(x, "RemoteTailnetIp", s.RemoteTailnetIp);
            return s;
        }

        public void Save()
        {
            string dir = Path.GetDirectoryName(SettingsPath);
            Directory.CreateDirectory(dir);
            XElement x = new XElement("ArkServerManager",
                new XElement("ServerRoot", ServerRoot),
                new XElement("MapName", MapName),
                new XElement("SessionName", SessionName),
                new XElement("MaxPlayers", MaxPlayers),
                new XElement("GamePort", GamePort),
                new XElement("QueryPort", QueryPort),
                new XElement("RconPort", RconPort),
                new XElement("ServerPassword", Protect(ServerPassword)),
                new XElement("AdminPassword", Protect(AdminPassword)),
                new XElement("AdditionalArguments", AdditionalArguments),
                new XElement("AutoRestart", AutoRestart),
                new XElement("RestartDelaySeconds", RestartDelaySeconds),
                new XElement("ServerPVE", ServerPVE),
                new XElement("UseSingleplayerSettings", UseSingleplayerSettings),
                new XElement("DifficultyOffset", DifficultyOffset.ToString(CultureInfo.InvariantCulture)),
                new XElement("OverrideOfficialDifficulty", OverrideOfficialDifficulty.ToString(CultureInfo.InvariantCulture)),
                new XElement("XPMultiplier", XPMultiplier.ToString(CultureInfo.InvariantCulture)),
                new XElement("TamingSpeedMultiplier", TamingSpeedMultiplier.ToString(CultureInfo.InvariantCulture)),
                new XElement("HarvestAmountMultiplier", HarvestAmountMultiplier.ToString(CultureInfo.InvariantCulture)),
                new XElement("ResourcesRespawnPeriodMultiplier", ResourcesRespawnPeriodMultiplier.ToString(CultureInfo.InvariantCulture)),
                new XElement("DinoCountMultiplier", DinoCountMultiplier.ToString(CultureInfo.InvariantCulture)),
                new XElement("DayCycleSpeedScale", DayCycleSpeedScale.ToString(CultureInfo.InvariantCulture)),
                new XElement("DayTimeSpeedScale", DayTimeSpeedScale.ToString(CultureInfo.InvariantCulture)),
                new XElement("NightTimeSpeedScale", NightTimeSpeedScale.ToString(CultureInfo.InvariantCulture)),
                new XElement("MatingIntervalMultiplier", MatingIntervalMultiplier.ToString(CultureInfo.InvariantCulture)),
                new XElement("EggHatchSpeedMultiplier", EggHatchSpeedMultiplier.ToString(CultureInfo.InvariantCulture)),
                new XElement("BabyMatureSpeedMultiplier", BabyMatureSpeedMultiplier.ToString(CultureInfo.InvariantCulture)),
                new XElement("AllowThirdPersonPlayer", AllowThirdPersonPlayer),
                new XElement("ServerCrosshair", ServerCrosshair),
                new XElement("ShowMapPlayerLocation", ShowMapPlayerLocation),
                new XElement("AllowFlyerCarryPVE", AllowFlyerCarryPVE),
                new XElement("DisableStructurePlacementCollision", DisableStructurePlacementCollision),
                new XElement("ResourceNoReplenishRadiusStructures", ResourceNoReplenishRadiusStructures.ToString(CultureInfo.InvariantCulture)),
                new XElement("DisableCryopodCooldown", DisableCryopodCooldown),
                new XElement("ScheduledStartEnabled", ScheduledStartEnabled),
                new XElement("ScheduledStartAt", ScheduledStartAt.ToString("o", CultureInfo.InvariantCulture)),
                new XElement("ScheduledStopEnabled", ScheduledStopEnabled),
                new XElement("ScheduledStopAt", ScheduledStopAt.ToString("o", CultureInfo.InvariantCulture)),
                new XElement("DailyStartEnabled", DailyStartEnabled),
                new XElement("DailyStartTime", DailyStartTime.ToString("c", CultureInfo.InvariantCulture)),
                new XElement("DailyStartLastRunAt", DailyStartLastRunAt.ToString("o", CultureInfo.InvariantCulture)),
                new XElement("DailyStopEnabled", DailyStopEnabled),
                new XElement("DailyStopTime", DailyStopTime.ToString("c", CultureInfo.InvariantCulture)),
                new XElement("DailyStopLastRunAt", DailyStopLastRunAt.ToString("o", CultureInfo.InvariantCulture)),
                new XElement("RemotePort", RemotePort),
                new XElement("RemotePin", Protect(RemotePin)),
                new XElement("RemoteTailnetIp", RemoteTailnetIp));
            x.Save(SettingsPath);
        }

        private void ImportExistingIni()
        {
            string ini = Path.Combine(ConfigDirectory, "GameUserSettings.ini");
            if (!File.Exists(ini)) return;
            foreach (string raw in File.ReadAllLines(ini))
            {
                int p = raw.IndexOf('=');
                if (p <= 0) continue;
                string key = raw.Substring(0, p).Trim();
                string value = raw.Substring(p + 1).Trim();
                int n;
                if (key.Equals("SessionName", StringComparison.OrdinalIgnoreCase)) SessionName = value;
                else if (key.Equals("MaxPlayers", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out n)) MaxPlayers = n;
                else if (key.Equals("RCONPort", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out n)) RconPort = n;
                else if (key.Equals("ServerPassword", StringComparison.OrdinalIgnoreCase)) ServerPassword = value;
                else if (key.Equals("ServerAdminPassword", StringComparison.OrdinalIgnoreCase)) AdminPassword = value;
            }

            ImportGameplayIni();

            string saves = Path.Combine(ServerRoot, @"ShooterGame\Saved\SavedArks");
            if (Directory.Exists(saves))
            {
                FileInfo newest = new DirectoryInfo(saves).GetFiles("*.ark")
                    .Where(delegate(FileInfo f) { return f.Name.IndexOf('_') < 0; })
                    .OrderByDescending(delegate(FileInfo f) { return f.LastWriteTimeUtc; }).FirstOrDefault();
                if (newest != null) MapName = Path.GetFileNameWithoutExtension(newest.Name);
            }
        }

        private void ImportGameplayIni()
        {
            string[] files = { Path.Combine(ConfigDirectory, "GameUserSettings.ini"), Path.Combine(ConfigDirectory, "Game.ini") };
            foreach (string file in files)
            {
                if (!File.Exists(file)) continue;
                foreach (string raw in File.ReadAllLines(file))
                {
                    int p = raw.IndexOf('=');
                    if (p <= 0) continue;
                    string key = raw.Substring(0, p).Trim();
                    string value = raw.Substring(p + 1).Trim();
                    bool b; double d;
                    if (key.Equals("ServerPVE", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(value, out b)) ServerPVE = b;
                    else if (key.Equals("bUseSingleplayerSettings", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(value, out b)) UseSingleplayerSettings = b;
                    else if (key.Equals("DifficultyOffset", StringComparison.OrdinalIgnoreCase) && TryDouble(value, out d)) DifficultyOffset = d;
                    else if (key.Equals("OverrideOfficialDifficulty", StringComparison.OrdinalIgnoreCase) && TryDouble(value, out d)) OverrideOfficialDifficulty = d;
                    else if (key.Equals("XPMultiplier", StringComparison.OrdinalIgnoreCase) && TryDouble(value, out d)) XPMultiplier = d;
                    else if (key.Equals("TamingSpeedMultiplier", StringComparison.OrdinalIgnoreCase) && TryDouble(value, out d)) TamingSpeedMultiplier = d;
                    else if (key.Equals("HarvestAmountMultiplier", StringComparison.OrdinalIgnoreCase) && TryDouble(value, out d)) HarvestAmountMultiplier = d;
                    else if (key.Equals("ResourcesRespawnPeriodMultiplier", StringComparison.OrdinalIgnoreCase) && TryDouble(value, out d)) ResourcesRespawnPeriodMultiplier = d;
                    else if (key.Equals("DinoCountMultiplier", StringComparison.OrdinalIgnoreCase) && TryDouble(value, out d)) DinoCountMultiplier = d;
                    else if (key.Equals("DayCycleSpeedScale", StringComparison.OrdinalIgnoreCase) && TryDouble(value, out d)) DayCycleSpeedScale = d;
                    else if (key.Equals("DayTimeSpeedScale", StringComparison.OrdinalIgnoreCase) && TryDouble(value, out d)) DayTimeSpeedScale = d;
                    else if (key.Equals("NightTimeSpeedScale", StringComparison.OrdinalIgnoreCase) && TryDouble(value, out d)) NightTimeSpeedScale = d;
                    else if (key.Equals("MatingIntervalMultiplier", StringComparison.OrdinalIgnoreCase) && TryDouble(value, out d)) MatingIntervalMultiplier = d;
                    else if (key.Equals("EggHatchSpeedMultiplier", StringComparison.OrdinalIgnoreCase) && TryDouble(value, out d)) EggHatchSpeedMultiplier = d;
                    else if (key.Equals("BabyMatureSpeedMultiplier", StringComparison.OrdinalIgnoreCase) && TryDouble(value, out d)) BabyMatureSpeedMultiplier = d;
                    else if (key.Equals("AllowThirdPersonPlayer", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(value, out b)) AllowThirdPersonPlayer = b;
                    else if (key.Equals("ServerCrosshair", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(value, out b)) ServerCrosshair = b;
                    else if (key.Equals("ShowMapPlayerLocation", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(value, out b)) ShowMapPlayerLocation = b;
                    else if (key.Equals("AllowFlyerCarryPvE", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(value, out b)) AllowFlyerCarryPVE = b;
                    else if (key.Equals("bDisableStructurePlacementCollision", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(value, out b)) DisableStructurePlacementCollision = b;
                    else if ((key.Equals("ResourceNoReplenishRadiusStructures", StringComparison.OrdinalIgnoreCase) || key.Equals("StructurePreventResourceRadiusMultiplier", StringComparison.OrdinalIgnoreCase)) && TryDouble(value, out d)) ResourceNoReplenishRadiusStructures = d;
                    else if (key.Equals("EnableCryopodNerf", StringComparison.OrdinalIgnoreCase) && Boolean.TryParse(value, out b)) DisableCryopodCooldown = b;
                }
            }
        }

        private static string Protect(string value)
        {
            if (String.IsNullOrEmpty(value)) return "";
            byte[] data = Encoding.UTF8.GetBytes(value);
            byte[] entropy = Encoding.UTF8.GetBytes("ARK-Server-Manager-v1");
            return Convert.ToBase64String(ProtectedData.Protect(data, entropy, DataProtectionScope.CurrentUser));
        }

        private static string Unprotect(string value)
        {
            if (String.IsNullOrEmpty(value)) return "";
            try
            {
                byte[] data = Convert.FromBase64String(value);
                byte[] entropy = Encoding.UTF8.GetBytes("ARK-Server-Manager-v1");
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(data, entropy, DataProtectionScope.CurrentUser));
            }
            catch { return ""; }
        }

        private static string Value(XElement x, string name, string fallback)
        {
            XElement e = x.Element(name);
            return e == null ? fallback : e.Value;
        }
        private static int IntValue(XElement x, string name, int fallback)
        {
            int n; return Int32.TryParse(Value(x, name, ""), out n) ? n : fallback;
        }
        private static bool BoolValue(XElement x, string name, bool fallback)
        {
            bool b; return Boolean.TryParse(Value(x, name, ""), out b) ? b : fallback;
        }
        private static double DoubleValue(XElement x, string name, double fallback)
        {
            double d; return TryDouble(Value(x, name, ""), out d) ? d : fallback;
        }
        private static DateTime DateTimeValue(XElement x, string name, DateTime fallback)
        {
            DateTime value;
            return DateTime.TryParse(Value(x, name, ""), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out value) ? value.ToLocalTime() : fallback;
        }
        private static TimeSpan TimeSpanValue(XElement x, string name, TimeSpan fallback)
        {
            TimeSpan value;
            return TimeSpan.TryParse(Value(x, name, ""), CultureInfo.InvariantCulture, out value) ? value : fallback;
        }
        private static bool TryDouble(string value, out double result)
        {
            return Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) ||
                   Double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result);
        }
    }

    internal sealed class MainForm : Form
    {
        internal static void RunMapPreview()
        {
            Application.Run(new DinoMapDialog(CreatePreviewMapPoints(), 0));
        }

        internal static void RenderMapPreview(string path)
        {
            using (DinoMapDialog dialog = new DinoMapDialog(CreatePreviewMapPoints(), 0))
            {
                dialog.Show();
                Application.DoEvents();
                using (Bitmap image = new Bitmap(dialog.Width, dialog.Height))
                {
                    dialog.DrawToBitmap(image, new Rectangle(Point.Empty, dialog.Size));
                    image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
                dialog.Close();
            }
        }

        internal static void RenderResourcePreview(string path)
        {
            List<ResourceZoneSnapshot> zones = CreateResourcePreviewSnapshots();
            for (int i = 0; i < zones.Count; i++)
            {
                zones[i].Scanned = true;
                zones[i].Metal = 6 + (i * 7) % 13;
                zones[i].Crystal = (i * 5) % 8;
                zones[i].Obsidian = (i * 3) % 5;
            }
            using (Form preview = new Form { Size = new Size(820, 720), BackColor = Color.FromArgb(11, 16, 22), FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000) })
            using (ResourceMapCanvas canvas = new ResourceMapCanvas(zones) { Dock = DockStyle.Fill })
            {
                preview.Controls.Add(canvas); preview.Show(); Application.DoEvents();
                using (Bitmap image = new Bitmap(preview.Width, preview.Height))
                {
                    preview.DrawToBitmap(image, new Rectangle(Point.Empty, preview.Size));
                    image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
                preview.Close();
            }
        }

        private static List<DinoMapPoint> CreatePreviewMapPoints()
        {
            return new List<DinoMapPoint>
            {
                new DinoMapPoint(150, "ミッドガルド（ヴァルディランド）", 85.26, 15.30, -9432.3),
                new DinoMapPoint(140, "アスガルド", 63.68, 50.22, -326807.5),
                new DinoMapPoint(125, "ミッドガルド（ヴァナランド）", 28.40, 74.80, 3150.0),
                new DinoMapPoint(95, "ヴァナヘイム", 48.60, 84.20, -282400.0),
                new DinoMapPoint(80, "ヨトゥンヘイム", 18.70, 32.50, -236100.0)
            };
        }

        private readonly Color Bg = Color.FromArgb(18, 24, 31);
        private readonly Color PanelColor = Color.FromArgb(28, 36, 46);
        private readonly Color PanelLight = Color.FromArgb(37, 47, 59);
        private readonly Color TextColor = Color.FromArgb(235, 240, 245);
        private readonly Color Muted = Color.FromArgb(155, 168, 182);
        private readonly Color Green = Color.FromArgb(55, 196, 125);
        private readonly Color Red = Color.FromArgb(239, 93, 102);
        private readonly Color Amber = Color.FromArgb(242, 176, 70);
        private readonly Color Blue = Color.FromArgb(61, 140, 235);

        private AppSettings settings;
        private Process serverProcess;
        private bool deliberateStop;
        private bool stopping;
        private bool observedRunning;
        private bool launchPending;
        private bool logReadQueued;
        private bool commandBusy;
        private bool dinoSearchBusy;
        private DateTime serverStartedAt;
        private DateTime? restartAt;
        private TimeSpan lastCpu;
        private DateTime lastCpuAt;
        private DateTime lastLogRead = DateTime.MinValue;
        private FileSystemWatcher logWatcher;
        private static readonly object ConsoleReadLock = new object();
        private readonly object dinoSnapshotCacheLock = new object();
        private readonly Dictionary<string, DinoSaveSnapshot> dinoSnapshotCache = new Dictionary<string, DinoSaveSnapshot>(StringComparer.OrdinalIgnoreCase);

        private Label statusDot;
        private Label statusTitle;
        private Label statusDetail;
        private Label uptimeValue;
        private Label cpuValue;
        private Label memoryValue;
        private Label portValue;
        private Button startButton;
        private Button stopButton;
        private CheckBox scheduledStartEnabledBox;
        private DateTimePicker scheduledStartPicker;
        private DateTimePicker scheduledStartTimePicker;
        private CheckBox scheduledStopEnabledBox;
        private DateTimePicker scheduledStopPicker;
        private DateTimePicker scheduledStopTimePicker;
        private CheckBox dailyStartEnabledBox;
        private DateTimePicker dailyStartPicker;
        private CheckBox dailyStopEnabledBox;
        private DateTimePicker dailyStopPicker;
        private Label scheduleStatusLabel;
        private string scheduleLastAction = "";
        private RichTextBox logBox;
        private TabControl tabs;
        private TextBox rootBox;
        private ComboBox mapBox;
        private TextBox sessionBox;
        private NumericUpDown maxPlayersBox;
        private NumericUpDown gamePortBox;
        private NumericUpDown queryPortBox;
        private NumericUpDown rconPortBox;
        private TextBox serverPasswordBox;
        private TextBox adminPasswordBox;
        private TextBox extraArgsBox;
        private CheckBox autoRestartBox;
        private NumericUpDown restartDelayBox;
        private Label saveNotice;
        private ComboBox serverModeBox;
        private CheckBox singleplayerSettingsBox;
        private NumericUpDown difficultyBox;
        private NumericUpDown overrideDifficultyBox;
        private NumericUpDown xpMultiplierBox;
        private NumericUpDown tamingMultiplierBox;
        private NumericUpDown harvestMultiplierBox;
        private NumericUpDown resourceRespawnBox;
        private NumericUpDown dinoCountBox;
        private NumericUpDown dayCycleBox;
        private NumericUpDown dayTimeBox;
        private NumericUpDown nightTimeBox;
        private NumericUpDown matingIntervalBox;
        private NumericUpDown eggHatchBox;
        private NumericUpDown babyMatureBox;
        private CheckBox thirdPersonBox;
        private CheckBox crosshairBox;
        private CheckBox mapLocationBox;
        private CheckBox flyerCarryBox;
        private CheckBox structureCollisionBox;
        private NumericUpDown structureResourceRadiusBox;
        private CheckBox cryopodCooldownBox;
        private Label gameplaySaveNotice;
        private Label liveControlStatus;
        private TextBox commandInputBox;
        private Button sendCommandButton;
        private RichTextBox commandOutputBox;
        private ComboBox dinoSearchBox;
        private ComboBox dinoCategoryBox;
        private Button searchDinoButton;
        private Button locateDinoButton;
        private Button showDinoMapButton;
        private Button openDinoMapButton;
        private Label dinoCountLabel;
        private RichTextBox dinoResultBox;
        private readonly List<DinoMapPoint> selectedDinoMapPoints = new List<DinoMapPoint>();
        private bool updatingDinoHighlight;
        private List<string> lastDinoActors = new List<string>();
        private List<string> lastDinoLocations = new List<string>();
        private string lastDinoClassName = "";
        private string lastDinoCategory = "";
        private Button refreshResourcesButton;
        private Label resourceStatusLabel;
        private ComboBox resourceTypeBox;
        private ListView resourceZoneList;
        private ResourceMapCanvas resourceMapCanvas;
        private bool resourceSearchBusy;
        private List<ResourceZoneSnapshot> resourceSnapshots = new List<ResourceZoneSnapshot>();
        private RemoteControlServer remoteServer;
        private Label remoteServiceStatus;
        private TextBox remotePinText;
        private TextBox remoteUrlText;
        private Button remoteSetupButton;
        private System.Windows.Forms.Timer timer;

        public MainForm()
        {
            settings = AppSettings.Load();
            EnsureRemotePin();
            Text = "ARK Server Manager";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 640);
            Size = new Size(1080, 760);
            BackColor = Bg;
            ForeColor = TextColor;
            Font = new Font("Yu Gothic UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            // Keep the dashboard geometry stable on Windows display scaling.
            // The process manifest already opts into DPI awareness.
            AutoScaleMode = AutoScaleMode.None;
            BuildUi();
            FillSettings();
            ConfigureLogWatcher();
            DiscoverProcess();
            StartRemoteControl();
            UpdateLiveControlAvailability(IsRunning() && !launchPending && !stopping);
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 1000;
            timer.Tick += delegate { RefreshMonitor(); };
            timer.Start();
            Shown += delegate { RefreshMonitor(); };
        }

        public void OpenSettings()
        {
            tabs.SelectedIndex = 1;
        }

        public void OpenGameSettings()
        {
            tabs.SelectedIndex = 2;
        }

        public void OpenServerControl()
        {
            tabs.SelectedIndex = 3;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (timer != null) { timer.Stop(); timer.Dispose(); }
            if (logWatcher != null) { logWatcher.EnableRaisingEvents = false; logWatcher.Dispose(); logWatcher = null; }
            if (remoteServer != null) { remoteServer.Dispose(); remoteServer = null; }
            base.OnFormClosed(e);
        }

        private void BuildUi()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Bg, Padding = new Padding(24, 14, 24, 8) };
            Label brand = new Label { Text = "ARK  SERVER MANAGER", AutoSize = true, Font = new Font("Yu Gothic UI", 18F, FontStyle.Bold), ForeColor = TextColor, Location = new Point(24, 13) };
            Label subtitle = new Label { Text = "Dedicated server control center", AutoSize = true, ForeColor = Muted, Location = new Point(27, 49) };
            header.Controls.Add(brand); header.Controls.Add(subtitle);
            Controls.Add(header);

            tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(18, 7), Font = new Font("Yu Gothic UI", 10F, FontStyle.Bold) };
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.ItemSize = new Size(138, 38);
            tabs.DrawItem += DrawTab;
            tabs.TabPages.Add(BuildDashboard());
            tabs.TabPages.Add(BuildSettingsPage());
            tabs.TabPages.Add(BuildGameplayPage());
            tabs.TabPages.Add(BuildServerControlPage());
            tabs.TabPages.Add(BuildResourcePage());
            tabs.TabPages.Add(BuildRemotePage());
            tabs.TabPages.Add(BuildLogPage());
            Controls.Add(tabs);
            tabs.BringToFront();
        }

        private void DrawTab(object sender, DrawItemEventArgs e)
        {
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (SolidBrush b = new SolidBrush(selected ? PanelLight : Bg)) e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, e.Bounds,
                selected ? TextColor : Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private TabPage NewPage(string title)
        {
            return new TabPage(title) { BackColor = Bg, ForeColor = TextColor, Padding = new Padding(18) };
        }

        private TabPage BuildDashboard()
        {
            TabPage page = NewPage("ダッシュボード");
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(5) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 148));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 124));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Panel status = Card();
            statusDot = new Label { Text = "●", AutoSize = true, Font = new Font("Segoe UI Symbol", 19F), ForeColor = Muted, Location = new Point(24, 23) };
            statusTitle = new Label { Text = "確認中", AutoSize = true, Font = new Font("Yu Gothic UI", 20F, FontStyle.Bold), Location = new Point(60, 18), ForeColor = TextColor };
            statusDetail = new Label { Text = settings.MapName, AutoSize = true, ForeColor = Muted, Location = new Point(64, 61) };
            startButton = ActionButton("サーバーを起動", Green, 170);
            startButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            startButton.Location = new Point(status.Width - 390, 43);
            startButton.Click += StartServer;
            stopButton = ActionButton("安全に停止", Red, 155);
            stopButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            stopButton.Location = new Point(status.Width - 200, 43);
            stopButton.Click += StopServer;
            status.Resize += delegate { startButton.Left = status.ClientSize.Width - 365; stopButton.Left = status.ClientSize.Width - 177; };
            status.Controls.Add(statusDot); status.Controls.Add(statusTitle); status.Controls.Add(statusDetail); status.Controls.Add(startButton); status.Controls.Add(stopButton);
            layout.Controls.Add(status, 0, 0);

            TableLayoutPanel stats = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Margin = new Padding(0, 12, 0, 12) };
            for (int i = 0; i < 4; i++) stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            uptimeValue = AddStat(stats, 0, "稼働時間", "—");
            cpuValue = AddStat(stats, 1, "CPU", "—");
            memoryValue = AddStat(stats, 2, "メモリ使用量", "—");
            portValue = AddStat(stats, 3, "ゲームポート", "—");
            layout.Controls.Add(stats, 0, 1);

            Panel info = Card();
            Label heading = new Label { Text = "クイック操作", AutoSize = true, Font = new Font("Yu Gothic UI", 12F, FontStyle.Bold), Location = new Point(22, 18) };
            Button configButton = GhostButton("詳細設定ファイルを開く", 210);
            configButton.Location = new Point(22, 59);
            configButton.Click += delegate { OpenPath(settings.ConfigDirectory); };
            Button logsButton = GhostButton("ログ保存先を開く", 180);
            logsButton.Location = new Point(246, 59);
            logsButton.Click += delegate { OpenPath(Path.GetDirectoryName(settings.LogPath)); };
            Button editButton = GhostButton("起動設定を変更", 170);
            editButton.Location = new Point(440, 59);
            editButton.Click += delegate { tabs.SelectedIndex = 1; };
            Label hint = new Label { Text = "停止時は SaveWorld → DoExit の順で送信します。", AutoSize = true, ForeColor = Muted, Location = new Point(24, 112) };
            Label scheduleHeading = new Label { Text = "日時指定（単発／毎日・管理アプリ起動中に実行）", AutoSize = true, Font = new Font("Yu Gothic UI", 11F, FontStyle.Bold), Location = new Point(22, 154) };
            scheduledStartEnabledBox = new CheckBox { Text = "起動予約", AutoSize = true, Location = new Point(24, 190), ForeColor = TextColor };
            scheduledStartPicker = new DateTimePicker { Location = new Point(110, 184), Width = 122, Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy/MM/dd", Font = new Font("Yu Gothic UI", 9.5F) };
            scheduledStartTimePicker = new DateTimePicker { Location = new Point(238, 184), Width = 74, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Font = new Font("Yu Gothic UI", 9.5F) };
            scheduledStopEnabledBox = new CheckBox { Text = "停止予約", AutoSize = true, Location = new Point(24, 228), ForeColor = TextColor };
            scheduledStopPicker = new DateTimePicker { Location = new Point(110, 222), Width = 122, Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy/MM/dd", Font = new Font("Yu Gothic UI", 9.5F) };
            scheduledStopTimePicker = new DateTimePicker { Location = new Point(238, 222), Width = 74, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Font = new Font("Yu Gothic UI", 9.5F) };
            dailyStartEnabledBox = new CheckBox { Text = "毎日起動", AutoSize = true, Location = new Point(336, 190), ForeColor = TextColor };
            dailyStartPicker = new DateTimePicker { Location = new Point(430, 184), Width = 120, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Font = new Font("Yu Gothic UI", 9.5F) };
            dailyStopEnabledBox = new CheckBox { Text = "毎日停止", AutoSize = true, Location = new Point(336, 228), ForeColor = TextColor };
            dailyStopPicker = new DateTimePicker { Location = new Point(430, 222), Width = 120, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Font = new Font("Yu Gothic UI", 9.5F) };
            Button saveScheduleButton = ActionButton("予約を保存", Blue, 130); saveScheduleButton.Location = new Point(580, 184); saveScheduleButton.Height = 32; saveScheduleButton.Click += SaveSchedule;
            Button clearScheduleButton = GhostButton("すべて解除", 130); clearScheduleButton.Location = new Point(580, 222); clearScheduleButton.Height = 32; clearScheduleButton.Click += ClearSchedule;
            scheduleStatusLabel = new Label { Text = "予約なし", AutoSize = true, ForeColor = Muted, Location = new Point(24, 268), MaximumSize = new Size(780, 42) };
            info.Controls.Add(heading); info.Controls.Add(configButton); info.Controls.Add(logsButton); info.Controls.Add(editButton); info.Controls.Add(hint);
            info.Controls.Add(scheduleHeading); info.Controls.Add(scheduledStartEnabledBox); info.Controls.Add(scheduledStartPicker); info.Controls.Add(scheduledStartTimePicker);
            info.Controls.Add(scheduledStopEnabledBox); info.Controls.Add(scheduledStopPicker); info.Controls.Add(scheduledStopTimePicker); info.Controls.Add(dailyStartEnabledBox); info.Controls.Add(dailyStartPicker);
            info.Controls.Add(dailyStopEnabledBox); info.Controls.Add(dailyStopPicker); info.Controls.Add(saveScheduleButton); info.Controls.Add(clearScheduleButton); info.Controls.Add(scheduleStatusLabel);
            layout.Controls.Add(info, 0, 2);
            page.Controls.Add(layout);
            return page;
        }

        private TabPage BuildSettingsPage()
        {
            TabPage page = NewPage("起動設定");
            Panel card = Card();
            card.AutoScroll = true;
            card.AutoScrollMinSize = new Size(1140, 620);
            page.Controls.Add(card);
            Label title = new Label { Text = "サーバー起動設定", AutoSize = true, Font = new Font("Yu Gothic UI", 16F, FontStyle.Bold), Location = new Point(26, 20) };
            Label description = new Label { Text = "次回の起動から反映されます。建造物関連の2項目だけは Game.ini に安全に反映します。", AutoSize = true, ForeColor = Muted, Location = new Point(28, 55) };
            card.Controls.Add(title); card.Controls.Add(description);

            TableLayoutPanel form = new TableLayoutPanel { Location = new Point(26, 90), Width = 1060, Height = 390, Anchor = AnchorStyles.Top | AnchorStyles.Left, ColumnCount = 4, RowCount = 7 };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 400));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            for (int i = 0; i < 7; i++) form.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            rootBox = TextInput();
            TableLayoutPanel rootPanel = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0), ColumnCount = 2, RowCount = 1 };
            rootPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); rootPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
            Button browse = GhostButton("参照", 70); browse.Dock = DockStyle.Fill; browse.Margin = new Padding(0, 5, 14, 5); browse.Click += BrowseRoot;
            rootPanel.Controls.Add(rootBox, 0, 0); rootPanel.Controls.Add(browse, 1, 0);
            AddField(form, 0, 0, "サーバー保存先", rootPanel, 1, 1);
            mapBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown, BackColor = PanelLight, ForeColor = TextColor, FlatStyle = FlatStyle.Flat };
            mapBox.Items.AddRange(new object[] { "Fjordur", "TheIsland", "TheCenter", "Ragnarok", "ScorchedEarth_P", "Aberration_P", "Extinction", "Valguero_P", "CrystalIsles", "LostIsland" });
            sessionBox = TextInput();
            AddField(form, 0, 1, "マップ", mapBox, 1, 1); AddField(form, 2, 1, "サーバー名", sessionBox, 3, 1);
            maxPlayersBox = NumberInput(1, 250); gamePortBox = NumberInput(1, 65535);
            AddField(form, 0, 2, "最大人数", maxPlayersBox, 1, 1); AddField(form, 2, 2, "ゲームポート", gamePortBox, 3, 1);
            queryPortBox = NumberInput(1, 65535); rconPortBox = NumberInput(1, 65535);
            AddField(form, 0, 3, "Queryポート", queryPortBox, 1, 1); AddField(form, 2, 3, "RCONポート", rconPortBox, 3, 1);
            serverPasswordBox = TextInput(); serverPasswordBox.UseSystemPasswordChar = true;
            adminPasswordBox = TextInput(); adminPasswordBox.UseSystemPasswordChar = true;
            AddField(form, 0, 4, "参加パスワード", serverPasswordBox, 1, 1); AddField(form, 2, 4, "管理パスワード", adminPasswordBox, 3, 1);
            extraArgsBox = TextInput(); AddField(form, 0, 5, "追加オプション", extraArgsBox, 1, 1);
            autoRestartBox = new CheckBox { Text = "異常終了時に自動で再起動", AutoSize = true, ForeColor = TextColor, Dock = DockStyle.Fill };
            restartDelayBox = NumberInput(5, 600);
            AddField(form, 0, 6, "自動復旧", autoRestartBox, 1, 1); AddField(form, 2, 6, "再起動まで（秒）", restartDelayBox, 3, 1);
            card.Controls.Add(form);

            Button save = ActionButton("設定を保存", Blue, 160); save.Location = new Point(28, 505); save.Click += SaveSettings;
            saveNotice = new Label { Text = "", AutoSize = true, ForeColor = Green, Location = new Point(205, 517) };
            card.Controls.Add(save); card.Controls.Add(saveNotice);
            return page;
        }

        private TabPage BuildLogPage()
        {
            TabPage page = NewPage("サーバーログ");
            Panel top = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = Bg };
            Label label = new Label { Text = "ShooterGame.log", AutoSize = true, Font = new Font("Yu Gothic UI", 12F, FontStyle.Bold), Location = new Point(4, 14) };
            Button refresh = GhostButton("再読込", 100); refresh.Anchor = AnchorStyles.Top | AnchorStyles.Right; refresh.Location = new Point(800, 7); refresh.Click += delegate { ReadLog(true); };
            top.Resize += delegate { refresh.Left = top.ClientSize.Width - 108; };
            top.Controls.Add(label); top.Controls.Add(refresh);
            logBox = new RichTextBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(11, 16, 22), ForeColor = Color.FromArgb(205, 215, 224), BorderStyle = BorderStyle.None, Font = new Font("Consolas", 9.5F), ReadOnly = true, WordWrap = false };
            page.Controls.Add(logBox); page.Controls.Add(top);
            return page;
        }

        private TabPage BuildServerControlPage()
        {
            TabPage page = NewPage("サーバー操作");
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(5) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 245));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Panel state = new Panel { Dock = DockStyle.Fill, BackColor = Bg };
            liveControlStatus = new Label { Text = "● サーバー停止中 — 操作できません", AutoSize = true, ForeColor = Muted, Font = new Font("Yu Gothic UI", 10F, FontStyle.Bold), Location = new Point(8, 13) };
            state.Controls.Add(liveControlStatus);
            layout.Controls.Add(state, 0, 0);

            Panel commandCard = Card(); commandCard.Margin = new Padding(0, 0, 0, 12);
            Label commandTitle = new Label { Text = "ゲームへのコマンド入力", AutoSize = true, Font = new Font("Yu Gothic UI", 13F, FontStyle.Bold), Location = new Point(20, 16) };
            Label commandHint = new Label { Text = "RCONコマンドを入力すると、実行結果を下に表示します。例: ListPlayers / Broadcast メッセージ / SaveWorld", AutoSize = true, ForeColor = Muted, Location = new Point(22, 47) };
            commandInputBox = TextInput(); commandInputBox.Dock = DockStyle.None; commandInputBox.Location = new Point(22, 76); commandInputBox.Width = 720; commandInputBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            commandInputBox.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter && sendCommandButton.Enabled) { e.SuppressKeyPress = true; SendServerCommand(sender, EventArgs.Empty); } };
            sendCommandButton = ActionButton("送信", Blue, 120); sendCommandButton.Height = 34; sendCommandButton.Location = new Point(770, 72); sendCommandButton.Anchor = AnchorStyles.Top | AnchorStyles.Right; sendCommandButton.Click += SendServerCommand;
            commandOutputBox = new RichTextBox { Location = new Point(22, 120), Width = 850, Height = 92, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, BackColor = Color.FromArgb(11, 16, 22), ForeColor = Color.FromArgb(205, 215, 224), BorderStyle = BorderStyle.None, Font = new Font("Consolas", 9.5F), ReadOnly = true };
            commandCard.Resize += delegate { commandInputBox.Width = Math.Max(220, commandCard.ClientSize.Width - 190); sendCommandButton.Left = commandCard.ClientSize.Width - 142; commandOutputBox.Width = commandCard.ClientSize.Width - 44; };
            commandCard.Controls.Add(commandTitle); commandCard.Controls.Add(commandHint); commandCard.Controls.Add(commandInputBox); commandCard.Controls.Add(sendCommandButton); commandCard.Controls.Add(commandOutputBox);
            layout.Controls.Add(commandCard, 0, 1);

            Panel dinoCard = Card();
            Label dinoTitle = new Label { Text = "特定恐竜の個体検索", AutoSize = true, Font = new Font("Yu Gothic UI", 13F, FontStyle.Bold), Location = new Point(20, 16) };
            Label dinoHint = new Label { Text = "①数を検索　②場所を検索　③座標行を選び最大5体を追加　④選択中をマップ表示。［死］=野生個体が直近5セーブで不動。", AutoSize = true, ForeColor = Muted, Location = new Point(22, 47) };
            dinoSearchBox = new ComboBox { Location = new Point(22, 77), Width = 390, Height = 32, DropDownStyle = ComboBoxStyle.DropDown, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems, BackColor = PanelLight, ForeColor = TextColor, FlatStyle = FlatStyle.Flat, Font = new Font("Yu Gothic UI", 10F), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            PopulateDinoOptions();
            dinoCategoryBox = new ComboBox { Location = new Point(430, 77), Width = 190, Height = 32, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = PanelLight, ForeColor = TextColor, FlatStyle = FlatStyle.Flat, Font = new Font("Yu Gothic UI", 10F), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            dinoCategoryBox.Items.AddRange(new object[] { "すべて", "野生のみ", "テイム済みのみ" }); dinoCategoryBox.SelectedIndex = 0;
            dinoCategoryBox.SelectedIndexChanged += delegate { ClearDinoSearchCache(); };
            searchDinoButton = ActionButton("数を検索", Green, 120); searchDinoButton.Height = 34; searchDinoButton.Location = new Point(650, 72); searchDinoButton.Anchor = AnchorStyles.Top | AnchorStyles.Right; searchDinoButton.Click += SearchDinos;
            dinoCountLabel = new Label { Text = "個体数: —", AutoSize = true, Font = new Font("Yu Gothic UI", 12F, FontStyle.Bold), ForeColor = TextColor, Location = new Point(22, 118) };
            locateDinoButton = ActionButton("場所を検索", Blue, 130); locateDinoButton.Height = 32; locateDinoButton.Location = new Point(740, 112); locateDinoButton.Anchor = AnchorStyles.Top | AnchorStyles.Right; locateDinoButton.Enabled = false; locateDinoButton.Click += LocateDinos;
            openDinoMapButton = ActionButton("選択中を表示 (0/5)", Color.FromArgb(108, 92, 231), 188); openDinoMapButton.Height = 32; openDinoMapButton.Location = new Point(542, 112); openDinoMapButton.Anchor = AnchorStyles.Top | AnchorStyles.Right; openDinoMapButton.Enabled = false; openDinoMapButton.Click += OpenSelectedDinosOnMap;
            showDinoMapButton = ActionButton("マップへ追加 (0/5)", Color.FromArgb(67, 128, 184), 188); showDinoMapButton.Height = 32; showDinoMapButton.Location = new Point(344, 112); showDinoMapButton.Anchor = AnchorStyles.Top | AnchorStyles.Right; showDinoMapButton.Enabled = false; showDinoMapButton.Click += ShowSelectedDinoOnMap;
            dinoResultBox = new RichTextBox { Location = new Point(22, 151), Width = 850, Height = 150, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, BackColor = Color.FromArgb(11, 16, 22), ForeColor = Color.FromArgb(205, 215, 224), BorderStyle = BorderStyle.None, Font = new Font("Consolas", 9.2F), ReadOnly = true, WordWrap = false };
            dinoResultBox.SelectionChanged += delegate { if (!updatingDinoHighlight) UpdateMapButtonAvailability(); };
            dinoResultBox.DoubleClick += delegate { if (showDinoMapButton.Enabled) ShowSelectedDinoOnMap(dinoResultBox, EventArgs.Empty); };
            dinoCard.Resize += delegate { searchDinoButton.Left = dinoCard.ClientSize.Width - 142; dinoCategoryBox.Left = searchDinoButton.Left - 208; dinoSearchBox.Width = Math.Max(220, dinoCategoryBox.Left - 40); locateDinoButton.Left = dinoCard.ClientSize.Width - 152; openDinoMapButton.Left = locateDinoButton.Left - 198; showDinoMapButton.Left = openDinoMapButton.Left - 198; dinoResultBox.Width = dinoCard.ClientSize.Width - 44; dinoResultBox.Height = Math.Max(70, dinoCard.ClientSize.Height - 173); };
            dinoCard.Controls.Add(dinoTitle); dinoCard.Controls.Add(dinoHint); dinoCard.Controls.Add(dinoSearchBox); dinoCard.Controls.Add(dinoCategoryBox); dinoCard.Controls.Add(searchDinoButton); dinoCard.Controls.Add(dinoCountLabel); dinoCard.Controls.Add(showDinoMapButton); dinoCard.Controls.Add(openDinoMapButton); dinoCard.Controls.Add(locateDinoButton); dinoCard.Controls.Add(dinoResultBox);
            layout.Controls.Add(dinoCard, 0, 2);

            page.Controls.Add(layout);
            return page;
        }

        private TabPage BuildResourcePage()
        {
            TabPage page = NewPage("資源マップ");
            Panel top = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Bg };
            Label title = new Label { Text = "Fjordur 小型資源スポットの現存量", AutoSize = true, Font = new Font("Yu Gothic UI", 15F, FontStyle.Bold), Location = new Point(5, 5) };
            Label hint = new Label { Text = "現在量を取得した時だけ走査し、豊富エリア内の資源をアルゲンタヴィスで約3秒未満の小区画に分けます。", AutoSize = true, ForeColor = Muted, Location = new Point(7, 38) };
            resourceTypeBox = new ComboBox
            {
                Width = 135, Height = 32, Location = new Point(660, 8), Anchor = AnchorStyles.Top | AnchorStyles.Right,
                DropDownStyle = ComboBoxStyle.DropDownList, BackColor = PanelLight, ForeColor = TextColor,
                FlatStyle = FlatStyle.Flat, Font = new Font("Yu Gothic UI", 10F)
            };
            resourceTypeBox.Items.AddRange(new object[] { "すべて", "金属", "水晶", "黒曜石" });
            resourceTypeBox.SelectedIndex = 0;
            refreshResourcesButton = ActionButton("現在量を取得", Blue, 145);
            refreshResourcesButton.Height = 32; refreshResourcesButton.Location = new Point(805, 7); refreshResourcesButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            refreshResourcesButton.Enabled = false; refreshResourcesButton.Click += RefreshResources;
            resourceStatusLabel = new Label { Text = "サーバー稼働後に現在量を取得できます。", AutoSize = true, ForeColor = Muted, Location = new Point(7, 59) };
            top.Resize += delegate { refreshResourcesButton.Left = top.ClientSize.Width - 150; resourceTypeBox.Left = refreshResourcesButton.Left - 145; };
            top.Controls.Add(title); top.Controls.Add(hint); top.Controls.Add(resourceTypeBox); top.Controls.Add(refreshResourcesButton); top.Controls.Add(resourceStatusLabel);

            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill, Orientation = Orientation.Vertical, Size = new Size(1000, 520), SplitterDistance = 470,
                BackColor = Bg, Panel1MinSize = 340, Panel2MinSize = 400
            };
            resourceZoneList = new ListView
            {
                Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false,
                GridLines = true, MultiSelect = false, BackColor = Color.FromArgb(11, 16, 22), ForeColor = TextColor,
                BorderStyle = BorderStyle.None, Font = new Font("Yu Gothic UI", 9.2F)
            };
            resourceZoneList.Columns.Add("#", 34); resourceZoneList.Columns.Add("小型スポット", 190);
            resourceZoneList.Columns.Add("金属岩", 72); resourceZoneList.Columns.Add("水晶岩", 72); resourceZoneList.Columns.Add("黒曜石岩", 82);
            resourceZoneList.SelectedIndexChanged += delegate
            {
                if (resourceMapCanvas != null && resourceZoneList.SelectedItems.Count > 0)
                    resourceMapCanvas.SelectedZoneIndex = (int)resourceZoneList.SelectedItems[0].Tag;
            };
            resourceMapCanvas = new ResourceMapCanvas(resourceSnapshots) { Dock = DockStyle.Fill };
            resourceMapCanvas.SelectedZoneChanged += delegate
            {
                foreach (ListViewItem item in resourceZoneList.Items)
                {
                    bool selected = (int)item.Tag == resourceMapCanvas.SelectedZoneIndex;
                    item.Selected = selected;
                    if (selected) item.EnsureVisible();
                }
            };
            resourceTypeBox.SelectedIndexChanged += delegate
            {
                resourceMapCanvas.ResourceFilter = resourceTypeBox.SelectedIndex;
                RefreshResourceZoneList();
            };
            split.Panel1.Padding = new Padding(0, 0, 10, 0); split.Panel1.Controls.Add(resourceZoneList);
            split.Panel2.Padding = new Padding(10, 0, 0, 0); split.Panel2.Controls.Add(resourceMapCanvas);
            resourceSnapshots = CreateResourceSnapshots();
            resourceMapCanvas.SetSnapshots(resourceSnapshots);
            RefreshResourceZoneList();
            if (resourceZoneList.Items.Count > 0) resourceZoneList.Items[0].Selected = true;

            page.Controls.Add(split); page.Controls.Add(top);
            return page;
        }

        private TabPage BuildRemotePage()
        {
            TabPage page = NewPage("スマホ操作");
            Panel card = Card();
            page.Controls.Add(card);

            Label title = new Label { Text = "スマホから操作", AutoSize = true, Font = new Font("Yu Gothic UI", 16F, FontStyle.Bold), Location = new Point(26, 22) };
            Label description = new Label
            {
                Text = "このPCのTailscale IPだけで待ち受け、同じtailnetの端末から直接接続します。通常のLANやインターネットには公開しません。",
                AutoSize = true, ForeColor = Muted, Location = new Point(28, 60)
            };

            Label serviceTitle = new Label { Text = "ローカルWeb画面", AutoSize = true, ForeColor = Muted, Location = new Point(28, 112) };
            remoteServiceStatus = new Label { Text = "準備中…", AutoSize = true, Font = new Font("Yu Gothic UI", 11F, FontStyle.Bold), ForeColor = Amber, Location = new Point(28, 137) };

            Label pinTitle = new Label { Text = "スマホ操作用PIN", AutoSize = true, ForeColor = Muted, Location = new Point(28, 190) };
            remotePinText = new TextBox { Text = settings.RemotePin, ReadOnly = true, Location = new Point(28, 216), Width = 180, Font = new Font("Consolas", 17F, FontStyle.Bold), TextAlign = HorizontalAlignment.Center, BackColor = PanelLight, ForeColor = TextColor, BorderStyle = BorderStyle.FixedSingle };
            Button copyPin = GhostButton("PINをコピー", 140); copyPin.Location = new Point(225, 214);
            copyPin.Click += delegate { try { Clipboard.SetText(settings.RemotePin); } catch { } };

            Label urlTitle = new Label { Text = "iPhoneで開くURL", AutoSize = true, ForeColor = Muted, Location = new Point(28, 278) };
            string cachedRemoteUrl = ReadCachedRemoteUrl();
            remoteUrlText = new TextBox { Text = cachedRemoteUrl.Length > 0 ? cachedRemoteUrl : "下のボタンでTailscale接続を設定すると表示されます", ReadOnly = true, Location = new Point(28, 304), Width = 650, BackColor = PanelLight, ForeColor = TextColor, BorderStyle = BorderStyle.FixedSingle };
            remoteUrlText.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Button copyUrl = GhostButton("URLをコピー", 140); copyUrl.Location = new Point(696, 301); copyUrl.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            copyUrl.Click += delegate { if (remoteUrlText.Text.StartsWith("http", StringComparison.OrdinalIgnoreCase)) try { Clipboard.SetText(remoteUrlText.Text); } catch { } };

            remoteSetupButton = ActionButton("スマホ接続を有効化", Blue, 260); remoteSetupButton.Location = new Point(28, 362); remoteSetupButton.Click += ConfigureTailscaleServe;
            Label setupHint = new Label
            {
                Text = "初回だけWindowsファイアウォールの許可が必要です。確認画面では「はい」を押してください。\n接続中はPC側でこの管理アプリを起動したままにしてください。",
                AutoSize = true, ForeColor = Amber, Location = new Point(28, 426)
            };
            Label feature = new Label
            {
                Text = "スマホ画面: 起動／安全停止、稼働状況、ログ、RCONコマンド、恐竜の数・レベル・場所検索\n安全対策: Tailscale IPだけで待受、6桁PIN、12時間セッション、連続失敗時ロック",
                AutoSize = true, ForeColor = Muted, Location = new Point(28, 500)
            };

            card.Resize += delegate { remoteUrlText.Width = Math.Max(300, card.ClientSize.Width - 235); copyUrl.Left = card.ClientSize.Width - 170; };
            card.Controls.Add(title); card.Controls.Add(description); card.Controls.Add(serviceTitle); card.Controls.Add(remoteServiceStatus);
            card.Controls.Add(pinTitle); card.Controls.Add(remotePinText); card.Controls.Add(copyPin); card.Controls.Add(urlTitle); card.Controls.Add(remoteUrlText); card.Controls.Add(copyUrl);
            card.Controls.Add(remoteSetupButton); card.Controls.Add(setupHint); card.Controls.Add(feature);
            return page;
        }

        private TabPage BuildGameplayPage()
        {
            TabPage page = NewPage("ゲーム設定");
            Panel card = Card();
            card.AutoScroll = true;
            card.AutoScrollMinSize = new Size(1140, 800);
            page.Controls.Add(card);

            Label title = new Label { Text = "ARK ゲーム設定", AutoSize = true, Font = new Font("Yu Gothic UI", 16F, FontStyle.Bold), Location = new Point(26, 20) };
            Label description = new Label { Text = "非専用セッションでよく使う基本設定です。次回のサーバー起動時に反映されます。", AutoSize = true, ForeColor = Muted, Location = new Point(28, 55) };
            card.Controls.Add(title); card.Controls.Add(description);

            TableLayoutPanel form = new TableLayoutPanel { Location = new Point(26, 88), Width = 1060, Height = 570, Anchor = AnchorStyles.Top | AnchorStyles.Left, ColumnCount = 4, RowCount = 11 };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 400));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            for (int i = 0; i < 11; i++) form.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

            serverModeBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = PanelLight, ForeColor = TextColor, FlatStyle = FlatStyle.Flat, Margin = new Padding(4, 8, 14, 8) };
            serverModeBox.Items.AddRange(new object[] { "PvP", "PvE" });
            singleplayerSettingsBox = OptionCheck("追加補正を有効化");
            AddField(form, 0, 0, "ゲームモード", serverModeBox, 1, 1); AddField(form, 2, 0, "シングルプレイヤー", singleplayerSettingsBox, 3, 1);

            difficultyBox = DecimalInput(0.01m, 10m, 0.1m, 2); overrideDifficultyBox = DecimalInput(0m, 50m, 0.5m, 2);
            AddField(form, 0, 1, "難易度レベル", difficultyBox, 1, 1); AddField(form, 2, 1, "難易度上書き", overrideDifficultyBox, 3, 1);
            xpMultiplierBox = DecimalInput(0.01m, 100m, 0.1m, 2); tamingMultiplierBox = DecimalInput(0.01m, 100m, 0.1m, 2);
            AddField(form, 0, 2, "経験値倍率", xpMultiplierBox, 1, 1); AddField(form, 2, 2, "テイム速度倍率", tamingMultiplierBox, 3, 1);
            harvestMultiplierBox = DecimalInput(0.01m, 100m, 0.1m, 2); resourceRespawnBox = DecimalInput(0.01m, 100m, 0.1m, 2);
            AddField(form, 0, 3, "採取量倍率", harvestMultiplierBox, 1, 1); AddField(form, 2, 3, "資源再生成間隔", resourceRespawnBox, 3, 1);
            dinoCountBox = DecimalInput(0.01m, 10m, 0.1m, 2); dayCycleBox = DecimalInput(0.01m, 100m, 0.1m, 2);
            AddField(form, 0, 4, "野生恐竜の数", dinoCountBox, 1, 1); AddField(form, 2, 4, "昼夜周期速度", dayCycleBox, 3, 1);
            dayTimeBox = DecimalInput(0.01m, 100m, 0.1m, 2); nightTimeBox = DecimalInput(0.01m, 100m, 0.1m, 2);
            AddField(form, 0, 5, "昼の経過速度", dayTimeBox, 1, 1); AddField(form, 2, 5, "夜の経過速度", nightTimeBox, 3, 1);
            matingIntervalBox = DecimalInput(0.001m, 100m, 0.01m, 3); eggHatchBox = DecimalInput(0.01m, 1000m, 0.5m, 2);
            AddField(form, 0, 6, "交配間隔倍率", matingIntervalBox, 1, 1); AddField(form, 2, 6, "孵化速度倍率", eggHatchBox, 3, 1);
            babyMatureBox = DecimalInput(0.01m, 1000m, 0.5m, 2); structureResourceRadiusBox = DecimalInput(0.01m, 100m, 0.1m, 2);
            AddField(form, 0, 7, "成長速度倍率", babyMatureBox, 1, 1); AddField(form, 2, 7, "建築物周辺の資源再生成範囲", structureResourceRadiusBox, 3, 1);

            structureCollisionBox = OptionCheck("地形への重なりを許可");
            AddField(form, 0, 8, "建造物の設置コリジョン", structureCollisionBox, 1, 1);
            cryopodCooldownBox = OptionCheck("両方を無効化（PvPにも適用）");
            AddField(form, 2, 8, "低温クールダウン／低体温症", cryopodCooldownBox, 3, 1);

            crosshairBox = OptionCheck("クロスヘアを表示"); mapLocationBox = OptionCheck("現在位置を表示");
            AddField(form, 0, 9, "画面表示", crosshairBox, 1, 1); AddField(form, 2, 9, "マップ表示", mapLocationBox, 3, 1);
            thirdPersonBox = OptionCheck("三人称視点を許可"); flyerCarryBox = OptionCheck("PvEで運搬を許可");
            AddField(form, 0, 10, "カメラ", thirdPersonBox, 1, 1); AddField(form, 2, 10, "飛行生物", flyerCarryBox, 3, 1);
            card.Controls.Add(form);

            Button save = ActionButton("ゲーム設定を保存", Blue, 190); save.Location = new Point(28, 675); save.Click += SaveSettings;
            gameplaySaveNotice = new Label { Text = "", AutoSize = true, ForeColor = Green, Location = new Point(235, 687) };
            Label note = new Label { Text = "※シングルプレイヤー設定は繁殖・テイム等に追加補正がかかります。資源再生成範囲は小さいほど建築物の近くに資源が戻ります。", AutoSize = true, ForeColor = Amber, Location = new Point(28, 740) };
            card.Controls.Add(save); card.Controls.Add(gameplaySaveNotice); card.Controls.Add(note);
            return page;
        }

        private Panel Card()
        {
            return new Panel { Dock = DockStyle.Fill, BackColor = PanelColor, Margin = new Padding(0, 0, 0, 0), Padding = new Padding(12) };
        }

        private Label AddStat(TableLayoutPanel owner, int column, string title, string value)
        {
            Panel p = Card(); p.Margin = new Padding(column == 0 ? 0 : 6, 0, column == 3 ? 0 : 6, 0);
            Label t = new Label { Text = title, AutoSize = true, ForeColor = Muted, Location = new Point(17, 16) };
            Label v = new Label { Text = value, AutoSize = true, Font = new Font("Yu Gothic UI", 17F, FontStyle.Bold), ForeColor = TextColor, Location = new Point(15, 46) };
            p.Controls.Add(t); p.Controls.Add(v); owner.Controls.Add(p, column, 0); return v;
        }

        private Button ActionButton(string text, Color color, int width)
        {
            Button b = new Button { Text = text, Width = width, Height = 48, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Yu Gothic UI", 10F, FontStyle.Bold) };
            b.FlatAppearance.BorderSize = 0; return b;
        }

        private Button GhostButton(string text, int width)
        {
            Button b = new Button { Text = text, Width = width, Height = 38, BackColor = PanelLight, ForeColor = TextColor, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            b.FlatAppearance.BorderColor = Color.FromArgb(65, 78, 92); return b;
        }

        private TextBox TextInput()
        {
            return new TextBox { Dock = DockStyle.Fill, BackColor = PanelLight, ForeColor = TextColor, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Yu Gothic UI", 10F), Margin = new Padding(4, 8, 14, 8) };
        }

        private NumericUpDown NumberInput(int min, int max)
        {
            return new NumericUpDown { Dock = DockStyle.Fill, Minimum = min, Maximum = max, BackColor = PanelLight, ForeColor = TextColor, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(4, 8, 14, 8) };
        }

        private NumericUpDown DecimalInput(decimal min, decimal max, decimal increment, int decimalPlaces)
        {
            return new NumericUpDown { Dock = DockStyle.Fill, Minimum = min, Maximum = max, Increment = increment, DecimalPlaces = decimalPlaces, BackColor = PanelLight, ForeColor = TextColor, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(4, 8, 14, 8) };
        }

        private CheckBox OptionCheck(string text)
        {
            return new CheckBox { Text = text, AutoSize = true, ForeColor = TextColor, Dock = DockStyle.Fill, Margin = new Padding(4, 8, 14, 8) };
        }

        private void PopulateDinoOptions()
        {
            foreach (DinoCatalogEntry entry in FjordurDinoCatalog.GetJapaneseOrderedEntries())
                dinoSearchBox.Items.Add(new DinoOption(entry.Name, entry.ClassName));
            if (dinoSearchBox.Items.Count > 0) dinoSearchBox.SelectedIndex = 0;
        }

        private void EnsureRemotePin()
        {
            if (settings.RemotePort < 1024 || settings.RemotePort > 65535) settings.RemotePort = 8765;
            string overridePin = Environment.GetEnvironmentVariable("ARK_MANAGER_REMOTE_PIN");
            if (Regex.IsMatch(overridePin ?? "", "^[0-9]{6}$")) { settings.RemotePin = overridePin; return; }
            if (Regex.IsMatch(settings.RemotePin ?? "", "^[0-9]{6}$")) return;
            byte[] bytes = new byte[4];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            settings.RemotePin = (BitConverter.ToUInt32(bytes, 0) % 1000000).ToString("000000", CultureInfo.InvariantCulture);
            try { settings.Save(); } catch { }
        }

        private void StartRemoteControl()
        {
            try
            {
                IPAddress tailnetAddress = FindTailnetAddress();
                remoteServer = new RemoteControlServer(settings.RemotePort, settings.RemotePin, tailnetAddress,
                    GetRemoteState, RemoteStartServer, RemoteStopServer, RemoteSendCommand, RemoteSearchDino,
                    GetRemoteSchedule, RemoteSaveSchedule, RemoteClearSchedule);
                remoteServer.Start();
                UpdateRemoteConnectionStatus();
            }
            catch (Exception ex)
            {
                if (remoteServiceStatus != null)
                {
                    remoteServiceStatus.Text = "開始できません: " + ex.Message;
                    remoteServiceStatus.ForeColor = Red;
                }
            }
        }

        private void UpdateRemoteConnectionStatus()
        {
            if (remoteServiceStatus == null || remoteServer == null) return;
            if (remoteServer.TailnetListening)
            {
                string directUrl = "http://" + settings.RemoteTailnetIp + ":" + settings.RemotePort;
                remoteServiceStatus.Text = "● Tailscale接続待受中  " + directUrl;
                remoteServiceStatus.ForeColor = Green;
                if (remoteUrlText != null) remoteUrlText.Text = directUrl;
                SaveCachedRemoteUrl(directUrl);
            }
            else
            {
                remoteServiceStatus.Text = "● PC内のみ起動中 — Tailscale待受を自動再試行中";
                remoteServiceStatus.ForeColor = Amber;
            }
        }

        private void RetryRemoteTailnetBinding()
        {
            if (remoteServer != null && !remoteServer.TailnetListening && remoteServer.RetryTailnetListener())
                UpdateRemoteConnectionStatus();
        }

        private IPAddress FindTailnetAddress()
        {
            IPAddress configured;
            if (IPAddress.TryParse(settings.RemoteTailnetIp, out configured) && IsTailnetIPv4(configured)) return configured;
            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    foreach (UnicastIPAddressInformation address in adapter.GetIPProperties().UnicastAddresses)
                    {
                        if (IsTailnetIPv4(address.Address))
                        {
                            settings.RemoteTailnetIp = address.Address.ToString();
                            try { settings.Save(); } catch { }
                            return address.Address;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static bool IsTailnetIPv4(IPAddress address)
        {
            if (address == null || address.AddressFamily != AddressFamily.InterNetwork) return false;
            byte[] bytes = address.GetAddressBytes();
            return bytes.Length == 4 && bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127;
        }

        private T UiInvoke<T>(Func<T> action)
        {
            if (IsDisposed) throw new ObjectDisposedException("ARK Server Manager");
            if (InvokeRequired) return (T)Invoke(action);
            return action();
        }

        private RemoteState GetRemoteState()
        {
            return UiInvoke(delegate
            {
                bool running = IsRunning();
                string log = logBox == null ? "" : logBox.Text;
                log = Regex.Replace(log, @"(?i)(ServerPassword|ServerAdminPassword)=([^?\s""]*)", "$1=********");
                if (log.Length > 9000)
                {
                    int start = log.Length - 9000;
                    int newline = log.IndexOf('\n', start);
                    log = newline >= 0 ? log.Substring(newline + 1) : log.Substring(start);
                }
                return new RemoteState
                {
                    Running = running,
                    Starting = running && launchPending,
                    Stopping = stopping,
                    CanOperate = running && !launchPending && !stopping,
                    Status = statusTitle == null ? (running ? "稼働中" : "停止中") : statusTitle.Text,
                    Detail = statusDetail == null ? "" : statusDetail.Text,
                    Uptime = uptimeValue == null ? "—" : uptimeValue.Text,
                    Cpu = cpuValue == null ? "—" : cpuValue.Text,
                    Memory = memoryValue == null ? "—" : memoryValue.Text,
                    Port = portValue == null ? settings.GamePort.ToString(CultureInfo.InvariantCulture) : portValue.Text,
                    Map = settings.MapName,
                    Session = settings.SessionName,
                    Schedule = GetScheduleSummary(),
                    Log = log
                };
            });
        }

        private RemoteScheduleState GetRemoteSchedule()
        {
            return UiInvoke(delegate { return ScheduleLogic.ToRemoteState(settings, DateTime.Now, scheduleLastAction); });
        }

        private string RemoteSaveSchedule(RemoteScheduleState request)
        {
            return UiInvoke(delegate { return ApplyScheduleSettings(request); });
        }

        private string RemoteClearSchedule()
        {
            return UiInvoke(delegate { return ClearAllSchedules("スマホからすべての予約を解除しました"); });
        }

        private string RemoteStartServer()
        {
            return UiInvoke(delegate
            {
                DiscoverProcess();
                if (IsRunning()) return "サーバーはすでに起動しています。";
                if (!PullSettings(false)) return "PC側の起動設定を確認してください。";
                try
                {
                    settings.Save();
                    ApplyRequiredIniSettings();
                    ProcessStartInfo psi = new ProcessStartInfo(settings.ExecutablePath, BuildArguments());
                    psi.WorkingDirectory = Path.GetDirectoryName(settings.ExecutablePath);
                    psi.UseShellExecute = true;
                    psi.WindowStyle = ProcessWindowStyle.Minimized;
                    serverProcess = Process.Start(psi);
                    deliberateStop = false; stopping = false; observedRunning = true; launchPending = true;
                    restartAt = null; serverStartedAt = DateTime.Now;
                    ResetCpuSample(); RefreshMonitor();
                    return "サーバーの起動を開始しました。";
                }
                catch (Exception ex) { return "起動エラー: " + ex.Message; }
            });
        }

        private string RemoteStopServer()
        {
            return UiInvoke(delegate
            {
                DiscoverProcess();
                if (!IsRunning()) return "サーバーは停止しています。";
                if (stopping) return "すでに停止処理中です。";
                StopServer(this, EventArgs.Empty);
                return "SaveWorld後の安全停止を開始しました。";
            });
        }

        private string RemoteSendCommand(string command)
        {
            if (command == null) return "コマンドを入力してください。";
            command = command.Trim();
            if (command.Length == 0 || command.Length > 1000 || command.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                return "コマンドは1行、1000文字以内で入力してください。";
            RemoteConnectionInfo info = UiInvoke(delegate
            {
                return new RemoteConnectionInfo
                {
                    Available = IsRunning() && !launchPending && !stopping,
                    Port = settings.RconPort,
                    Password = settings.AdminPassword
                };
            });
            if (!info.Available) return "サーバーの起動完了後に実行できます。";
            try
            {
                using (RconClient rcon = new RconClient("127.0.0.1", info.Port, info.Password))
                {
                    rcon.Connect();
                    string response = rcon.Command(command, 350);
                    return String.IsNullOrWhiteSpace(response) ? "送信しました（ARKからの応答テキストはありません）" : response.Trim();
                }
            }
            catch (Exception ex) { return "コマンドエラー: " + ex.Message; }
        }

        private string RemoteSearchDino(string name, string category)
        {
            bool available = UiInvoke(delegate { return IsRunning() && !launchPending && !stopping; });
            if (!available) return "サーバーの起動完了後に検索できます。";
            string className = UiInvoke(delegate { return ResolveDinoClassNameText(name ?? ""); });
            if (!Regex.IsMatch(className ?? "", "^[A-Za-z0-9_]+$") || className.Length > 120)
                return "恐竜名を一覧から選ぶか、正しいARKクラス名を入力してください。";
            category = category == "wild" || category == "tamed" ? category : "all";
            DinoSearchResult result = ExecuteSavedDinoSearch(className, category);
            StringBuilder output = new StringBuilder(result.Text ?? "");
            if (result.Locations.Count > 0)
            {
                output.AppendLine();
                output.AppendLine("場所（レベルが高い順／最大100個体）:");
                for (int i = 0; i < result.Locations.Count; i++)
                    output.AppendLine((i + 1).ToString(CultureInfo.InvariantCulture) + ") " + result.Locations[i]);
            }
            return output.ToString().Trim();
        }

        private string ResolveDinoClassNameText(string text)
        {
            text = (text ?? "").Trim();
            foreach (object item in dinoSearchBox.Items)
            {
                DinoOption candidate = item as DinoOption;
                if (candidate != null && (candidate.Name.Equals(text, StringComparison.OrdinalIgnoreCase) || candidate.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)) return candidate.ClassName;
            }
            Match parenthesized = Regex.Match(text, @"\(([A-Za-z0-9_]+_C)\)\s*$");
            return parenthesized.Success ? parenthesized.Groups[1].Value : text;
        }

        private void ConfigureTailscaleServe(object sender, EventArgs e)
        {
            DialogResult answer = MessageBox.Show(
                "このPCのTailscale IP（" + settings.RemoteTailnetIp + "）への接続をWindowsファイアウォールで許可します。\n\n管理者権限の確認画面で「はい」を押してください。",
                "スマホ接続を有効化", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            if (answer == DialogResult.OK) LaunchElevatedTailscaleSetup();
        }

        private void LaunchElevatedTailscaleSetup()
        {
            try
            {
                string script = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory, "Enable smartphone access.ps1");
                if (!File.Exists(script)) throw new FileNotFoundException("設定用ファイルが見つかりません。", script);
                string arguments = "-NoProfile -ExecutionPolicy Bypass -File " + QuoteProcessArgument(script) +
                    " -TailnetIp " + QuoteProcessArgument(settings.RemoteTailnetIp) +
                    " -Port " + settings.RemotePort.ToString(CultureInfo.InvariantCulture);
                ProcessStartInfo psi = new ProcessStartInfo("powershell.exe", arguments);
                psi.UseShellExecute = true; psi.Verb = "runas"; psi.WindowStyle = ProcessWindowStyle.Normal;
                Process.Start(psi);
                remoteUrlText.Text = "Windowsファイアウォールを設定中…";
                Task.Factory.StartNew<string>(delegate
                {
                    for (int i = 0; i < 120; i++)
                    {
                        string url = ReadCachedRemoteUrl();
                        if (url.Length > 0) return url;
                        Thread.Sleep(1000);
                    }
                    return "";
                }).ContinueWith(delegate(Task<string> task)
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    BeginInvoke((Action)delegate
                    {
                        remoteUrlText.Text = task.Result.Length > 0 ? task.Result : "設定完了後、http://" + settings.RemoteTailnetIp + ":" + settings.RemotePort + " を開いてください";
                    });
                });
            }
            catch (Exception ex) { MessageBox.Show("設定画面を開けませんでした。\n\n" + ex.Message, "スマホ接続", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private string RemoteUrlCachePath()
        {
            string directory = Environment.GetEnvironmentVariable("ARK_MANAGER_SETTINGS_DIR");
            if (String.IsNullOrWhiteSpace(directory))
                directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ARK Server Manager");
            return Path.Combine(directory, "remote-url.txt");
        }

        private string ReadCachedRemoteUrl()
        {
            try
            {
                string path = RemoteUrlCachePath();
                if (!File.Exists(path)) return "";
                string value = File.ReadAllText(path, Encoding.UTF8).Trim();
                return Regex.IsMatch(value, @"^https?://[A-Za-z0-9.-]+(?::\d+)?/?$") ? value.TrimEnd('/') : "";
            }
            catch { return ""; }
        }

        private void SaveCachedRemoteUrl(string url)
        {
            try
            {
                string path = RemoteUrlCachePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, url, new UTF8Encoding(false));
            }
            catch { }
        }

        private sealed class RemoteConnectionInfo
        {
            public bool Available;
            public int Port;
            public string Password;
        }

        private void UpdateLiveControlAvailability(bool available)
        {
            if (sendCommandButton == null) return;
            sendCommandButton.Enabled = available && !commandBusy;
            searchDinoButton.Enabled = available && !dinoSearchBusy;
            locateDinoButton.Enabled = available && !dinoSearchBusy && (lastDinoActors.Count > 0 || lastDinoLocations.Count > 0);
            commandInputBox.Enabled = available && !commandBusy;
            dinoSearchBox.Enabled = available && !dinoSearchBusy;
            dinoCategoryBox.Enabled = available && !dinoSearchBusy;
            if (refreshResourcesButton != null)
                refreshResourcesButton.Enabled = available && !resourceSearchBusy &&
                    settings.MapName.Equals("Fjordur", StringComparison.OrdinalIgnoreCase);
            UpdateMapButtonAvailability();
            if (available)
            {
                liveControlStatus.Text = "● RCON接続可能 — サーバー操作を実行できます";
                liveControlStatus.ForeColor = Green;
            }
            else if (IsRunning() && launchPending)
            {
                liveControlStatus.Text = "● サーバー起動中 — 起動完了ログを待っています";
                liveControlStatus.ForeColor = Amber;
            }
            else
            {
                liveControlStatus.Text = "● サーバー停止中 — 操作できません";
                liveControlStatus.ForeColor = Muted;
            }
        }

        private static List<ResourceZoneSnapshot> CreateResourceSnapshots()
        {
            return new List<ResourceZoneSnapshot>();
        }

        private static List<ResourceZoneSnapshot> CreateResourcePreviewSnapshots()
        {
            List<ResourceZoneSnapshot> zones = new List<ResourceZoneSnapshot>();
            double[,] centers = { { 76.0, 18.0 }, { 88.5, 13.0 }, { 19.0, 35.0 },
                { 24.0, 72.0 }, { 84.0, 82.0 }, { 86.0, 96.0 },
                { 43.0, 48.0 }, { 76.0, 42.0 }, { 16.0, 82.0 } };
            for (int i = 0; i < centers.GetLength(0); i++)
                zones.Add(new ResourceZoneSnapshot("preview_" + i, "小型資源スポット " + (i + 1), "プレビュー",
                    centers[i, 0], centers[i, 1], 0.25, 1 | 2 | 4));
            return zones;
        }

        private static int ResourceFilterMask(int selectedIndex)
        {
            return selectedIndex == 1 ? 1 : selectedIndex == 2 ? 2 : selectedIndex == 3 ? 4 : 0;
        }

        private static string ResourceCountText(ResourceZoneSnapshot zone, int mask, int value)
        {
            if ((zone.ExpectedMask & mask) == 0) return "—";
            return zone.Scanned ? value.ToString(CultureInfo.InvariantCulture) + "個" : "取得待ち";
        }

        private void RefreshResourceZoneList()
        {
            if (resourceZoneList == null) return;
            int selected = resourceMapCanvas == null ? -1 : resourceMapCanvas.SelectedZoneIndex;
            int filterMask = ResourceFilterMask(resourceTypeBox == null ? 0 : resourceTypeBox.SelectedIndex);
            resourceZoneList.BeginUpdate();
            resourceZoneList.Items.Clear();
            for (int i = 0; i < resourceSnapshots.Count; i++)
            {
                ResourceZoneSnapshot zone = resourceSnapshots[i];
                if (filterMask != 0 && (zone.ExpectedMask & filterMask) == 0) continue;
                ListViewItem item = new ListViewItem((i + 1).ToString(CultureInfo.InvariantCulture));
                item.Tag = i;
                item.SubItems.Add(zone.Name + "（" + zone.Realm + "）");
                item.SubItems.Add(ResourceCountText(zone, 1, zone.Metal));
                item.SubItems.Add(ResourceCountText(zone, 2, zone.Crystal));
                item.SubItems.Add(ResourceCountText(zone, 4, zone.Obsidian));
                resourceZoneList.Items.Add(item);
                if (i == selected) item.Selected = true;
            }
            resourceZoneList.EndUpdate();
            if (resourceZoneList.SelectedItems.Count == 0 && resourceZoneList.Items.Count > 0)
                resourceZoneList.Items[0].Selected = true;
        }

        private void RefreshResources(object sender, EventArgs e)
        {
            if (resourceSearchBusy) return;
            if (!settings.MapName.Equals("Fjordur", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("資源帯表示はFjordurマップ限定です。", "資源帯", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!IsRunning() || launchPending || stopping) { UpdateLiveControlAvailability(false); return; }
            resourceSearchBusy = true;
            refreshResourcesButton.Enabled = false;
            resourceStatusLabel.Text = "ボタン操作による資源走査を実行しています…（通常約10～20秒）";
            resourceStatusLabel.ForeColor = Amber;
            Task.Factory.StartNew(delegate { return QueryResourceProbe(); }).ContinueWith(delegate(Task<ResourceProbeResult> task)
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke((Action)delegate
                {
                    resourceSearchBusy = false;
                    ResourceProbeResult result = task.Result;
                    if (result.Success)
                    {
                        ApplyResourceProbeResponse(result.Response);
                        resourceStatusLabel.Text = "取得完了 — ボタンを押した時点の小型スポット別現存数（自動走査なし）";
                        resourceStatusLabel.ForeColor = Green;
                    }
                    else
                    {
                        resourceStatusLabel.Text = result.Error;
                        resourceStatusLabel.ForeColor = Color.FromArgb(235, 96, 96);
                    }
                    UpdateLiveControlAvailability(IsRunning() && !launchPending && !stopping);
                });
            });
        }

        private ResourceProbeResult QueryResourceProbe()
        {
            try
            {
                using (RconClient rcon = new RconClient("127.0.0.1", settings.RconPort, settings.AdminPassword))
                {
                    rcon.Connect();
                    string refresh = rcon.Command("ResourceProbe.Refresh", 350);
                    if (refresh.IndexOf("OK=1", StringComparison.OrdinalIgnoreCase) < 0)
                        return ResourceProbeResult.Fail("手動走査を開始できません。ResourceProbe 1.4の導入後にサーバーを再起動してください。");
                    for (int attempt = 0; attempt < 24; attempt++)
                    {
                        if (attempt > 0) Thread.Sleep(700);
                        string response = rcon.Command("ResourceProbe.Scan", 350);
                        if (Regex.IsMatch(response ?? "", @"(?m)^READY=1\s*$"))
                        {
                            double age = 0;
                            Match ageMatch = Regex.Match(response, @"(?m)^AGE_SECONDS=([0-9.]+)\s*$");
                            if (ageMatch.Success) Double.TryParse(ageMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out age);
                            bool scanningEnabled = !Regex.IsMatch(response, @"(?m)^SCANNING_ENABLED=0\s*$");
                            return ResourceProbeResult.Ok(response, age, scanningEnabled);
                        }
                        if (response.IndexOf("OK=1", StringComparison.OrdinalIgnoreCase) < 0)
                            return ResourceProbeResult.Fail("ResourceProbeが応答しません。プラグイン導入後にサーバーを再起動してください。");
                    }
                    return ResourceProbeResult.Fail("手動走査が30秒以内に完了しませんでした。サーバー負荷が落ち着いてからもう一度押してください。");
                }
            }
            catch (Exception ex) { return ResourceProbeResult.Fail("資源量の取得エラー: " + ex.Message); }
        }

        private void ApplyResourceProbeResponse(string response)
        {
            List<ResourceZoneSnapshot> parsed = new List<ResourceZoneSnapshot>();
            Dictionary<string, int> sourceNumbers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            MatchCollection lines = Regex.Matches(response ?? "", @"(?m)^ZONE=([^\r\n]+)$");
            foreach (Match line in lines)
            {
                Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string[] fields = line.Groups[1].Value.Trim().Split('|');
                if (fields.Length == 0) continue;
                values["ZONE"] = fields[0];
                foreach (string field in fields.Skip(1))
                {
                    int equals = field.IndexOf('=');
                    if (equals > 0) values[field.Substring(0, equals)] = field.Substring(equals + 1);
                }
                string id;
                if (!values.TryGetValue("ZONE", out id)) continue;
                string source; if (!values.TryGetValue("SOURCE", out source)) source = id;
                string realm; if (!values.TryGetValue("REALM", out realm)) realm = "MIDGARD";
                double latitude = ParseResourceDouble(values, "LAT");
                double longitude = ParseResourceDouble(values, "LON");
                double radius = ParseResourceDouble(values, "RADIUS");
                int expected = ParseResourceInt(values, "EXPECTED");
                int resourceRockCount = ParseResourceInt(values, "FOLIAGE");
                if (resourceRockCount <= 5) continue;
                int number; sourceNumbers.TryGetValue(source, out number); number++; sourceNumbers[source] = number;
                ResourceZoneSnapshot zone = new ResourceZoneSnapshot(id,
                    ResourceSourceDisplayName(source) + " " + number.ToString(CultureInfo.InvariantCulture),
                    ResourceRealmDisplayName(realm), latitude, longitude, radius, expected);
                zone.Scanned = ParseResourceInt(values, "SCANNED") == 1;
                zone.Metal = ParseResourceInt(values, "METAL");
                zone.Crystal = ParseResourceInt(values, "CRYSTAL");
                zone.Obsidian = ParseResourceInt(values, "OBSIDIAN");
                zone.MetalHealth = ParseResourceInt(values, "METAL_HP");
                zone.CrystalHealth = ParseResourceInt(values, "CRYSTAL_HP");
                zone.ObsidianHealth = ParseResourceInt(values, "OBSIDIAN_HP");
                parsed.Add(zone);
            }
            resourceSnapshots = parsed;
            RefreshResourceZoneList();
            if (resourceMapCanvas != null) resourceMapCanvas.SetSnapshots(resourceSnapshots);
        }

        private static string ResourceSourceDisplayName(string source)
        {
            switch (source ?? "")
            {
                case "vardiland_snow": return "ヴァルディランド雪山";
                case "dvergheim_mines": return "ドヴェルグヘイム鉱山";
                case "vannaland_north": return "ヴァナランド北部山岳";
                case "vannaland_east": return "ヴァナランド東部山岳";
                case "balheimr_volcano": return "バルヘイム火山";
                case "space_cave": return "宇宙洞窟";
                case "asgard_mountains": return "アスガルド山岳";
                case "jotunheim_ice": return "ヨトゥンヘイム氷山";
                case "vanaheim_crystal": return "ヴァナヘイム水晶地帯";
                default: return "資源スポット";
            }
        }

        private static string ResourceRealmDisplayName(string realm)
        {
            switch ((realm ?? "").ToUpperInvariant())
            {
                case "ASGARD": return "アスガルド";
                case "VANAHEIM": return "ヴァナヘイム";
                case "JOTUNHEIM": return "ヨトゥンヘイム";
                default: return "ミッドガルド";
            }
        }

        private static int ParseResourceInt(Dictionary<string, string> values, string key)
        {
            string text; int value;
            return values.TryGetValue(key, out text) && Int32.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : 0;
        }

        private static double ParseResourceDouble(Dictionary<string, string> values, string key)
        {
            string text; double value;
            return values.TryGetValue(key, out text) && Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : 0;
        }

        private void SendServerCommand(object sender, EventArgs e)
        {
            string command = commandInputBox.Text.Trim();
            if (command.Length == 0) return;
            if (!IsRunning() || launchPending || stopping) { UpdateLiveControlAvailability(false); return; }
            commandBusy = true; sendCommandButton.Enabled = false;
            commandOutputBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] > " + command + "\r\n");
            Task.Factory.StartNew(delegate
            {
                try
                {
                    using (RconClient rcon = new RconClient("127.0.0.1", settings.RconPort, settings.AdminPassword))
                    {
                        rcon.Connect();
                        string response = rcon.Command(command, 350);
                        return String.IsNullOrWhiteSpace(response) ? "送信しました（ARKからの応答テキストはありません）" : response.Trim();
                    }
                }
                catch (Exception ex) { return "エラー: " + ex.Message; }
            }).ContinueWith(delegate(Task<string> task)
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke((Action)delegate
                {
                    commandBusy = false; commandOutputBox.AppendText(task.Result + "\r\n\r\n");
                    commandOutputBox.SelectionStart = commandOutputBox.TextLength; commandOutputBox.ScrollToCaret();
                    UpdateLiveControlAvailability(IsRunning() && !launchPending && !stopping);
                });
            });
        }

        private void SearchDinos(object sender, EventArgs e)
        {
            ClearDinoSearchCache();
            string className = ResolveDinoClassName();
            if (className.Length == 0)
            {
                MessageBox.Show("恐竜を選択するか、ARKのクラス名を入力してください。", "恐竜検索", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!Regex.IsMatch(className, "^[A-Za-z0-9_]+$") || className.Length > 120)
            {
                MessageBox.Show("クラス名には英数字とアンダースコアだけを使用してください。", "恐竜検索", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!IsRunning() || launchPending || stopping) { UpdateLiveControlAvailability(false); return; }
            string category = dinoCategoryBox.SelectedIndex == 1 ? "wild" : dinoCategoryBox.SelectedIndex == 2 ? "tamed" : "all";
            string categoryLabel = category == "wild" ? "野生のみ" : category == "tamed" ? "テイム済みのみ" : "すべて";
            dinoSearchBusy = true; searchDinoButton.Enabled = false; locateDinoButton.Enabled = false; dinoCountLabel.Text = "個体数: 検索中…";
            dinoResultBox.Text = categoryLabel + "の個体数とレベルを検索しています。検索中だけ保存データを読み込みます。\r\n";
            Task.Factory.StartNew(delegate { return ExecuteDinoCountSearch(className, category); }).ContinueWith(delegate(Task<DinoSearchResult> task)
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke((Action)delegate
                {
                    dinoSearchBusy = false; DinoSearchResult result = task.Result;
                    dinoCountLabel.Text = result.Count >= 0 ? "個体数: " + result.Count : "個体数: 取得待ち";
                    dinoResultBox.Text = result.Text;
                    lastDinoActors = result.Actors;
                    lastDinoLocations = result.Locations;
                    lastDinoClassName = result.ClassName;
                    lastDinoCategory = categoryLabel;
                    dinoResultBox.SelectionStart = 0;
                    UpdateLiveControlAvailability(IsRunning() && !launchPending && !stopping);
                });
            });
        }

        private string ResolveDinoClassName()
        {
            DinoOption option = dinoSearchBox.SelectedItem as DinoOption;
            if (option != null && dinoSearchBox.Text == option.ToString()) return option.ClassName;
            return ResolveDinoClassNameText(dinoSearchBox.Text);
        }

        private DinoSearchResult ExecuteDinoCountSearch(string className, string category)
        {
            return ExecuteSavedDinoSearch(className, category);
        }

        private DinoSearchResult ExecuteSavedDinoSearch(string className, string category)
        {
            string helperDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
            string helperPath = Path.Combine(helperDirectory, "ARK Dino Search.exe");
            string savePath = Path.Combine(settings.ServerRoot ?? "", "ShooterGame", "Saved", "SavedArks", settings.MapName + ".ark");
            if (!File.Exists(helperPath))
                return new DinoSearchResult(-1, "恐竜検索用の追加ファイルが見つかりません。\r\n" + helperPath, className, new List<string>(), new List<string>());
            if (!File.Exists(savePath))
                return new DinoSearchResult(-1, "マップの保存データが見つかりません。\r\n" + savePath, className, new List<string>(), new List<string>());

            DinoSaveSnapshot current;
            string errorMessage;
            if (!TryLoadDinoSnapshot(helperPath, savePath, className, category, out current, out errorMessage))
                return new DinoSearchResult(-1, "保存データ検索エラー: " + errorMessage, className, new List<string>(), new List<string>());

            HashSet<string> stationaryWildIds = new HashSet<string>(StringComparer.Ordinal);
            string historyStatus;
            if (category == "tamed")
            {
                historyStatus = "［死］判定は野生個体だけが対象です。";
            }
            else
            {
                List<string> historyPaths = GetDinoHistorySavePaths(savePath, settings.MapName);
                if (historyPaths.Count < DinoHistoryLogic.RequiredSaveCount)
                {
                    historyStatus = "［死］判定: 履歴不足（" + historyPaths.Count + "/5セーブ）。";
                }
                else
                {
                    List<DinoSaveSnapshot> olderSnapshots = new List<DinoSaveSnapshot>();
                    string historyError = "";
                    for (int i = 1; i < historyPaths.Count; i++)
                    {
                        DinoSaveSnapshot older;
                        if (!TryLoadDinoSnapshot(helperPath, historyPaths[i], className, "wild", out older, out historyError)) break;
                        olderSnapshots.Add(older);
                    }
                    if (olderSnapshots.Count == DinoHistoryLogic.RequiredSaveCount - 1)
                    {
                        stationaryWildIds = DinoHistoryLogic.FindStationaryWildDinoIds(current, olderSnapshots);
                        historyStatus = "［死］=同じ野生個体が現在＋直近4セーブで1m以上動いていない疑い（死亡確定ではありません）。";
                    }
                    else
                    {
                        historyStatus = "［死］判定を完了できませんでした: " + historyError;
                    }
                }
            }

            List<string> locations = new List<string>();
            foreach (DinoLocationRecord location in current.Locations)
                locations.Add(FormatDinoLocation(location, stationaryWildIds.Contains(location.DinoId)));

            string categoryLabel = category == "wild" ? "野生のみ" : category == "tamed" ? "テイム済みのみ" : "すべて";
            StringBuilder output = new StringBuilder();
            output.AppendLine("検索クラス: " + className);
            output.AppendLine("検索対象: " + categoryLabel);
            output.AppendLine("個体数: " + current.Count);
            if (current.SavedAt.HasValue) output.AppendLine("基準データ: " + current.SavedAt.Value.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss") + " のサーバー保存時点");
            output.AppendLine(historyStatus);
            output.AppendLine();
            output.AppendLine("保存データはボタン操作時だけ読み込みます。常時監視はしていません。");
            output.AppendLine("場所は「場所を検索」を押すとレベルが高い順に表示します。");
            return new DinoSearchResult(current.Count, output.ToString(), className, new List<string>(), locations);
        }

        private List<string> GetDinoHistorySavePaths(string currentSavePath, string mapName)
        {
            List<string> result = new List<string> { currentSavePath };
            string directory = Path.GetDirectoryName(currentSavePath);
            if (String.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return result;
            try
            {
                IEnumerable<string> backups = Directory.GetFiles(directory, (mapName ?? "") + "_*.ark")
                    .Where(delegate(string path) { return !String.Equals(Path.GetFullPath(path), Path.GetFullPath(currentSavePath), StringComparison.OrdinalIgnoreCase); })
                    .OrderByDescending(delegate(string path) { return File.GetLastWriteTimeUtc(path); });
                foreach (string backup in backups)
                {
                    if (result.Count >= DinoHistoryLogic.RequiredSaveCount) break;
                    result.Add(backup);
                }
            }
            catch { }
            return result;
        }

        private bool TryLoadDinoSnapshot(string helperPath, string savePath, string className, string category, out DinoSaveSnapshot snapshot, out string errorMessage)
        {
            snapshot = null;
            errorMessage = "";
            FileInfo saveInfo;
            string cacheKey;
            try
            {
                saveInfo = new FileInfo(savePath);
                if (!saveInfo.Exists) { errorMessage = "保存データが見つかりません: " + savePath; return false; }
                cacheKey = saveInfo.FullName + "|" + saveInfo.Length + "|" + saveInfo.LastWriteTimeUtc.Ticks + "|" + className + "|" + category;
            }
            catch (Exception ex) { errorMessage = ex.Message; return false; }
            lock (dinoSnapshotCacheLock)
            {
                if (dinoSnapshotCache.TryGetValue(cacheKey, out snapshot)) return true;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(helperPath,
                    QuoteProcessArgument(savePath) + " " + QuoteProcessArgument(className) + " " + category);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                string stdout;
                string stderr;
                using (Process process = Process.Start(psi))
                {
                    Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
                    Task<string> stderrTask = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(30000))
                    {
                        try { process.Kill(); } catch { }
                        errorMessage = "検索が30秒を超えたため中止しました。";
                        return false;
                    }
                    process.WaitForExit();
                    stdout = stdoutTask.Result;
                    stderr = stderrTask.Result;
                    Match helperError = Regex.Match(stdout ?? "", @"(?m)^ERROR=(.+)$");
                    if (process.ExitCode != 0 || helperError.Success)
                    {
                        errorMessage = helperError.Success ? helperError.Groups[1].Value.Trim() : stderr.Trim();
                        return false;
                    }
                }

                DinoSaveSnapshot parsed = new DinoSaveSnapshot();
                Match countMatch = Regex.Match(stdout ?? "", @"(?m)^COUNT=(\d+)\s*$");
                if (countMatch.Success) Int32.TryParse(countMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed.Count);
                Match savedMatch = Regex.Match(stdout ?? "", @"(?m)^SAVED_AT=(.+)$");
                DateTimeOffset savedAt;
                if (savedMatch.Success && DateTimeOffset.TryParse(savedMatch.Groups[1].Value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out savedAt))
                    parsed.SavedAt = savedAt;
                MatchCollection locationMatches = Regex.Matches(stdout ?? "", @"(?m)^LOCATION=(.+)$");
                foreach (Match match in locationMatches)
                {
                    DinoLocationRecord record;
                    if (TryParseDinoHelperLocation(match.Groups[1].Value.Trim(), out record)) parsed.Locations.Add(record);
                }
                snapshot = parsed;
                lock (dinoSnapshotCacheLock)
                {
                    if (dinoSnapshotCache.Count > 64) dinoSnapshotCache.Clear();
                    dinoSnapshotCache[cacheKey] = parsed;
                }
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private static bool TryParseDinoHelperLocation(string line, out DinoLocationRecord record)
        {
            record = null;
            Match core = Regex.Match(line ?? "", @"^Lv\.(?<level>\d+)\s+DINOID=(?<id1>-?\d+):(?<id2>-?\d+)\s+TYPE=(?<type>WILD|TAMED).*?\bX=(?<x>-?\d+(?:\.\d+)?)\s+Y=(?<y>-?\d+(?:\.\d+)?)\s+Z=(?<z>-?\d+(?:\.\d+)?)$", RegexOptions.IgnoreCase);
            if (!core.Success) return false;
            int level;
            double x, y, z;
            if (!Int32.TryParse(core.Groups["level"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out level) ||
                !Double.TryParse(core.Groups["x"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
                !Double.TryParse(core.Groups["y"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out y) ||
                !Double.TryParse(core.Groups["z"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out z)) return false;
            DinoLocationRecord parsed = new DinoLocationRecord
            {
                Level = level,
                DinoId = core.Groups["id1"].Value + ":" + core.Groups["id2"].Value,
                IsWild = core.Groups["type"].Value.Equals("WILD", StringComparison.OrdinalIgnoreCase),
                X = x,
                Y = y,
                Z = z
            };
            Match gps = Regex.Match(line ?? "", @"\bAREA=(?<area>[A-Z_]+)\s+LAT=(?<lat>-?\d+(?:\.\d+)?)\s+LON=(?<lon>-?\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            double latitude, longitude;
            if (gps.Success && Double.TryParse(gps.Groups["lat"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out latitude) &&
                Double.TryParse(gps.Groups["lon"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out longitude))
            {
                parsed.HasGps = true;
                parsed.Area = gps.Groups["area"].Value;
                parsed.Latitude = latitude;
                parsed.Longitude = longitude;
            }
            record = parsed;
            return true;
        }

        private static string FormatDinoLocation(DinoLocationRecord location, bool suspectedDead)
        {
            string marker = suspectedDead ? " ［死］" : "";
            if (location.HasGps)
            {
                string raw = String.Format(CultureInfo.InvariantCulture,
                    "Lv.{0}{1}  AREA={2}  LAT={3:F2} LON={4:F2} Z={5:F1}",
                    location.Level, marker, location.Area, location.Latitude, location.Longitude, location.Z);
                return LocalizeFjordurArea(raw);
            }
            return String.Format(CultureInfo.InvariantCulture, "Lv.{0}{1}  X={2:F1} Y={3:F1} Z={4:F1}",
                location.Level, marker, location.X, location.Y, location.Z);
        }

        private static string QuoteProcessArgument(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }

        private static string LocalizeFjordurArea(string location)
        {
            string localized = Regex.Replace(location ?? "", @"\bAREA=([A-Z_]+)", delegate(Match match)
            {
                switch (match.Groups[1].Value)
                {
                    case "ASGARD": return "エリア=アスガルド";
                    case "VANAHEIM": return "エリア=ヴァナヘイム";
                    case "JOTUNHEIM": return "エリア=ヨトゥンヘイム";
                    case "MIDGARD_BALHEIMR": return "エリア=ミッドガルド（バルヘイム）";
                    case "MIDGARD_BOLBJORD": return "エリア=ミッドガルド（ボルビョルド）";
                    case "MIDGARD_VARDILAND": return "エリア=ミッドガルド（ヴァルディランド）";
                    case "MIDGARD_VANNALAND": return "エリア=ミッドガルド（ヴァナランド）";
                    case "MIDGARD_OCEAN_CAVE": return "エリア=ミッドガルド（海域・洞窟）";
                    default: return match.Value;
                }
            });
            return Regex.Replace(localized, @"\bLAT=(-?[0-9.]+)\s+LON=(-?[0-9.]+)", "緯度=$1  経度=$2");
        }

        private List<string> ExtractExtendedRconLocations(string response)
        {
            List<string> locations = new List<string>();
            MatchCollection matches = Regex.Matches(response ?? "", @"Location\s+X=(-?\d+(?:\.\d+)?)\s+Y=(-?\d+(?:\.\d+)?)\s+Z=(-?\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            foreach (Match match in matches)
                locations.Add(FormatVisibleCoordinates(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value));
            return locations;
        }

        private void LocateDinos(object sender, EventArgs e)
        {
            if ((lastDinoActors.Count == 0 && lastDinoLocations.Count == 0) || dinoSearchBusy) return;
            if (!IsRunning() || launchPending || stopping) { UpdateLiveControlAvailability(false); return; }
            if (lastDinoLocations.Count > 0)
            {
                StringBuilder cached = new StringBuilder();
                cached.AppendLine("検索クラス: " + lastDinoClassName);
                cached.AppendLine("検索対象: " + lastDinoCategory);
                cached.AppendLine("座標表示: " + lastDinoLocations.Count + "（レベルが高い順／最大100個体）");
                cached.AppendLine();
                for (int i = 0; i < lastDinoLocations.Count; i++)
                    cached.AppendLine((i + 1).ToString() + ") " + lastDinoLocations[i]);
                dinoResultBox.Text = cached.ToString();
                SelectFirstDinoLocationLine();
                return;
            }
            List<string> actors = new List<string>(lastDinoActors);
            string className = lastDinoClassName;
            dinoSearchBusy = true; locateDinoButton.Enabled = false; searchDinoButton.Enabled = false;
            dinoResultBox.Text = className + " の場所を照会しています（最大100個体）。\r\n";
            Task.Factory.StartNew(delegate { return ExecuteDinoLocationSearch(className, actors); }).ContinueWith(delegate(Task<string> task)
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke((Action)delegate
                {
                    dinoSearchBusy = false; dinoResultBox.Text = task.Result; dinoResultBox.SelectionStart = 0;
                    UpdateLiveControlAvailability(IsRunning() && !launchPending && !stopping);
                });
            });
        }

        private string ExecuteDinoLocationSearch(string className, List<string> actors)
        {
            try
            {
                using (RconClient rcon = new RconClient("127.0.0.1", settings.RconPort, settings.AdminPassword))
                {
                    rcon.Connect();
                    int limit = Math.Min(actors.Count, 100);
                    List<string> locations = new List<string>();
                    for (int i = 0; i < limit; i++)
                    {
                        string response = rcon.Command("PrintActorLocation " + actors[i], 80);
                        locations.Add(ExtractLocation(response));
                    }
                    if (locations.Any(delegate(string value) { return value.Length == 0; }))
                    {
                        Thread.Sleep(400);
                        string consoleSnapshot = ReadServerConsoleTail();
                        for (int i = 0; i < limit; i++)
                            if (locations[i].Length == 0) locations[i] = ExtractLocationForActor(consoleSnapshot, actors[i]);
                    }
                    StringBuilder output = new StringBuilder();
                    output.AppendLine("検索クラス: " + className);
                    output.AppendLine("個体数: " + actors.Count + " / 座標表示: " + limit);
                    output.AppendLine();
                    for (int i = 0; i < limit; i++)
                    {
                        output.Append((i + 1).ToString()).Append(") ").Append(actors[i]);
                        output.AppendLine(locations[i].Length > 0 ? "  " + locations[i] : "  （座標応答なし）");
                    }
                    if (actors.Count > limit) output.AppendLine("ほか " + (actors.Count - limit) + " 個体（表示上限を超えたため省略）");
                    return output.ToString();
                }
            }
            catch (Exception ex) { return "場所検索エラー: " + ex.Message; }
        }

        private void SelectFirstDinoLocationLine()
        {
            if (dinoResultBox == null) return;
            for (int i = 0; i < dinoResultBox.Lines.Length; i++)
            {
                DinoMapPoint ignored;
                if (!TryParseDinoMapPoint(dinoResultBox.Lines[i], out ignored)) continue;
                int start = dinoResultBox.GetFirstCharIndexFromLine(i);
                if (start >= 0) dinoResultBox.Select(start, dinoResultBox.Lines[i].Length);
                UpdateMapButtonAvailability();
                return;
            }
            dinoResultBox.SelectionStart = 0;
            UpdateMapButtonAvailability();
        }

        private void UpdateMapButtonAvailability()
        {
            if (showDinoMapButton == null || dinoResultBox == null) return;
            DinoMapPoint point;
            bool hasPoint = TryGetSelectedDinoMapPoint(out point);
            bool alreadySelected = hasPoint && FindSelectedDinoMapPoint(point) >= 0;
            showDinoMapButton.Enabled = hasPoint && (alreadySelected || selectedDinoMapPoints.Count < 5);
            showDinoMapButton.Text = alreadySelected
                ? "選択から外す (" + selectedDinoMapPoints.Count + "/5)"
                : "マップへ追加 (" + selectedDinoMapPoints.Count + "/5)";
            if (openDinoMapButton != null)
            {
                openDinoMapButton.Enabled = selectedDinoMapPoints.Count > 0;
                openDinoMapButton.Text = "選択中を表示 (" + selectedDinoMapPoints.Count + "/5)";
            }
            HighlightDinoMapSelections();
        }

        private int FindSelectedDinoMapPoint(DinoMapPoint point)
        {
            for (int i = 0; i < selectedDinoMapPoints.Count; i++)
                if (SameDinoMapPoint(selectedDinoMapPoints[i], point)) return i;
            return -1;
        }

        private static bool SameDinoMapPoint(DinoMapPoint a, DinoMapPoint b)
        {
            if (a == null || b == null) return false;
            if (a.ResultIndex > 0 && b.ResultIndex > 0) return a.ResultIndex == b.ResultIndex;
            return a.Level == b.Level &&
                String.Equals(a.Area, b.Area, StringComparison.Ordinal) &&
                Math.Abs(a.Latitude - b.Latitude) < 0.0001 &&
                Math.Abs(a.Longitude - b.Longitude) < 0.0001 &&
                Math.Abs(a.Z - b.Z) < 0.05;
        }

        private void HighlightDinoMapSelections()
        {
            if (dinoResultBox == null || updatingDinoHighlight) return;
            updatingDinoHighlight = true;
            Point scrollPosition = Point.Empty;
            bool preserveScroll = dinoResultBox.IsHandleCreated;
            if (preserveScroll)
            {
                SendMessage(dinoResultBox.Handle, EM_GETSCROLLPOS, IntPtr.Zero, ref scrollPosition);
                SendMessage(dinoResultBox.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            }
            try
            {
                int selectionStart = dinoResultBox.SelectionStart;
                int selectionLength = dinoResultBox.SelectionLength;
                int currentLine = dinoResultBox.GetLineFromCharIndex(selectionStart);
                dinoResultBox.SelectAll();
                dinoResultBox.SelectionBackColor = dinoResultBox.BackColor;
                dinoResultBox.SelectionColor = dinoResultBox.ForeColor;
                string[] lines = dinoResultBox.Lines;
                for (int i = 0; i < lines.Length; i++)
                {
                    DinoMapPoint point;
                    if (!TryParseDinoMapPoint(lines[i], out point)) continue;
                    bool added = FindSelectedDinoMapPoint(point) >= 0;
                    if (!added && i != currentLine) continue;
                    int start = dinoResultBox.GetFirstCharIndexFromLine(i);
                    if (start < 0) continue;
                    dinoResultBox.Select(start, lines[i].Length);
                    dinoResultBox.SelectionBackColor = added
                        ? (i == currentLine ? Color.FromArgb(93, 76, 166) : Color.FromArgb(49, 67, 91))
                        : Color.FromArgb(82, 67, 35);
                    dinoResultBox.SelectionColor = Color.White;
                }
                dinoResultBox.Select(Math.Min(selectionStart, dinoResultBox.TextLength),
                    Math.Min(selectionLength, Math.Max(0, dinoResultBox.TextLength - selectionStart)));
            }
            finally
            {
                if (preserveScroll)
                {
                    SendMessage(dinoResultBox.Handle, EM_SETSCROLLPOS, IntPtr.Zero, ref scrollPosition);
                    SendMessage(dinoResultBox.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
                    dinoResultBox.Invalidate();
                }
                updatingDinoHighlight = false;
            }
        }

        private bool TryGetSelectedDinoMapPoint(out DinoMapPoint point)
        {
            point = null;
            if (dinoResultBox == null || dinoResultBox.TextLength == 0) return false;
            int lineIndex = dinoResultBox.GetLineFromCharIndex(dinoResultBox.SelectionStart);
            string[] lines = dinoResultBox.Lines;
            return lineIndex >= 0 && lineIndex < lines.Length && TryParseDinoMapPoint(lines[lineIndex], out point);
        }

        private static bool TryParseDinoMapPoint(string line, out DinoMapPoint point)
        {
            point = null;
            Match match = Regex.Match(line ?? "",
                @"Lv\.(?<level>\d+).*?(?:エリア=(?<area>.*?)\s{2,})?緯度=(?<lat>-?\d+(?:\.\d+)?)\s+経度=(?<lon>-?\d+(?:\.\d+)?)\s+Z=(?<z>-?\d+(?:\.\d+)?)",
                RegexOptions.IgnoreCase);
            if (!match.Success) return false;
            int level;
            double latitude, longitude, z;
            if (!Int32.TryParse(match.Groups["level"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out level) ||
                !Double.TryParse(match.Groups["lat"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out latitude) ||
                !Double.TryParse(match.Groups["lon"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out longitude) ||
                !Double.TryParse(match.Groups["z"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out z)) return false;
            string area = match.Groups["area"].Success ? match.Groups["area"].Value.Trim() : "座標マップ";
            int resultIndex = 0;
            Match indexMatch = Regex.Match(line ?? "", @"^\s*(?<index>\d+)\)");
            if (indexMatch.Success) Int32.TryParse(indexMatch.Groups["index"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out resultIndex);
            point = new DinoMapPoint(resultIndex, level, area, latitude, longitude, z);
            return true;
        }

        private void ShowSelectedDinoOnMap(object sender, EventArgs e)
        {
            DinoMapPoint point;
            if (!TryGetSelectedDinoMapPoint(out point))
            {
                MessageBox.Show("場所検索結果の座標行を選択してください。", "恐竜位置マップ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int selectedIndex = FindSelectedDinoMapPoint(point);
            if (selectedIndex >= 0) selectedDinoMapPoints.RemoveAt(selectedIndex);
            else if (selectedDinoMapPoints.Count < 5) selectedDinoMapPoints.Add(point);
            else
            {
                MessageBox.Show("マップへ追加できるのは最大5体です。いずれかを選択から外してください。", "恐竜位置マップ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            UpdateMapButtonAvailability();
        }

        private void OpenSelectedDinosOnMap(object sender, EventArgs e)
        {
            if (selectedDinoMapPoints.Count == 0)
            {
                MessageBox.Show("座標行を選び「マップへ追加」を押してください。", "恐竜位置マップ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int activeIndex = 0;
            DinoMapPoint current;
            if (TryGetSelectedDinoMapPoint(out current))
            {
                int found = FindSelectedDinoMapPoint(current);
                if (found >= 0) activeIndex = found;
            }
            using (DinoMapDialog dialog = new DinoMapDialog(new List<DinoMapPoint>(selectedDinoMapPoints), activeIndex)) dialog.ShowDialog(this);
        }

        private void ClearDinoSearchCache()
        {
            lastDinoActors = new List<string>(); lastDinoLocations = new List<string>(); lastDinoClassName = ""; lastDinoCategory = "";
            selectedDinoMapPoints.Clear();
            if (locateDinoButton != null) locateDinoButton.Enabled = false;
            if (showDinoMapButton != null) { showDinoMapButton.Enabled = false; showDinoMapButton.Text = "マップへ追加 (0/5)"; }
            if (openDinoMapButton != null) { openDinoMapButton.Enabled = false; openDinoMapButton.Text = "選択中を表示 (0/5)"; }
        }

        private List<string> ExtractActorNames(string response, string className)
        {
            List<string> names = new List<string>();
            if (String.IsNullOrWhiteSpace(response)) return names;
            MatchCollection matches = Regex.Matches(response, @"(?:PersistentLevel\.)?([A-Za-z0-9_]+_C_\d+)", RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                string name = match.Groups[1].Value;
                if (name.IndexOf(className, StringComparison.OrdinalIgnoreCase) >= 0 && !names.Contains(name)) names.Add(name);
            }
            return names;
        }

        private string ExtractLocation(string response)
        {
            if (String.IsNullOrWhiteSpace(response)) return "";
            Match m = Regex.Match(response, @"X\s*=\s*(-?[0-9.]+).*?Y\s*=\s*(-?[0-9.]+).*?Z\s*=\s*(-?[0-9.]+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!m.Success) return response.Trim().Replace("\r", " ").Replace("\n", " ");
            return FormatVisibleCoordinates(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);
        }

        private string FormatVisibleCoordinates(string xText, string yText, string zText)
        {
            double x, y, z;
            if (settings.MapName.Equals("Fjordur", StringComparison.OrdinalIgnoreCase) &&
                Double.TryParse(xText, NumberStyles.Float, CultureInfo.InvariantCulture, out x) &&
                Double.TryParse(yText, NumberStyles.Float, CultureInfo.InvariantCulture, out y) &&
                Double.TryParse(zText, NumberStyles.Float, CultureInfo.InvariantCulture, out z))
            {
                double latitude = 50D + y / 7140D;
                double longitude = 50D + x / 7140D;
                return "緯度=" + latitude.ToString("0.00", CultureInfo.InvariantCulture) +
                    "  経度=" + longitude.ToString("0.00", CultureInfo.InvariantCulture) +
                    "  Z=" + z.ToString("0.0", CultureInfo.InvariantCulture);
            }
            return "X=" + xText + "  Y=" + yText + "  Z=" + zText;
        }

        private string ExtractLocationForActor(string consoleText, string actorName)
        {
            if (String.IsNullOrEmpty(consoleText)) return "";
            int index = consoleText.LastIndexOf(actorName, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return "";
            int start = Math.Max(0, index - 250); int length = Math.Min(consoleText.Length - start, 700);
            return ExtractLocation(consoleText.Substring(start, length));
        }

        private string ReadServerConsoleTail()
        {
            if (!IsRunning()) return "";
            lock (ConsoleReadLock)
            {
                bool attached = false;
                try
                {
                    FreeConsole();
                    attached = AttachConsole((uint)serverProcess.Id);
                    if (!attached) return "";
                    IntPtr output = GetStdHandle(-11);
                    ConsoleBufferInfo info;
                    if (output == IntPtr.Zero || output == new IntPtr(-1) || !GetConsoleScreenBufferInfo(output, out info)) return "";
                    int width = Math.Max(1, (int)info.Size.X);
                    int endRow = Math.Max(0, (int)info.CursorPosition.Y);
                    int startRow = Math.Max(0, endRow - 700);
                    int count = Math.Min(1000000, width * (endRow - startRow + 1));
                    StringBuilder text = new StringBuilder(count);
                    int read;
                    if (!ReadConsoleOutputCharacter(output, text, count, new ConsoleCoordinate(0, (short)startRow), out read)) return "";
                    string raw = text.ToString(0, Math.Min(read, text.Length));
                    StringBuilder lines = new StringBuilder(raw.Length + 256);
                    for (int offset = 0; offset < raw.Length; offset += width)
                    {
                        int lineLength = Math.Min(width, raw.Length - offset);
                        string line = raw.Substring(offset, lineLength).TrimEnd('\0', ' ');
                        if (line.Length > 0) lines.AppendLine(line);
                    }
                    return lines.ToString();
                }
                catch { return ""; }
                finally { if (attached) FreeConsole(); }
            }
        }

        private void AddField(TableLayoutPanel form, int labelCol, int row, string label, Control control, int inputCol, int span)
        {
            string standard = GetStandardValue(label);
            string text = String.IsNullOrEmpty(standard) ? label : label + "\r\n標準: " + standard;
            Label l = new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Muted, Font = new Font("Yu Gothic UI", 9F), Margin = new Padding(0, 0, 8, 0) };
            form.Controls.Add(l, labelCol, row); form.Controls.Add(control, inputCol, row);
            if (span > 1) form.SetColumnSpan(control, span);
        }

        private string GetStandardValue(string label)
        {
            switch (label)
            {
                case "サーバー保存先": return @"D:\arkserver";
                case "マップ": return "TheIsland";
                case "サーバー名": return "ARK Server";
                case "最大人数": return "70";
                case "ゲームポート": return "7777";
                case "Queryポート": return "27015";
                case "RCONポート": return "27020";
                case "参加パスワード": return "なし";
                case "管理パスワード": return "なし";
                case "追加オプション": return "-server -log";
                case "自動復旧": return "無効";
                case "再起動まで（秒）": return "15";
                case "ゲームモード": return "PvP";
                case "シングルプレイヤー": return "無効";
                case "難易度レベル": return "1.00";
                case "難易度上書き": return "0.00（無効）";
                case "経験値倍率": return "1.00";
                case "テイム速度倍率": return "1.00";
                case "採取量倍率": return "1.00";
                case "資源再生成間隔": return "1.00";
                case "野生恐竜の数": return "1.00";
                case "昼夜周期速度": return "1.00";
                case "昼の経過速度": return "1.00";
                case "夜の経過速度": return "1.00";
                case "交配間隔倍率": return "1.00";
                case "孵化速度倍率": return "1.00";
                case "成長速度倍率": return "1.00";
                case "建築物周辺の資源再生成範囲": return "1.00";
                case "建造物の設置コリジョン": return "重なり不可";
                case "低温クールダウン／低体温症": return "有効（PvP）／無効（PvE）";
                case "画面表示": return "クロスヘア非表示";
                case "マップ表示": return "現在位置非表示";
                case "カメラ": return "三人称無効";
                case "飛行生物": return "PvE運搬不可";
                default: return "";
            }
        }

        private void FillSettings()
        {
            rootBox.Text = settings.ServerRoot; mapBox.Text = settings.MapName; sessionBox.Text = settings.SessionName;
            maxPlayersBox.Value = Clamp(settings.MaxPlayers, maxPlayersBox); gamePortBox.Value = Clamp(settings.GamePort, gamePortBox);
            queryPortBox.Value = Clamp(settings.QueryPort, queryPortBox); rconPortBox.Value = Clamp(settings.RconPort, rconPortBox);
            serverPasswordBox.Text = settings.ServerPassword; adminPasswordBox.Text = settings.AdminPassword;
            extraArgsBox.Text = settings.AdditionalArguments; autoRestartBox.Checked = settings.AutoRestart;
            restartDelayBox.Value = Clamp(settings.RestartDelaySeconds, restartDelayBox);
            serverModeBox.SelectedIndex = settings.ServerPVE ? 1 : 0;
            singleplayerSettingsBox.Checked = settings.UseSingleplayerSettings;
            difficultyBox.Value = Clamp(settings.DifficultyOffset, difficultyBox);
            overrideDifficultyBox.Value = Clamp(settings.OverrideOfficialDifficulty, overrideDifficultyBox);
            xpMultiplierBox.Value = Clamp(settings.XPMultiplier, xpMultiplierBox);
            tamingMultiplierBox.Value = Clamp(settings.TamingSpeedMultiplier, tamingMultiplierBox);
            harvestMultiplierBox.Value = Clamp(settings.HarvestAmountMultiplier, harvestMultiplierBox);
            resourceRespawnBox.Value = Clamp(settings.ResourcesRespawnPeriodMultiplier, resourceRespawnBox);
            dinoCountBox.Value = Clamp(settings.DinoCountMultiplier, dinoCountBox);
            dayCycleBox.Value = Clamp(settings.DayCycleSpeedScale, dayCycleBox);
            dayTimeBox.Value = Clamp(settings.DayTimeSpeedScale, dayTimeBox);
            nightTimeBox.Value = Clamp(settings.NightTimeSpeedScale, nightTimeBox);
            matingIntervalBox.Value = Clamp(settings.MatingIntervalMultiplier, matingIntervalBox);
            eggHatchBox.Value = Clamp(settings.EggHatchSpeedMultiplier, eggHatchBox);
            babyMatureBox.Value = Clamp(settings.BabyMatureSpeedMultiplier, babyMatureBox);
            thirdPersonBox.Checked = settings.AllowThirdPersonPlayer;
            crosshairBox.Checked = settings.ServerCrosshair;
            mapLocationBox.Checked = settings.ShowMapPlayerLocation;
            flyerCarryBox.Checked = settings.AllowFlyerCarryPVE;
            structureCollisionBox.Checked = settings.DisableStructurePlacementCollision;
            structureResourceRadiusBox.Value = Clamp(settings.ResourceNoReplenishRadiusStructures, structureResourceRadiusBox);
            cryopodCooldownBox.Checked = settings.DisableCryopodCooldown;
            SyncScheduleControls();
            UpdateScheduleStatus();
        }

        private decimal Clamp(int n, NumericUpDown box)
        {
            return Math.Min(box.Maximum, Math.Max(box.Minimum, n));
        }

        private decimal Clamp(double n, NumericUpDown box)
        {
            decimal value;
            try { value = (decimal)n; } catch { value = box.Minimum; }
            return Math.Min(box.Maximum, Math.Max(box.Minimum, value));
        }

        private void BrowseRoot(object sender, EventArgs e)
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = "ARKサーバーのルートフォルダーを選択"; d.SelectedPath = rootBox.Text;
                if (d.ShowDialog(this) == DialogResult.OK) rootBox.Text = d.SelectedPath;
            }
        }

        private bool PullSettings(bool showErrors)
        {
            string root = rootBox.Text.Trim();
            string exe = Path.Combine(root, @"ShooterGame\Binaries\Win64\ShooterGameServer.exe");
            if (!File.Exists(exe))
            {
                if (showErrors) MessageBox.Show("指定先に ShooterGameServer.exe が見つかりません。", "設定を確認", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (String.IsNullOrWhiteSpace(mapBox.Text) || String.IsNullOrWhiteSpace(sessionBox.Text))
            {
                if (showErrors) MessageBox.Show("マップ名とサーバー名を入力してください。", "設定を確認", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            int[] ports = { (int)gamePortBox.Value, (int)queryPortBox.Value, (int)rconPortBox.Value };
            if (ports.Distinct().Count() != ports.Length)
            {
                if (showErrors) MessageBox.Show("ゲーム・Query・RCONポートは別々の番号にしてください。", "設定を確認", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            settings.ServerRoot = root; settings.MapName = mapBox.Text.Trim(); settings.SessionName = sessionBox.Text.Trim();
            settings.MaxPlayers = (int)maxPlayersBox.Value; settings.GamePort = ports[0]; settings.QueryPort = ports[1]; settings.RconPort = ports[2];
            settings.ServerPassword = serverPasswordBox.Text; settings.AdminPassword = adminPasswordBox.Text;
            settings.AdditionalArguments = extraArgsBox.Text.Trim(); settings.AutoRestart = autoRestartBox.Checked;
            settings.RestartDelaySeconds = (int)restartDelayBox.Value;
            settings.ServerPVE = serverModeBox.SelectedIndex == 1;
            settings.UseSingleplayerSettings = singleplayerSettingsBox.Checked;
            settings.DifficultyOffset = (double)difficultyBox.Value;
            settings.OverrideOfficialDifficulty = (double)overrideDifficultyBox.Value;
            settings.XPMultiplier = (double)xpMultiplierBox.Value;
            settings.TamingSpeedMultiplier = (double)tamingMultiplierBox.Value;
            settings.HarvestAmountMultiplier = (double)harvestMultiplierBox.Value;
            settings.ResourcesRespawnPeriodMultiplier = (double)resourceRespawnBox.Value;
            settings.DinoCountMultiplier = (double)dinoCountBox.Value;
            settings.DayCycleSpeedScale = (double)dayCycleBox.Value;
            settings.DayTimeSpeedScale = (double)dayTimeBox.Value;
            settings.NightTimeSpeedScale = (double)nightTimeBox.Value;
            settings.MatingIntervalMultiplier = (double)matingIntervalBox.Value;
            settings.EggHatchSpeedMultiplier = (double)eggHatchBox.Value;
            settings.BabyMatureSpeedMultiplier = (double)babyMatureBox.Value;
            settings.AllowThirdPersonPlayer = thirdPersonBox.Checked;
            settings.ServerCrosshair = crosshairBox.Checked;
            settings.ShowMapPlayerLocation = mapLocationBox.Checked;
            settings.AllowFlyerCarryPVE = flyerCarryBox.Checked;
            settings.DisableStructurePlacementCollision = structureCollisionBox.Checked;
            settings.ResourceNoReplenishRadiusStructures = (double)structureResourceRadiusBox.Value;
            settings.DisableCryopodCooldown = cryopodCooldownBox.Checked;
            return true;
        }

        private void SaveSettings(object sender, EventArgs e)
        {
            if (!PullSettings(true)) return;
            try
            {
                settings.Save(); saveNotice.Text = "保存しました  " + DateTime.Now.ToString("HH:mm:ss");
                gameplaySaveNotice.Text = "保存しました  " + DateTime.Now.ToString("HH:mm:ss");
                statusDetail.Text = settings.MapName + "  •  " + settings.SessionName;
                ConfigureLogWatcher();
            }
            catch (Exception ex) { MessageBox.Show("設定を保存できませんでした。\n\n" + ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private string BuildArguments()
        {
            List<string> options = new List<string>();
            options.Add("SessionName=" + CleanUrlValue(settings.SessionName));
            if (!String.IsNullOrEmpty(settings.ServerPassword)) options.Add("ServerPassword=" + CleanUrlValue(settings.ServerPassword));
            if (!String.IsNullOrEmpty(settings.AdminPassword)) options.Add("ServerAdminPassword=" + CleanUrlValue(settings.AdminPassword));
            options.Add("MaxPlayers=" + settings.MaxPlayers.ToString(CultureInfo.InvariantCulture));
            options.Add("Port=" + settings.GamePort.ToString(CultureInfo.InvariantCulture));
            options.Add("QueryPort=" + settings.QueryPort.ToString(CultureInfo.InvariantCulture));
            options.Add("RCONEnabled=True");
            options.Add("RCONPort=" + settings.RconPort.ToString(CultureInfo.InvariantCulture));
            options.Add("ServerPVE=" + BoolText(settings.ServerPVE));
            options.Add("bUseSingleplayerSettings=" + BoolText(settings.UseSingleplayerSettings));
            options.Add("DifficultyOffset=" + FloatText(settings.DifficultyOffset));
            if (settings.OverrideOfficialDifficulty > 0) options.Add("OverrideOfficialDifficulty=" + FloatText(settings.OverrideOfficialDifficulty));
            options.Add("XPMultiplier=" + FloatText(settings.XPMultiplier));
            options.Add("TamingSpeedMultiplier=" + FloatText(settings.TamingSpeedMultiplier));
            options.Add("HarvestAmountMultiplier=" + FloatText(settings.HarvestAmountMultiplier));
            options.Add("ResourcesRespawnPeriodMultiplier=" + FloatText(settings.ResourcesRespawnPeriodMultiplier));
            options.Add("DinoCountMultiplier=" + FloatText(settings.DinoCountMultiplier));
            options.Add("DayCycleSpeedScale=" + FloatText(settings.DayCycleSpeedScale));
            options.Add("DayTimeSpeedScale=" + FloatText(settings.DayTimeSpeedScale));
            options.Add("NightTimeSpeedScale=" + FloatText(settings.NightTimeSpeedScale));
            options.Add("MatingIntervalMultiplier=" + FloatText(settings.MatingIntervalMultiplier));
            options.Add("EggHatchSpeedMultiplier=" + FloatText(settings.EggHatchSpeedMultiplier));
            options.Add("BabyMatureSpeedMultiplier=" + FloatText(settings.BabyMatureSpeedMultiplier));
            options.Add("AllowThirdPersonPlayer=" + BoolText(settings.AllowThirdPersonPlayer));
            options.Add("ServerCrosshair=" + BoolText(settings.ServerCrosshair));
            options.Add("ShowMapPlayerLocation=" + BoolText(settings.ShowMapPlayerLocation));
            options.Add("AllowFlyerCarryPvE=" + BoolText(settings.AllowFlyerCarryPVE));
            options.Add("bDisableStructurePlacementCollision=" + BoolText(settings.DisableStructurePlacementCollision));
            options.Add("ResourceNoReplenishRadiusStructures=" + FloatText(settings.ResourceNoReplenishRadiusStructures));
            Dictionary<string, string> cryopodValues = BuildCryopodIniValues(settings.DisableCryopodCooldown);
            foreach (KeyValuePair<string, string> cryopodValue in cryopodValues)
                options.Add(cryopodValue.Key + "=" + cryopodValue.Value);
            options.Add("listen");
            string url = settings.MapName + "?" + String.Join("?", options.ToArray());
            return "\"" + url.Replace("\"", "") + "\" " + settings.AdditionalArguments;
        }

        private string BoolText(bool value)
        {
            return value ? "True" : "False";
        }

        private string FloatText(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private void ApplyRequiredIniSettings()
        {
            string gameIni = Path.Combine(settings.ConfigDirectory, "Game.ini");
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            values["bDisableStructurePlacementCollision"] = BoolText(settings.DisableStructurePlacementCollision);
            values["ResourceNoReplenishRadiusStructures"] = FloatText(settings.ResourceNoReplenishRadiusStructures);
            UpdateIniSection(gameIni, "/script/shootergame.shootergamemode", values);

            string gameUserSettings = Path.Combine(settings.ConfigDirectory, "GameUserSettings.ini");
            Dictionary<string, string> serverValues = BuildCryopodIniValues(settings.DisableCryopodCooldown);
            UpdateIniSection(gameUserSettings, "ServerSettings", serverValues);
        }

        private static Dictionary<string, string> BuildCryopodIniValues(bool disabled)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            values["EnableCryopodNerf"] = disabled ? "True" : "False";
            values["EnableCryoSicknessPVE"] = disabled ? "False" : "True";
            values["CryopodNerfDuration"] = disabled ? "0" : "10";
            values["CryopodNerfDamageMult"] = disabled ? "0" : "0.01";
            values["CryopodNerfIncomingDamageMultPercent"] = disabled ? "0" : "0.25";
            return values;
        }

        private void UpdateIniSection(string path, string sectionName, Dictionary<string, string> values)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            List<string> lines = File.Exists(path) ? new List<string>(File.ReadAllLines(path)) : new List<string>();
            int sectionStart = -1;
            int sectionEnd = lines.Count;
            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    string current = trimmed.Substring(1, trimmed.Length - 2);
                    if (sectionStart >= 0) { sectionEnd = i; break; }
                    if (current.Equals(sectionName, StringComparison.OrdinalIgnoreCase)) sectionStart = i;
                }
            }
            if (sectionStart < 0)
            {
                if (lines.Count > 0 && lines[lines.Count - 1].Length > 0) lines.Add("");
                lines.Add("[" + sectionName + "]");
                sectionStart = lines.Count - 1;
                sectionEnd = lines.Count;
            }

            HashSet<string> written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = sectionStart + 1; i < sectionEnd; i++)
            {
                int equals = lines[i].IndexOf('=');
                if (equals <= 0) continue;
                string key = lines[i].Substring(0, equals).Trim();
                string value;
                if (!values.TryGetValue(key, out value)) continue;
                if (written.Contains(key))
                {
                    lines.RemoveAt(i); i--; sectionEnd--; continue;
                }
                lines[i] = key + "=" + value;
                written.Add(key);
            }
            foreach (KeyValuePair<string, string> item in values)
            {
                if (!written.Contains(item.Key)) { lines.Insert(sectionEnd, item.Key + "=" + item.Value); sectionEnd++; }
            }

            if (File.Exists(path) && !File.Exists(path + ".arkmanager.bak")) File.Copy(path, path + ".arkmanager.bak", false);
            string temporary = path + ".arkmanager.tmp";
            File.WriteAllLines(temporary, lines.ToArray(), new UTF8Encoding(false));
            File.Copy(temporary, path, true);
            File.Delete(temporary);
        }

        private string CleanUrlValue(string value)
        {
            return (value ?? "").Replace("?", "").Replace("\"", "").Replace("\r", " ").Replace("\n", " ");
        }

        private void SaveSchedule(object sender, EventArgs e)
        {
            RemoteScheduleState request = new RemoteScheduleState
            {
                OneTimeStartEnabled = scheduledStartEnabledBox.Checked,
                OneTimeStartAt = ScheduleLogic.CombineDateAndTime(scheduledStartPicker.Value, scheduledStartTimePicker.Value).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
                OneTimeStopEnabled = scheduledStopEnabledBox.Checked,
                OneTimeStopAt = ScheduleLogic.CombineDateAndTime(scheduledStopPicker.Value, scheduledStopTimePicker.Value).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
                DailyStartEnabled = dailyStartEnabledBox.Checked,
                DailyStartTime = ScheduleLogic.ToTime(dailyStartPicker.Value).ToString(@"hh\:mm", CultureInfo.InvariantCulture),
                DailyStopEnabled = dailyStopEnabledBox.Checked,
                DailyStopTime = ScheduleLogic.ToTime(dailyStopPicker.Value).ToString(@"hh\:mm", CultureInfo.InvariantCulture)
            };
            string result = ApplyScheduleSettings(request);
            if (result.StartsWith("エラー:", StringComparison.Ordinal))
                MessageBox.Show(result.Substring(4).Trim(), "日時設定", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ClearSchedule(object sender, EventArgs e)
        {
            string result = ClearAllSchedules("すべての予約を解除しました");
            if (result.StartsWith("エラー:", StringComparison.Ordinal))
                MessageBox.Show(result.Substring(4).Trim(), "日時設定", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private string ClearAllSchedules(string actionMessage)
        {
            ScheduleLogic.Clear(settings);
            SyncScheduleControls();
            scheduleLastAction = actionMessage;
            try { settings.Save(); }
            catch (Exception ex) { return "エラー: 予約を解除できませんでした。 " + ex.Message; }
            UpdateScheduleStatus();
            return "すべての日時設定を解除しました。";
        }

        private void SyncScheduleControls()
        {
            if (scheduledStartEnabledBox == null) return;
            scheduledStartEnabledBox.Checked = settings.ScheduledStartEnabled;
            scheduledStartPicker.Value = settings.ScheduledStartAt;
            scheduledStartTimePicker.Value = settings.ScheduledStartAt;
            scheduledStopEnabledBox.Checked = settings.ScheduledStopEnabled;
            scheduledStopPicker.Value = settings.ScheduledStopAt;
            scheduledStopTimePicker.Value = settings.ScheduledStopAt;
            dailyStartEnabledBox.Checked = settings.DailyStartEnabled;
            dailyStartPicker.Value = DateTime.Today.Add(settings.DailyStartTime);
            dailyStopEnabledBox.Checked = settings.DailyStopEnabled;
            dailyStopPicker.Value = DateTime.Today.Add(settings.DailyStopTime);
        }

        private string ApplyScheduleSettings(RemoteScheduleState request)
        {
            string error;
            if (!ScheduleLogic.TryApply(settings, request, DateTime.Now, out error)) return "エラー: " + error;
            scheduleLastAction = "";
            try { settings.Save(); }
            catch (Exception ex) { return "エラー: 予約を保存できませんでした。 " + ex.Message; }
            SyncScheduleControls();
            UpdateScheduleStatus();
            return "日時設定を保存しました。 " + GetScheduleSummary();
        }

        private void UpdateScheduleStatus()
        {
            if (scheduleStatusLabel == null) return;
            scheduleStatusLabel.Text = GetScheduleSummary();
            scheduleStatusLabel.ForeColor = ScheduleLogic.HasActive(settings) ? Amber : Muted;
        }

        private string GetScheduleSummary()
        {
            return ScheduleLogic.Summary(settings, DateTime.Now, scheduleLastAction);
        }

        private bool CheckScheduledActions(bool running)
        {
            if (stopping) return false;
            ScheduleDecision decision = ScheduleLogic.ConsumeDue(settings, DateTime.Now, running && launchPending);
            if (!decision.HasAction || (decision.Action == ScheduledAction.Stop && running && launchPending))
            {
                UpdateScheduleStatus();
                return false;
            }
            SyncScheduleControls();
            try { settings.Save(); } catch { }

            if (decision.Action == ScheduledAction.Start)
            {
                if (running)
                {
                    scheduleLastAction = (decision.IsDaily ? "定時起動" : "起動予約") + "時刻になりました（すでに稼働中）";
                    UpdateScheduleStatus();
                    return false;
                }
                scheduleLastAction = (decision.IsDaily ? "定時起動" : "起動予約") + "を実行しました";
                UpdateScheduleStatus();
                StartServer(this, EventArgs.Empty);
                return true;
            }

            if (!running)
            {
                scheduleLastAction = (decision.IsDaily ? "定時停止" : "停止予約") + "時刻になりました（すでに停止中）";
                UpdateScheduleStatus();
                return false;
            }
            scheduleLastAction = (decision.IsDaily ? "定時停止" : "停止予約") + "を実行しています";
            UpdateScheduleStatus();
            StopServer(this, EventArgs.Empty);
            return true;
        }

        private void StartServer(object sender, EventArgs e)
        {
            DiscoverProcess();
            if (IsRunning()) { MessageBox.Show("サーバーはすでに起動しています。", "ARK Server Manager", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (!PullSettings(true)) { tabs.SelectedIndex = 1; return; }
            settings.Save();
            try
            {
                ApplyRequiredIniSettings();
                ProcessStartInfo psi = new ProcessStartInfo(settings.ExecutablePath, BuildArguments());
                psi.WorkingDirectory = Path.GetDirectoryName(settings.ExecutablePath);
                psi.UseShellExecute = true;
                psi.WindowStyle = ProcessWindowStyle.Minimized;
                serverProcess = Process.Start(psi);
                deliberateStop = false; stopping = false; observedRunning = true; launchPending = true; restartAt = null; serverStartedAt = DateTime.Now;
                ResetCpuSample(); RefreshMonitor();
            }
            catch (Exception ex) { MessageBox.Show("サーバーを起動できませんでした。\n\n" + ex.Message, "起動エラー", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void StopServer(object sender, EventArgs e)
        {
            DiscoverProcess();
            if (!IsRunning()) { MessageBox.Show("サーバーは停止しています。", "ARK Server Manager", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            deliberateStop = true; stopping = true; restartAt = null; stopButton.Enabled = false; startButton.Enabled = false;
            SetStatus("停止処理中", "ワールドを保存してから終了します…", Amber);
            Process target = serverProcess;
            Task.Factory.StartNew(delegate
            {
                string error = null;
                try
                {
                    using (RconClient rcon = new RconClient("127.0.0.1", settings.RconPort, settings.AdminPassword))
                    {
                        rcon.Connect(); rcon.Command("SaveWorld"); Thread.Sleep(1800); rcon.Command("DoExit");
                    }
                    if (!target.WaitForExit(30000)) error = "RCON終了コマンド後もサーバーが動作しています。";
                }
                catch (Exception ex) { error = "正常終了を確認できませんでした。\n\n" + ex.Message; }
                return error;
            }).ContinueWith(delegate(Task<string> task)
            {
                BeginInvoke((Action)delegate
                {
                    string error = task.Result;
                    if (error != null && IsProcessAlive(target))
                    {
                        DialogResult answer = MessageBox.Show(error + "\n\n強制終了しますか？\n（直前のセーブが反映されない可能性があります）", "停止の確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                        if (answer == DialogResult.Yes)
                        {
                            try { target.Kill(); target.WaitForExit(5000); } catch (Exception ex) { MessageBox.Show(ex.Message, "強制終了エラー", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                        }
                    }
                    stopping = false; DiscoverProcess(); RefreshMonitor();
                });
            });
        }

        private void RefreshMonitor()
        {
            RetryRemoteTailnetBinding();
            bool wasRunning = serverProcess != null && IsProcessAlive(serverProcess);
            if (!wasRunning) DiscoverProcess();
            bool running = IsRunning();

            if (CheckScheduledActions(running)) return;

            if (!running && observedRunning && !deliberateStop && settings.AutoRestart && !restartAt.HasValue)
                restartAt = DateTime.Now.AddSeconds(settings.RestartDelaySeconds);
            if (!running && restartAt.HasValue && DateTime.Now >= restartAt.Value)
            {
                restartAt = null; StartServer(this, EventArgs.Empty); running = IsRunning();
            }

            if (running)
            {
                observedRunning = true;
                if (!stopping) deliberateStop = false;
                try { serverProcess.Refresh(); } catch { }
                HashSet<int> udpPorts = GetActiveUdpPorts();
                if (launchPending && IsServerReady()) launchPending = false;
                string ports = GetPortStatus(udpPorts);
                TimeSpan up = DateTime.Now - serverStartedAt;
                try { up = DateTime.Now - serverProcess.StartTime; } catch { }
                if (stopping) SetStatus("停止処理中", "ワールドを保存してから終了します…", Amber);
                else if (launchPending)
                {
                    string note = up.TotalMinutes >= 10 ? "起動に時間がかかっています • ログを確認してください" : "ShooterGameServer 起動済み • 起動完了ログ待ち " + FormatDuration(up);
                    SetStatus("起動中", note, Amber);
                }
                else SetStatus("稼働中", settings.MapName + "  •  " + settings.SessionName + "  •  PID " + serverProcess.Id, Green);
                uptimeValue.Text = FormatDuration(up);
                cpuValue.Text = MeasureCpu();
                memoryValue.Text = GetMemoryDisplay();
                portValue.Text = ports;
            }
            else
            {
                launchPending = false;
                string detail = restartAt.HasValue ? "異常終了を検知 • " + Math.Max(0, (int)(restartAt.Value - DateTime.Now).TotalSeconds) + "秒後に再起動" : settings.MapName + "  •  " + settings.SessionName;
                SetStatus(restartAt.HasValue ? "再起動待機中" : "停止中", detail, restartAt.HasValue ? Amber : Muted);
                uptimeValue.Text = "—"; cpuValue.Text = "—"; memoryValue.Text = "—"; portValue.Text = settings.GamePort.ToString();
            }
            startButton.Enabled = !running && !stopping; stopButton.Enabled = running && !stopping;
            UpdateLiveControlAvailability(running && !launchPending && !stopping);
            if ((DateTime.Now - lastLogRead).TotalSeconds >= 1) ReadLog(false);
        }

        private void SetStatus(string title, string detail, Color color)
        {
            statusDot.ForeColor = color; statusTitle.Text = title; statusDetail.Text = detail;
        }

        private void DiscoverProcess()
        {
            if (IsRunning()) return;
            serverProcess = null;
            foreach (Process p in Process.GetProcessesByName("ShooterGameServer"))
            {
                try
                {
                    if (String.Equals(p.MainModule.FileName, settings.ExecutablePath, StringComparison.OrdinalIgnoreCase)) { serverProcess = p; break; }
                }
                catch { if (serverProcess == null) serverProcess = p; }
            }
            if (serverProcess != null)
            {
                try { serverStartedAt = serverProcess.StartTime; } catch { serverStartedAt = DateTime.Now; }
                launchPending = !IsServerReady();
                ResetCpuSample();
            }
        }

        private bool IsRunning() { return serverProcess != null && IsProcessAlive(serverProcess); }
        private bool IsProcessAlive(Process p)
        {
            if (p == null) return false;
            try { return !p.HasExited; } catch { return false; }
        }

        private void ResetCpuSample()
        {
            if (!IsRunning()) return;
            try { lastCpu = serverProcess.TotalProcessorTime; lastCpuAt = DateTime.UtcNow; } catch { }
        }

        private string MeasureCpu()
        {
            try
            {
                DateTime now = DateTime.UtcNow; TimeSpan cpu = serverProcess.TotalProcessorTime;
                double elapsed = (now - lastCpuAt).TotalMilliseconds;
                double value = elapsed <= 0 ? 0 : (cpu - lastCpu).TotalMilliseconds / elapsed / Environment.ProcessorCount * 100d;
                lastCpu = cpu; lastCpuAt = now; return Math.Max(0, value).ToString("0.0") + "%";
            }
            catch { return "—"; }
        }

        private HashSet<int> GetActiveUdpPorts()
        {
            try
            {
                return new HashSet<int>(IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners().Select(delegate(IPEndPoint e) { return e.Port; }));
            }
            catch { return new HashSet<int>(); }
        }

        private string GetPortStatus(HashSet<int> ports)
        {
            return settings.GamePort + (ports.Contains(settings.GamePort) ? "  ✓" : "  …");
        }

        private bool IsServerReady()
        {
            try
            {
                string consoleText = ReadServerConsoleTail();
                if (ContainsCurrentProcessSuccessfulStart(consoleText)) return true;

                FileInfo log = new FileInfo(settings.LogPath);
                if (!log.Exists || log.LastWriteTime < serverStartedAt.AddSeconds(-3)) return false;
                using (FileStream fs = new FileStream(log.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    using (StreamReader reader = new StreamReader(fs, Encoding.UTF8, true))
                    {
                        char[] buffer = new char[1024 * 1024];
                        int length = reader.ReadBlock(buffer, 0, buffer.Length);
                        string text = new string(buffer, 0, length);
                        return ContainsSuccessfulStartMarker(text, serverStartedAt);
                    }
                }
            }
            catch { return false; }
        }

        private static bool ContainsCurrentProcessSuccessfulStart(string text)
        {
            return !String.IsNullOrEmpty(text) && Regex.IsMatch(text,
                "Server:\\s*\"[^\"\\r\\n]+\"\\s*has successfully started\\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static bool ContainsSuccessfulStartMarker(string text, DateTime processStartedAt)
        {
            if (String.IsNullOrEmpty(text)) return false;
            string namePattern = "[^\"\\r\\n]+";
            string pattern = "\\[(?<stamp>\\d{4}\\.\\d{2}\\.\\d{2}-\\d{2}\\.\\d{2}\\.\\d{2}:\\d{3})\\][^\\r\\n]*Server:\\s*\"" +
                namePattern + "\"\\s*has successfully started\\b";
            MatchCollection matches = Regex.Matches(text, pattern,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            DateTime processUtc = processStartedAt.ToUniversalTime().AddSeconds(-15);
            foreach (Match match in matches)
            {
                DateTime markerUtc;
                if (DateTime.TryParseExact(match.Groups["stamp"].Value,
                    "yyyy.MM.dd-HH.mm.ss:fff", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out markerUtc) &&
                    markerUtc >= processUtc)
                    return true;
            }
            return false;
        }

        private string GetMemoryDisplay()
        {
            try
            {
                serverProcess.Refresh();
                long bytes = serverProcess.WorkingSet64;
                ulong total = GetTotalPhysicalMemory();
                double gb = bytes / 1024d / 1024d / 1024d;
                if (total == 0) return gb.ToString("0.0") + " GB";
                double percent = bytes / (double)total * 100d;
                return gb.ToString("0.0") + " GB  (" + percent.ToString("0.0") + "%)";
            }
            catch { return "—"; }
        }

        private static ulong GetTotalPhysicalMemory()
        {
            MemoryStatus status = new MemoryStatus();
            status.Length = (uint)Marshal.SizeOf(typeof(MemoryStatus));
            return GlobalMemoryStatusEx(ref status) ? status.TotalPhysical : 0;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MemoryStatus
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhysical;
            public ulong AvailablePhysical;
            public ulong TotalPageFile;
            public ulong AvailablePageFile;
            public ulong TotalVirtual;
            public ulong AvailableVirtual;
            public ulong AvailableExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

        private const int WM_SETREDRAW = 0x000B;
        private const int EM_GETSCROLLPOS = 0x04DD;
        private const int EM_SETSCROLLPOS = 0x04DE;
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, ref Point lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct ConsoleCoordinate
        {
            public short X;
            public short Y;
            public ConsoleCoordinate(short x, short y) { X = x; Y = y; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ConsoleRectangle
        {
            public short Left;
            public short Top;
            public short Right;
            public short Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ConsoleBufferInfo
        {
            public ConsoleCoordinate Size;
            public ConsoleCoordinate CursorPosition;
            public ushort Attributes;
            public ConsoleRectangle Window;
            public ConsoleCoordinate MaximumWindowSize;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint processId);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int standardHandle);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleScreenBufferInfo(IntPtr consoleOutput, out ConsoleBufferInfo info);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ReadConsoleOutputCharacter(IntPtr consoleOutput, StringBuilder text, int length, ConsoleCoordinate readCoordinate, out int charactersRead);

        private string FormatDuration(TimeSpan t)
        {
            if (t.TotalDays >= 1) return ((int)t.TotalDays) + "日 " + t.Hours.ToString("00") + ":" + t.Minutes.ToString("00");
            return ((int)t.TotalHours).ToString("00") + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00");
        }

        private void ReadLog(bool force)
        {
            lastLogRead = DateTime.Now;
            if (IsRunning())
            {
                string consoleText = ReadServerConsoleTail();
                if (!String.IsNullOrWhiteSpace(consoleText))
                {
                    UpdateLogText(consoleText, force);
                    return;
                }
            }
            if (!File.Exists(settings.LogPath)) { if (force) logBox.Text = "ログファイルはまだありません。"; return; }
            try
            {
                using (FileStream fs = new FileStream(settings.LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    long start = Math.Max(0, fs.Length - 160000); fs.Seek(start, SeekOrigin.Begin);
                    using (StreamReader sr = new StreamReader(fs, Encoding.UTF8, true))
                    {
                        if (start > 0) sr.ReadLine();
                        string text = sr.ReadToEnd();
                        UpdateLogText(text, force);
                    }
                }
            }
            catch (Exception ex) { if (force) logBox.Text = "ログを読めませんでした: " + ex.Message; }
        }

        private void UpdateLogText(string text, bool force)
        {
            if (text == logBox.Text) return;
            bool atBottom = logBox.SelectionStart >= Math.Max(0, logBox.TextLength - 5);
            logBox.Text = text;
            if (atBottom || force) { logBox.SelectionStart = logBox.TextLength; logBox.ScrollToCaret(); }
        }

        private void ConfigureLogWatcher()
        {
            if (logWatcher != null) { logWatcher.EnableRaisingEvents = false; logWatcher.Dispose(); logWatcher = null; }
            string directory = Path.GetDirectoryName(settings.LogPath);
            if (!Directory.Exists(directory)) return;
            try
            {
                logWatcher = new FileSystemWatcher(directory, Path.GetFileName(settings.LogPath));
                logWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
                logWatcher.Changed += LogFileChanged;
                logWatcher.Created += LogFileChanged;
                logWatcher.Renamed += LogFileChanged;
                logWatcher.EnableRaisingEvents = true;
            }
            catch { if (logWatcher != null) { logWatcher.Dispose(); logWatcher = null; } }
        }

        private void LogFileChanged(object sender, FileSystemEventArgs e)
        {
            if (logReadQueued || IsDisposed || !IsHandleCreated) return;
            logReadQueued = true;
            try
            {
                BeginInvoke((Action)delegate
                {
                    logReadQueued = false;
                    ReadLog(false);
                });
            }
            catch { logReadQueued = false; }
        }

        private void OpenPath(string path)
        {
            if (!Directory.Exists(path)) { MessageBox.Show("フォルダーが見つかりません。\n" + path, "確認", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
        }

        private sealed class ResourceProbeResult
        {
            public readonly bool Success;
            public readonly string Response;
            public readonly string Error;
            public readonly double AgeSeconds;
            public readonly bool ScanningEnabled;
            private ResourceProbeResult(bool success, string response, string error, double ageSeconds, bool scanningEnabled)
            {
                Success = success; Response = response ?? ""; Error = error ?? ""; AgeSeconds = ageSeconds; ScanningEnabled = scanningEnabled;
            }
            public static ResourceProbeResult Ok(string response, double ageSeconds, bool scanningEnabled) { return new ResourceProbeResult(true, response, "", ageSeconds, scanningEnabled); }
            public static ResourceProbeResult Fail(string error) { return new ResourceProbeResult(false, "", error, 0, false); }
        }

        private sealed class ResourceZoneSnapshot
        {
            public readonly string Id;
            public readonly string Name;
            public readonly string Realm;
            public readonly double Latitude;
            public readonly double Longitude;
            public readonly double RadiusGps;
            public readonly int ExpectedMask;
            public bool Scanned;
            public int Metal;
            public int Crystal;
            public int Obsidian;
            public int MetalHealth;
            public int CrystalHealth;
            public int ObsidianHealth;
            public ResourceZoneSnapshot(string id, string name, string realm, double latitude, double longitude, double radiusGps, int expectedMask)
            {
                Id = id; Name = name; Realm = realm; Latitude = latitude; Longitude = longitude;
                RadiusGps = radiusGps; ExpectedMask = expectedMask;
            }
        }

        private sealed class ResourceMapCanvas : Control
        {
            private static readonly double[] LongitudeGridPixels = { 42, 159, 276, 392, 508, 622, 739, 853, 971, 1107, 1243 };
            private static readonly double[] LatitudeGridPixels = { 27, 139, 251, 363, 476, 589, 702, 815, 927, 1040, 1153 };
            private List<ResourceZoneSnapshot> zones;
            private Image fjordurMap;
            private int resourceFilter;
            private int selectedZoneIndex;
            public event EventHandler SelectedZoneChanged;

            public ResourceMapCanvas(IList<ResourceZoneSnapshot> source)
            {
                zones = new List<ResourceZoneSnapshot>(source ?? new List<ResourceZoneSnapshot>());
                selectedZoneIndex = zones.Count > 0 ? 0 : -1;
                DoubleBuffered = true;
                ResizeRedraw = true;
                BackColor = Color.FromArgb(11, 16, 22);
                Cursor = Cursors.Hand;
                try
                {
                    using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ArkServerManager.FjordurMap.png"))
                    using (Image sourceImage = stream == null ? null : Image.FromStream(stream))
                        if (sourceImage != null) fjordurMap = new Bitmap(sourceImage);
                }
                catch { fjordurMap = null; }
            }

            public int ResourceFilter
            {
                get { return resourceFilter; }
                set { resourceFilter = Math.Max(0, Math.Min(3, value)); Invalidate(); }
            }

            public int SelectedZoneIndex
            {
                get { return selectedZoneIndex; }
                set
                {
                    int next = zones.Count == 0 ? -1 : Math.Max(0, Math.Min(zones.Count - 1, value));
                    if (next == selectedZoneIndex) return;
                    selectedZoneIndex = next;
                    Invalidate();
                    EventHandler handler = SelectedZoneChanged;
                    if (handler != null) handler(this, EventArgs.Empty);
                }
            }

            public void SetSnapshots(IList<ResourceZoneSnapshot> source)
            {
                zones = new List<ResourceZoneSnapshot>(source ?? new List<ResourceZoneSnapshot>());
                if (selectedZoneIndex >= zones.Count) selectedZoneIndex = zones.Count - 1;
                if (selectedZoneIndex < 0 && zones.Count > 0) selectedZoneIndex = 0;
                Invalidate();
            }

            private int FilterMask { get { return resourceFilter == 1 ? 1 : resourceFilter == 2 ? 2 : resourceFilter == 3 ? 4 : 0; } }

            private RectangleF GetMapRectangle()
            {
                float availableWidth = Math.Max(120, Width - 26);
                float availableHeight = Math.Max(120, Height - 104);
                float size = Math.Min(availableWidth, availableHeight);
                return new RectangleF((Width - size) / 2F, 32, size, size);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                RectangleF map = GetMapRectangle();
                using (SolidBrush ocean = new SolidBrush(Color.FromArgb(20, 50, 64))) e.Graphics.FillRectangle(ocean, map);
                if (fjordurMap != null) e.Graphics.DrawImage(fjordurMap, map);
                using (Pen border = new Pen(Color.FromArgb(150, 205, 216), 2F)) e.Graphics.DrawRectangle(border, map.X, map.Y, map.Width, map.Height);

                int mask = FilterMask;
                for (int i = 0; i < zones.Count; i++)
                    if (i != selectedZoneIndex && (mask == 0 || (zones[i].ExpectedMask & mask) != 0)) DrawZone(e.Graphics, map, i, false);
                if (selectedZoneIndex >= 0 && selectedZoneIndex < zones.Count && (mask == 0 || (zones[selectedZoneIndex].ExpectedMask & mask) != 0))
                    DrawZone(e.Graphics, map, selectedZoneIndex, true);
                DrawLegend(e.Graphics, map);
            }

            private Color ResourceColor(ResourceZoneSnapshot zone)
            {
                if (resourceFilter == 1) return Color.FromArgb(242, 164, 63);
                if (resourceFilter == 2) return Color.FromArgb(73, 205, 231);
                if (resourceFilter == 3) return Color.FromArgb(169, 103, 224);
                return zone.Scanned ? Color.FromArgb(53, 183, 144) : Color.FromArgb(145, 157, 170);
            }

            private void DrawZone(Graphics graphics, RectangleF map, int index, bool selected)
            {
                ResourceZoneSnapshot zone = zones[index];
                float x = MapX(map, zone.Longitude);
                float y = MapY(map, zone.Latitude);
                float rx = Math.Max(4F, Math.Abs(MapX(map, Math.Min(100, zone.Longitude + zone.RadiusGps)) - x));
                float ry = Math.Max(4F, Math.Abs(MapY(map, Math.Min(100, zone.Latitude + zone.RadiusGps)) - y));
                Color color = ResourceColor(zone);
                using (SolidBrush band = new SolidBrush(Color.FromArgb(selected ? 88 : 48, color)))
                    graphics.FillEllipse(band, x - rx, y - ry, rx * 2, ry * 2);
                using (Pen bandBorder = new Pen(selected ? Color.White : color, selected ? 3F : 1.5F))
                    graphics.DrawEllipse(bandBorder, x - rx, y - ry, rx * 2, ry * 2);
                float pinRadius = selected ? 11F : 7F;
                using (SolidBrush pin = new SolidBrush(color)) graphics.FillEllipse(pin, x - pinRadius, y - pinRadius, pinRadius * 2, pinRadius * 2);
                using (Pen pinBorder = new Pen(Color.White, selected ? 3F : 1.5F)) graphics.DrawEllipse(pinBorder, x - pinRadius, y - pinRadius, pinRadius * 2, pinRadius * 2);
                if (selected)
                {
                    using (Font font = new Font("Yu Gothic UI", 7.5F, FontStyle.Bold))
                    using (SolidBrush text = new SolidBrush(Color.White))
                    using (StringFormat center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        graphics.DrawString((index + 1).ToString(CultureInfo.InvariantCulture), font, text,
                            new RectangleF(x - pinRadius, y - pinRadius - 1, pinRadius * 2, pinRadius * 2), center);
                }
            }

            private void DrawLegend(Graphics graphics, RectangleF map)
            {
                if (selectedZoneIndex < 0 || selectedZoneIndex >= zones.Count) return;
                ResourceZoneSnapshot zone = zones[selectedZoneIndex];
                string counts = zone.Scanned
                    ? "金属岩 " + zone.Metal + "個　水晶岩 " + zone.Crystal + "個　黒曜石岩 " + zone.Obsidian + "個"
                    : "現存資源量：取得待ち";
                string title = "#" + (selectedZoneIndex + 1) + "  " + zone.Name + "（" + zone.Realm + "）";
                string gps = "中心 GPS 緯度 " + zone.Latitude.ToString("0.0", CultureInfo.InvariantCulture) +
                    " / 経度 " + zone.Longitude.ToString("0.0", CultureInfo.InvariantCulture) + "　およその範囲 ±" +
                    zone.RadiusGps.ToString("0.00", CultureInfo.InvariantCulture);
                RectangleF box = new RectangleF(map.Left, map.Bottom + 9, map.Width, 61);
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(31, 40, 50))) graphics.FillRectangle(fill, box);
                using (Font titleFont = new Font("Yu Gothic UI", 9.5F, FontStyle.Bold))
                using (Font detailFont = new Font("Yu Gothic UI", 8.2F))
                using (SolidBrush white = new SolidBrush(Color.White))
                using (SolidBrush muted = new SolidBrush(Color.FromArgb(175, 188, 199)))
                {
                    graphics.DrawString(title + "　" + counts, titleFont, white, box.Left + 8, box.Top + 6);
                    graphics.DrawString(gps, detailFont, muted, box.Left + 8, box.Top + 33);
                }
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                RectangleF map = GetMapRectangle();
                int mask = FilterMask;
                int nearest = -1;
                double best = 34D * 34D;
                for (int i = 0; i < zones.Count; i++)
                {
                    if (mask != 0 && (zones[i].ExpectedMask & mask) == 0) continue;
                    double dx = e.X - MapX(map, zones[i].Longitude);
                    double dy = e.Y - MapY(map, zones[i].Latitude);
                    double distance = dx * dx + dy * dy;
                    if (distance < best) { best = distance; nearest = i; }
                }
                if (nearest >= 0) SelectedZoneIndex = nearest;
            }

            private static double InterpolateGridPixel(double value, double[] anchors)
            {
                value = Math.Max(0D, Math.Min(100D, value));
                int lower = Math.Min(9, (int)Math.Floor(value / 10D));
                double fraction = (value - lower * 10D) / 10D;
                return anchors[lower] + (anchors[lower + 1] - anchors[lower]) * fraction;
            }
            private static float MapX(RectangleF map, double longitude) { return map.Left + (float)(InterpolateGridPixel(longitude, LongitudeGridPixels) / 1266D * map.Width); }
            private static float MapY(RectangleF map, double latitude) { return map.Top + (float)(InterpolateGridPixel(latitude, LatitudeGridPixels) / 1243D * map.Height); }
            protected override void Dispose(bool disposing)
            {
                if (disposing && fjordurMap != null) fjordurMap.Dispose();
                base.Dispose(disposing);
            }
        }

        private sealed class DinoMapPoint
        {
            public readonly int ResultIndex;
            public readonly int Level;
            public readonly string Area;
            public readonly double Latitude;
            public readonly double Longitude;
            public readonly double Z;
            public DinoMapPoint(int level, string area, double latitude, double longitude, double z) : this(0, level, area, latitude, longitude, z) { }
            public DinoMapPoint(int resultIndex, int level, string area, double latitude, double longitude, double z)
            {
                ResultIndex = resultIndex; Level = level; Area = area; Latitude = latitude; Longitude = longitude; Z = z;
            }
        }

        private sealed class DinoMapDialog : Form
        {
            private readonly List<DinoMapPoint> points;
            private readonly Label title;
            private readonly List<Label> selectorLabels = new List<Label>();
            private readonly DinoMapCanvas canvas;

            public DinoMapDialog(DinoMapPoint point) : this(new List<DinoMapPoint> { point }, 0) { }

            public DinoMapDialog(IList<DinoMapPoint> sourcePoints, int activeIndex)
            {
                points = new List<DinoMapPoint>(sourcePoints ?? new List<DinoMapPoint>());
                if (points.Count == 0) throw new ArgumentException("地図表示する個体がありません。", "sourcePoints");
                Text = "恐竜位置マップ";
                StartPosition = FormStartPosition.CenterParent;
                MinimumSize = new Size(700, 620);
                Size = new Size(820, 720);
                BackColor = Color.FromArgb(18, 24, 31);
                ForeColor = Color.FromArgb(235, 240, 245);
                Font = new Font("Yu Gothic UI", 10F);

                Panel header = new Panel { Dock = DockStyle.Top, Height = 112, BackColor = Color.FromArgb(18, 24, 31) };
                title = new Label
                {
                    Location = new Point(20, 10), Width = 760, Height = 55, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Font = new Font("Yu Gothic UI", 12F, FontStyle.Bold), ForeColor = ForeColor
                };
                FlowLayoutPanel selector = new FlowLayoutPanel
                {
                    Location = new Point(20, 70), Width = 760, Height = 32, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0)
                };
                Color[] selectorColors =
                {
                    Color.FromArgb(190, 55, 62), Color.FromArgb(40, 142, 88), Color.FromArgb(53, 108, 190),
                    Color.FromArgb(190, 127, 35), Color.FromArgb(139, 70, 178)
                };
                for (int i = 0; i < points.Count; i++)
                {
                    int captured = i;
                    Label chip = new Label
                    {
                        Text = "#" + (i + 1) + "  Lv." + points[i].Level, Width = 136, Height = 29,
                        Margin = new Padding(0, 0, 8, 0), TextAlign = ContentAlignment.MiddleCenter,
                        BackColor = selectorColors[i % selectorColors.Length], ForeColor = Color.White,
                        Font = new Font("Yu Gothic UI", 9F, FontStyle.Bold), Cursor = Cursors.Hand, BorderStyle = BorderStyle.FixedSingle
                    };
                    chip.Click += delegate { canvas.ActiveIndex = captured; };
                    selectorLabels.Add(chip);
                    selector.Controls.Add(chip);
                }
                header.Controls.Add(title); header.Controls.Add(selector);
                Label note = new Label
                {
                    Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(20, 8, 20, 0),
                    ForeColor = Color.FromArgb(155, 168, 182),
                    Text = "最大5体を色分け表示します。リストまたはピンをクリックすると選択個体が切り替わります。"
                };
                canvas = new DinoMapCanvas(points, activeIndex) { Dock = DockStyle.Fill, Margin = new Padding(16) };
                canvas.ActivePointChanged += delegate
                {
                    UpdateTitle();
                };
                Controls.Add(canvas);
                Controls.Add(note);
                Controls.Add(header);
                UpdateTitle();
            }

            private void UpdateTitle()
            {
                int index = Math.Max(0, Math.Min(points.Count - 1, canvas.ActiveIndex));
                DinoMapPoint point = points[index];
                for (int i = 0; i < selectorLabels.Count; i++)
                {
                    selectorLabels[i].BorderStyle = i == index ? BorderStyle.Fixed3D : BorderStyle.FixedSingle;
                    selectorLabels[i].Text = (i == index ? "選択中 #" : "#") + (i + 1) + "  Lv." + points[i].Level;
                }
                title.Text = "選択中 #" + (index + 1) + "/" + points.Count + "　Lv." + point.Level + "  " + point.Area + "\r\n" +
                    "緯度 " + point.Latitude.ToString("0.00", CultureInfo.InvariantCulture) +
                    "  /  経度 " + point.Longitude.ToString("0.00", CultureInfo.InvariantCulture) +
                    "  /  Z " + point.Z.ToString("0.0", CultureInfo.InvariantCulture);
            }
        }

        private sealed class DinoMapCanvas : Control
        {
            // Pixel positions of the 0-100 GPS grid on the bundled 1266 x 1243 map.
            // The image is hand drawn, so using each 10-point line as an anchor keeps
            // pins aligned even where the spacing is not perfectly uniform.
            private static readonly double[] LongitudeGridPixels = { 42, 159, 276, 392, 508, 622, 739, 853, 971, 1107, 1243 };
            private static readonly double[] LatitudeGridPixels = { 27, 139, 251, 363, 476, 589, 702, 815, 927, 1040, 1153 };
            private static readonly Color[] PinColors =
            {
                Color.FromArgb(255, 74, 82), Color.FromArgb(55, 196, 125), Color.FromArgb(78, 151, 255),
                Color.FromArgb(242, 176, 70), Color.FromArgb(190, 105, 238)
            };
            private readonly List<DinoMapPoint> points;
            private int activeIndex;
            private readonly double minLat;
            private readonly double maxLat;
            private readonly double minLon;
            private readonly double maxLon;
            private readonly Image fjordurMap;
            public event EventHandler ActivePointChanged;

            public DinoMapCanvas(DinoMapPoint point) : this(new List<DinoMapPoint> { point }, 0) { }

            public DinoMapCanvas(IList<DinoMapPoint> sourcePoints, int selectedIndex)
            {
                points = new List<DinoMapPoint>(sourcePoints ?? new List<DinoMapPoint>());
                if (points.Count == 0) throw new ArgumentException("地図表示する個体がありません。", "sourcePoints");
                activeIndex = Math.Max(0, Math.Min(points.Count - 1, selectedIndex));
                DoubleBuffered = true;
                BackColor = Color.FromArgb(11, 16, 22);
                minLat = 0; maxLat = 100; minLon = 0; maxLon = 100;
                try
                {
                    using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ArkServerManager.FjordurMap.png"))
                    using (Image source = stream == null ? null : Image.FromStream(stream))
                        if (source != null) fjordurMap = new Bitmap(source);
                }
                catch { fjordurMap = null; }
            }

            public int ActiveIndex
            {
                get { return activeIndex; }
                set
                {
                    int next = Math.Max(0, Math.Min(points.Count - 1, value));
                    if (next == activeIndex) return;
                    activeIndex = next;
                    Invalidate();
                    EventHandler handler = ActivePointChanged;
                    if (handler != null) handler(this, EventArgs.Empty);
                }
            }

            private RectangleF GetMapRectangle()
            {
                float size = Math.Max(100, Math.Min(Width - 115, Height - 92));
                return new RectangleF((Width - size) / 2F + 10, 34, size, size);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                RectangleF map = GetMapRectangle();
                using (SolidBrush ocean = new SolidBrush(Color.FromArgb(20, 50, 64))) e.Graphics.FillRectangle(ocean, map);
                if (fjordurMap != null) e.Graphics.DrawImage(fjordurMap, map);
                else DrawTerrain(e.Graphics, map);
                using (Pen border = new Pen(Color.FromArgb(130, 190, 205), 2)) e.Graphics.DrawRectangle(border, map.X, map.Y, map.Width, map.Height);

                for (int i = 0; i < points.Count; i++) if (i != activeIndex) DrawPin(e.Graphics, map, i, false);
                DrawPin(e.Graphics, map, activeIndex, true);
            }

            private void DrawPin(Graphics graphics, RectangleF map, int index, bool active)
            {
                DinoMapPoint point = points[index];
                float pinX = MapX(map, point.Longitude);
                float pinY = MapY(map, point.Latitude);
                Color color = PinColors[index % PinColors.Length];
                float radius = active ? 12F : 9F;
                if (active)
                    using (SolidBrush halo = new SolidBrush(Color.FromArgb(90, color))) graphics.FillEllipse(halo, pinX - 24, pinY - 24, 48, 48);
                using (SolidBrush pin = new SolidBrush(color)) graphics.FillEllipse(pin, pinX - radius, pinY - radius, radius * 2, radius * 2);
                using (Pen white = new Pen(Color.White, active ? 3F : 1.5F))
                {
                    graphics.DrawEllipse(white, pinX - radius, pinY - radius, radius * 2, radius * 2);
                    if (active) graphics.DrawLine(white, pinX, pinY + radius, pinX, pinY + radius + 14);
                }
                using (Font numberFont = new Font("Yu Gothic UI", active ? 9F : 7.5F, FontStyle.Bold))
                using (StringFormat center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (SolidBrush text = new SolidBrush(Color.White))
                    graphics.DrawString((index + 1).ToString(), numberFont, text, new RectangleF(pinX - radius, pinY - radius - 1, radius * 2, radius * 2), center);
                if (active)
                    using (Font font = new Font("Yu Gothic UI", 10F, FontStyle.Bold))
                    using (SolidBrush text = new SolidBrush(Color.White))
                        graphics.DrawString("選択中 #" + (index + 1) + "  Lv." + point.Level, font, text, pinX + 17, pinY - 22);
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                RectangleF map = GetMapRectangle();
                int nearest = -1;
                double best = 28D * 28D;
                for (int i = 0; i < points.Count; i++)
                {
                    double dx = e.X - MapX(map, points[i].Longitude);
                    double dy = e.Y - MapY(map, points[i].Latitude);
                    double distance = dx * dx + dy * dy;
                    if (distance < best) { best = distance; nearest = i; }
                }
                if (nearest >= 0) ActiveIndex = nearest;
            }

            private void DrawGrid(Graphics g, RectangleF map)
            {
                using (Pen grid = new Pen(Color.FromArgb(65, 130, 145), 1))
                using (Pen border = new Pen(Color.FromArgb(130, 190, 205), 2))
                using (Font font = new Font("Consolas", 8F))
                using (SolidBrush text = new SolidBrush(Color.FromArgb(155, 185, 194)))
                {
                    for (int i = 0; i <= 5; i++)
                    {
                        float x = map.Left + map.Width * i / 5F;
                        float y = map.Top + map.Height * i / 5F;
                        g.DrawLine(grid, x, map.Top, x, map.Bottom);
                        g.DrawLine(grid, map.Left, y, map.Right, y);
                        double lon = minLon + (maxLon - minLon) * i / 5D;
                        double lat = minLat + (maxLat - minLat) * i / 5D;
                        g.DrawString(lon.ToString("0.0", CultureInfo.InvariantCulture), font, text, x - 14, map.Bottom + 7);
                        g.DrawString(lat.ToString("0.0", CultureInfo.InvariantCulture), font, text, 7, y - 6);
                    }
                    g.DrawRectangle(border, map.X, map.Y, map.Width, map.Height);
                    g.DrawString("経度 →", font, text, map.Right - 46, map.Bottom + 26);
                    g.DrawString("緯度 ↓", font, text, 8, map.Top - 21);
                }
            }

            private void DrawTerrain(Graphics g, RectangleF map)
            {
                DinoMapPoint point = points[activeIndex];
                bool midgard = point.Area.IndexOf("ミッドガルド", StringComparison.OrdinalIgnoreCase) >= 0 || point.Area == "座標マップ";
                using (SolidBrush land = new SolidBrush(midgard ? Color.FromArgb(72, 107, 78) : Color.FromArgb(73, 87, 111)))
                using (SolidBrush accent = new SolidBrush(Color.FromArgb(90, 128, 92)))
                using (Font label = new Font("Yu Gothic UI", 10F, FontStyle.Bold))
                using (SolidBrush text = new SolidBrush(Color.FromArgb(205, 220, 210)))
                {
                    if (!midgard)
                    {
                        PointF[] realm = { P(map, minLon + (maxLon-minLon)*.12, minLat + (maxLat-minLat)*.18), P(map, minLon + (maxLon-minLon)*.78, minLat + (maxLat-minLat)*.08), P(map, minLon + (maxLon-minLon)*.92, minLat + (maxLat-minLat)*.62), P(map, minLon + (maxLon-minLon)*.66, minLat + (maxLat-minLat)*.92), P(map, minLon + (maxLon-minLon)*.14, minLat + (maxLat-minLat)*.78) };
                        g.FillPolygon(land, realm);
                        g.DrawString(point.Area, label, text, map.Left + 18, map.Top + 16);
                        return;
                    }
                    g.FillEllipse(land, R(map, 7, 3, 88, 61));
                    g.FillEllipse(accent, R(map, 0, 49, 50, 49));
                    using (SolidBrush lava = new SolidBrush(Color.FromArgb(126, 84, 57))) g.FillEllipse(lava, R(map, 69, 69, 30, 29));
                    using (SolidBrush swamp = new SolidBrush(Color.FromArgb(60, 94, 76))) g.FillEllipse(swamp, R(map, 1, 36, 17, 17));
                    g.DrawString("ヴァナランド", label, text, P(map, 45, 24));
                    g.DrawString("ヴァルディランド", label, text, P(map, 13, 72));
                    g.DrawString("バルヘイム", label, text, P(map, 76, 82));
                    g.DrawString("ボルビョルド", label, text, P(map, 2, 43));
                }
            }

            private PointF P(RectangleF map, double lon, double lat) { return new PointF(MapX(map, lon), MapY(map, lat)); }
            private RectangleF R(RectangleF map, double lon, double lat, double width, double height)
            {
                return new RectangleF(MapX(map, lon), MapY(map, lat), (float)(map.Width * width / (maxLon - minLon)), (float)(map.Height * height / (maxLat - minLat)));
            }
            private static double InterpolateGridPixel(double value, double[] anchors)
            {
                value = Math.Max(0D, Math.Min(100D, value));
                int lower = Math.Min(9, (int)Math.Floor(value / 10D));
                double fraction = (value - lower * 10D) / 10D;
                return anchors[lower] + (anchors[lower + 1] - anchors[lower]) * fraction;
            }
            private float MapX(RectangleF map, double lon)
            {
                return map.Left + (float)(InterpolateGridPixel(lon, LongitudeGridPixels) / 1266D * map.Width);
            }
            private float MapY(RectangleF map, double lat)
            {
                return map.Top + (float)(InterpolateGridPixel(lat, LatitudeGridPixels) / 1243D * map.Height);
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing && fjordurMap != null) fjordurMap.Dispose();
                base.Dispose(disposing);
            }
        }

        private sealed class DinoOption
        {
            public readonly string Name;
            public readonly string ClassName;
            public DinoOption(string name, string className) { Name = name; ClassName = className; }
            public override string ToString() { return Name; }
        }

        private sealed class DinoSearchResult
        {
            public readonly int Count;
            public readonly string Text;
            public readonly string ClassName;
            public readonly List<string> Actors;
            public readonly List<string> Locations;
            public DinoSearchResult(int count, string text, string className, List<string> actors, List<string> locations) { Count = count; Text = text; ClassName = className; Actors = actors ?? new List<string>(); Locations = locations ?? new List<string>(); }
        }
    }

    internal sealed class RconClient : IDisposable
    {
        private readonly string host; private readonly int port; private readonly string password; private TcpClient client; private NetworkStream stream; private int packetId = 10;
        public RconClient(string host, int port, string password) { this.host = host; this.port = port; this.password = password ?? ""; }
        public void Connect()
        {
            client = new TcpClient(); client.ReceiveTimeout = 4000; client.SendTimeout = 4000;
            IAsyncResult ar = client.BeginConnect(host, port, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(4000)) { client.Close(); throw new IOException("RCONポートに接続できません（タイムアウト）。"); }
            client.EndConnect(ar); stream = client.GetStream();
            int id = ++packetId; SendPacket(id, 3, password);
            bool accepted = false;
            for (int i = 0; i < 3; i++)
            {
                Packet p = ReadPacket();
                if (p.Id == -1) throw new UnauthorizedAccessException("RCON管理パスワードが一致しません。");
                if (p.Id == id) { accepted = true; break; }
            }
            if (!accepted) throw new IOException("RCON認証の応答を確認できません。");
        }
        public string Command(string command)
        {
            return Command(command, 120);
        }
        public string Command(string command, int quietMilliseconds)
        {
            int id = ++packetId; SendPacket(id, 2, command); Packet p = ReadPacket();
            StringBuilder response = new StringBuilder();
            if (p.Id == id && !String.IsNullOrEmpty(p.Body)) response.Append(p.Body);
            DateTime quietUntil = DateTime.UtcNow.AddMilliseconds(Math.Max(20, quietMilliseconds));
            while (DateTime.UtcNow < quietUntil)
            {
                if (client.Available >= 4)
                {
                    Packet next = ReadPacket();
                    if (next.Id == id && !String.IsNullOrEmpty(next.Body)) response.Append(next.Body);
                    quietUntil = DateTime.UtcNow.AddMilliseconds(Math.Max(20, quietMilliseconds));
                }
                else Thread.Sleep(15);
            }
            return response.ToString();
        }
        private void SendPacket(int id, int type, string body)
        {
            byte[] text = Encoding.UTF8.GetBytes(body ?? ""); int length = 10 + text.Length;
            using (MemoryStream ms = new MemoryStream()) using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write(length); bw.Write(id); bw.Write(type); bw.Write(text); bw.Write((byte)0); bw.Write((byte)0); bw.Flush();
                byte[] packet = ms.ToArray(); stream.Write(packet, 0, packet.Length); stream.Flush();
            }
        }
        private Packet ReadPacket()
        {
            byte[] lenBytes = ReadExact(4); int length = BitConverter.ToInt32(lenBytes, 0);
            if (length < 10 || length > 1024 * 1024) throw new IOException("不正なRCON応答です。");
            byte[] data = ReadExact(length); int id = BitConverter.ToInt32(data, 0); int type = BitConverter.ToInt32(data, 4);
            string body = Encoding.UTF8.GetString(data, 8, Math.Max(0, length - 10)); return new Packet(id, type, body);
        }
        private byte[] ReadExact(int count)
        {
            byte[] data = new byte[count]; int offset = 0;
            while (offset < count) { int n = stream.Read(data, offset, count - offset); if (n <= 0) throw new EndOfStreamException("RCON接続が閉じられました。"); offset += n; }
            return data;
        }
        public void Dispose() { if (stream != null) stream.Dispose(); if (client != null) client.Close(); }
        private sealed class Packet { public readonly int Id; public readonly int Type; public readonly string Body; public Packet(int id, int type, string body) { Id = id; Type = type; Body = body; } }
    }
}
