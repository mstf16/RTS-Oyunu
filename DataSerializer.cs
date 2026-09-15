using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RTSProje
{
    // ============================================================
    // DataSerializer.cs
    // ------------------------------------------------------------
    // Burası oyunun tarif defteri (Reçete yöneticisi).
    // Askerin canını, kışlanın maliyetini kodun içine çakarsan (hardcode),
    // her denge ayarında projeyi baştan derlemek zorunda kalırsın.
    // Bunun yerine değerleri harici JSON dosyalarına (unit_stats.json vb.)
    // koyuyoruz.
    //
    // Neden Çökme Koruması (Fallback) Var?
    // Biri yanlışlıkla JSON dosyasını silerse veya içine yanlış bir
    // virgül atıp bozarsa oyun patlayıp masaüstüne dönmesin.
    // Hemen kendi cebinden fabrika ayarlarını çıkarır, oyunu
    // tıkır tıkır başlatır ve bozulan dosyayı da arkada onarır.
    // ============================================================


    // ------------------------------------------------------------
    // VERİ MODELLERİ (JSON ile C# arasındaki tercümanlar)
    // ------------------------------------------------------------

    public class EngineConfigData
    {
        public int TargetFPS { get; set; } = 60;
        public float TimeScale { get; set; } = 1.0f;
        public int MapWidth { get; set; } = 64;
        public int MapHeight { get; set; } = 64;
        public float DefaultBuildingConfidence { get; set; } = 1.0f;
        public float RoadDecayMultiplier { get; set; } = 0.08f;
        public bool EnableFogOfWar { get; set; } = true;
    }

    public class UnitStatData
    {
        public string UnitId { get; set; } = "infantry_basic";
        public string DisplayName { get; set; } = "Piyade";
        public UnitClass UnitClass { get; set; } = UnitClass.Melee;
        public float MaxHealth { get; set; } = 100f;
        public float HealthRegenRate { get; set; } = 0f;
        public float AttackDamage { get; set; } = 12f;
        public float AttackRange { get; set; } = 1.2f;
        public float AttackCooldown { get; set; } = 1.0f;
        public float ArmorValue { get; set; } = 2f;
        public float MoveSpeed { get; set; } = 3.5f;
        public float VisionRadius { get; set; } = 8f;
        public Dictionary<ResourceType, int> ProductionCost { get; set; } = new Dictionary<ResourceType, int>
        {
            { ResourceType.Wood, 50 },
            { ResourceType.Food, 30 }
        };
        public float BuildTimeSeconds { get; set; } = 10f;

        // Koçbaşı/mancınık gibi kuşatma birimleri askere karşı zayıf,
        // binaya karşı devasa hasar verir. 0 ise "sıradan birim,
        // binalara özel bir avantajı yok" demek.
        public float SiegeDamageBonusVsBuildings { get; set; } = 0f;

        // Kuşatma birimi mi? System_DynamicSynergy ve CombatResolver
        // (Faz 5) bu bayrağa bakıp "kuşatma mancınığı yakalandı,
        // savunmasız" gibi kuralları buradan tetikleyecek.
        public bool IsSiegeUnit { get; set; } = false;
        
        // Bu birim bir işçi mi? true ise EconomyManager, doğum anında
        // WorkerState component'ini de hazırlayıp WorkerBehavior'a
        // kaydediyor - böylece kaynak toplama döngüsüne girebiliyor.
        public bool IsWorker { get; set; } = false;   
    }

    public class BuildingStatData
    {
        public string BuildingId { get; set; } = "barracks_tier1";
        public string DisplayName { get; set; } = "Kışla";
        public float MaxHealth { get; set; } = 500f;
        public float MaxRoadDistanceThreshold { get; set; } = 6f; // Asfalttan bu kadar uzaklaşırsa verim düşmeye başlar
        public float ConfidenceDropRatePerTile { get; set; } = 0.15f; // Her adımda verim kaç puan erisin?
        public int MaxGarrisonCap { get; set; } = 2; // İçine kaç muhafız alabilir?
        public float BaseProductionSpeed { get; set; } = 1.0f;

        public Dictionary<ResourceType, int> ProductionCost { get; set; } = new Dictionary<ResourceType, int>
        {
            { ResourceType.Wood, 100 }
        };

        // Bina haritada kaç hücre kaplıyor (örn. 3x3 bir kışla).
        public int FootprintWidth { get; set; } = 2;
        public int FootprintHeight { get; set; } = 2;

        // Bu binayı inşa etmek için hangi evrede (Tier) olman lazım?
        public int RequiredTier { get; set; } = 1;

        // Bu bina inşa edildiğinde takımın evresini bir üst seviyeye
        // mi çıkarıyor? (Örn. "Tier 2 Konağı" inşa edince Tier 2'ye geçilir.)
        public bool IsTierAdvancementBuilding { get; set; } = false;

        // Tedarik ağı (SupplyNode) özellikleri - her bina değil, sadece
        // özel olarak işaretlenmiş binalar sinyal yayar.
        public bool IsSupplyNode { get; set; } = false;
        public float SupplyRadius { get; set; } = 0f;
        public bool IsNetworkRoot { get; set; } = false; // Ana üs (Town Center) burada true olur
    }


    public static class DataSerializer
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true, // İnsan evladı açıp okuyabilsin diye girintili yaz
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() } // Enum'ları sayı yerine "Melee", "Wood" diye metin kaydet
        };

        private const string DefaultDataFolder = "GameData";

        public static string EnsureDataDirectory(string subFolder = "")
        {
            string targetPath = string.IsNullOrWhiteSpace(subFolder)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultDataFolder)
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultDataFolder, subFolder);

            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }

            return targetPath;
        }

        // Motor ayarlarını oku, yoksa hemen yenisini yaz
        public static EngineConfigData LoadEngineConfig(string fileName = "config_base.json")
        {
            string folder = EnsureDataDirectory();
            string fullPath = Path.Combine(folder, fileName);

            if (!File.Exists(fullPath))
            {
                var defaultConfig = CreateDefaultEngineConfig();
                SaveToFile(fullPath, defaultConfig);
                return defaultConfig;
            }

            try
            {
                string jsonText = File.ReadAllText(fullPath);
                var config = JsonSerializer.Deserialize<EngineConfigData>(jsonText, _jsonOptions);
                return config ?? CreateDefaultEngineConfig();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[UYARI]: {fileName} bozulmuş ({ex.Message}). Fabrika ayarları devreye sokuldu!");
                Console.ResetColor();

                var fallbackConfig = CreateDefaultEngineConfig();
                SaveToFile(fullPath + ".backup.json", fallbackConfig);
                return fallbackConfig;
            }
        }

        // Asker şablonlarını oku
        public static Dictionary<string, UnitStatData> LoadUnitDatabase(string fileName = "unit_stats.json")
        {
            string folder = EnsureDataDirectory();
            string fullPath = Path.Combine(folder, fileName);
            var resultDict = new Dictionary<string, UnitStatData>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(fullPath))
            {
                var defaultUnits = CreateDefaultUnitList();
                SaveToFile(fullPath, defaultUnits);

                for (int i = 0; i < defaultUnits.Count; i++)
                {
                    resultDict[defaultUnits[i].UnitId] = defaultUnits[i];
                }
                return resultDict;
            }

            try
            {
                string jsonText = File.ReadAllText(fullPath);
                var unitList = JsonSerializer.Deserialize<List<UnitStatData>>(jsonText, _jsonOptions);

                if (unitList != null)
                {
                    for (int i = 0; i < unitList.Count; i++)
                    {
                        resultDict[unitList[i].UnitId] = unitList[i];
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[HATA]: {fileName} okunamadı: {ex.Message}");
                Console.ResetColor();

                var defaultUnits = CreateDefaultUnitList();
                for (int i = 0; i < defaultUnits.Count; i++)
                {
                    resultDict[defaultUnits[i].UnitId] = defaultUnits[i];
                }
            }

            return resultDict;
        }

        // Bina şablonlarını oku
        public static Dictionary<string, BuildingStatData> LoadBuildingDatabase(string fileName = "building_stats.json")
        {
            string folder = EnsureDataDirectory();
            string fullPath = Path.Combine(folder, fileName);
            var resultDict = new Dictionary<string, BuildingStatData>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(fullPath))
            {
                var defaultBuildings = CreateDefaultBuildingList();
                SaveToFile(fullPath, defaultBuildings);

                for (int i = 0; i < defaultBuildings.Count; i++)
                {
                    resultDict[defaultBuildings[i].BuildingId] = defaultBuildings[i];
                }
                return resultDict;
            }

            try
            {
                string jsonText = File.ReadAllText(fullPath);
                var buildingList = JsonSerializer.Deserialize<List<BuildingStatData>>(jsonText, _jsonOptions);

                if (buildingList != null)
                {
                    for (int i = 0; i < buildingList.Count; i++)
                    {
                        resultDict[buildingList[i].BuildingId] = buildingList[i];
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[HATA]: {fileName} okunamadı: {ex.Message}");
                Console.ResetColor();

                var defaultBuildings = CreateDefaultBuildingList();
                for (int i = 0; i < defaultBuildings.Count; i++)
                {
                    resultDict[defaultBuildings[i].BuildingId] = defaultBuildings[i];
                }
            }

            return resultDict;
        }

        public static bool SaveToFile<T>(string filePath, T data)
        {
            try
            {
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string jsonString = JsonSerializer.Serialize(data, _jsonOptions);
                File.WriteAllText(filePath, jsonString);
                return true;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[HATA]: {filePath} diske yazılamadı: {ex.Message}");
                Console.ResetColor();
                return false;
            }
        }

        // Fabrika standartları
        private static EngineConfigData CreateDefaultEngineConfig()
        {
            return new EngineConfigData
            {
                TargetFPS = 60,
                TimeScale = 1.0f,
                MapWidth = 64,
                MapHeight = 64,
                DefaultBuildingConfidence = 1.0f,
                RoadDecayMultiplier = 0.08f,
                EnableFogOfWar = true
            };
        }

        private static List<UnitStatData> CreateDefaultUnitList()
        {
            return new List<UnitStatData>
            {
                
                 // 0) KÖYLÜ / İŞÇİ - savaşamaz, sadece kaynak toplar, 0 altın
                new UnitStatData
                {
                    UnitId = "unit_villager",
                    DisplayName = "Köylü",
                    UnitClass = UnitClass.Melee,
                    MaxHealth = 35f,
                    AttackDamage = 1f,
                    AttackRange = 0.8f,
                    AttackCooldown = 2f,
                    ArmorValue = 0f,
                    MoveSpeed = 2.8f,
                    VisionRadius = 5f,
                    ProductionCost = new Dictionary<ResourceType, int> { { ResourceType.Food, 25 } },
                    BuildTimeSeconds = 5f,
                    IsWorker = true
                },

                // 1) ET SİPERİ - 0 altın, kalabalık, düşmanın ilk vuruşunu göğüsler
                new UnitStatData
                {
                    UnitId = "unit_shield_bearer",
                    DisplayName = "Et Siperi",
                    UnitClass = UnitClass.Melee,
                    MaxHealth = 90f,
                    AttackDamage = 6f,
                    AttackRange = 1f,
                    AttackCooldown = 1.1f,
                    ArmorValue = 1f,
                    MoveSpeed = 3.0f,
                    VisionRadius = 6f,
                    ProductionCost = new Dictionary<ResourceType, int> { { ResourceType.Wood, 30 }, { ResourceType.Food, 20 } },
                    BuildTimeSeconds = 6f
                },

                // 2) TACİZCİ - 0 altın, hafif menzilli, vur-kaç
                new UnitStatData
                {
                    UnitId = "unit_skirmisher",
                    DisplayName = "Tacizci",
                    UnitClass = UnitClass.Ranged,
                    MaxHealth = 45f,
                    AttackDamage = 8f,
                    AttackRange = 4.5f,
                    AttackCooldown = 1.3f,
                    ArmorValue = 0f,
                    MoveSpeed = 3.3f,
                    VisionRadius = 7f,
                    ProductionCost = new Dictionary<ResourceType, int> { { ResourceType.Wood, 35 }, { ResourceType.Food, 25 } },
                    BuildTimeSeconds = 7f
                },

                // 3) ZIRHLI HAT ASKERİ - altın ister, ordunun omurgası
                new UnitStatData
                {
                    UnitId = "unit_line_guard",
                    DisplayName = "Zırhlı Hat Askeri",
                    UnitClass = UnitClass.Melee,
                    MaxHealth = 150f,
                    AttackDamage = 16f,
                    AttackRange = 1.2f,
                    AttackCooldown = 1.0f,
                    ArmorValue = 5f,
                    MoveSpeed = 2.6f,
                    VisionRadius = 7f,
                    ProductionCost = new Dictionary<ResourceType, int> { { ResourceType.Wood, 50 }, { ResourceType.Food, 30 }, { ResourceType.Gold, 15 } },
                    BuildTimeSeconds = 13f
                },

                // 4) AĞIR MENZİLLİ - altın ister, zırh delen, arkadan vurur
                new UnitStatData
                {
                    UnitId = "unit_heavy_ranged",
                    DisplayName = "Ağır Menzilli",
                    UnitClass = UnitClass.Ranged,
                    MaxHealth = 65f,
                    AttackDamage = 20f,
                    AttackRange = 6f,
                    AttackCooldown = 1.5f,
                    ArmorValue = 1f,
                    MoveSpeed = 2.5f,
                    VisionRadius = 9f,
                    ProductionCost = new Dictionary<ResourceType, int> { { ResourceType.Wood, 60 }, { ResourceType.Gold, 25 } },
                    BuildTimeSeconds = 16f
                },

                // 5) ELİT / ŞOK - pahalı, tek başına tehlikeli ama korumasız kalırsa savunmasız
                new UnitStatData
                {
                    UnitId = "unit_elite_shock",
                    DisplayName = "Elit Şok Birliği",
                    UnitClass = UnitClass.Elite,
                    MaxHealth = 200f,
                    AttackDamage = 30f,
                    AttackRange = 1.4f,
                    AttackCooldown = 1.1f,
                    ArmorValue = 6f,
                    MoveSpeed = 3.4f,
                    VisionRadius = 8f,
                    ProductionCost = new Dictionary<ResourceType, int> { { ResourceType.Wood, 80 }, { ResourceType.Food, 60 }, { ResourceType.Gold, 60 } },
                    BuildTimeSeconds = 26f
                },

                // 6) YAKIN KUŞATMA (KOÇBAŞI) - askere hantal, binaya balyoz gibi
                new UnitStatData
                {
                    UnitId = "unit_battering_ram",
                    DisplayName = "Koçbaşı",
                    UnitClass = UnitClass.Melee,
                    MaxHealth = 180f,
                    AttackDamage = 5f,
                    AttackRange = 1f,
                    AttackCooldown = 1.6f,
                    ArmorValue = 4f,
                    MoveSpeed = 1.6f,
                    VisionRadius = 5f,
                    ProductionCost = new Dictionary<ResourceType, int> { { ResourceType.Wood, 100 }, { ResourceType.Gold, 20 } },
                    BuildTimeSeconds = 22f,
                    SiegeDamageBonusVsBuildings = 75f,
                    IsSiegeUnit = true
                },

                // 7) UZAK KUŞATMA (MANCINIK) - uzun menzil, yakalanırsa kırılgan
                new UnitStatData
                {
                    UnitId = "unit_catapult",
                    DisplayName = "Mancınık",
                    UnitClass = UnitClass.Ranged,
                    MaxHealth = 70f,
                    AttackDamage = 4f,
                    AttackRange = 8f,
                    AttackCooldown = 2.5f,
                    ArmorValue = 0f,
                    MoveSpeed = 1.2f,
                    VisionRadius = 8f,
                    ProductionCost = new Dictionary<ResourceType, int> { { ResourceType.Wood, 90 }, { ResourceType.Gold, 40 } },
                    BuildTimeSeconds = 24f,
                    SiegeDamageBonusVsBuildings = 100f,
                    IsSiegeUnit = true
                }
            };
        }

                private static List<BuildingStatData> CreateDefaultBuildingList()
        {
            return new List<BuildingStatData>
            {
                new BuildingStatData
                {
                    BuildingId = "building_towncenter",
                    DisplayName = "Köy Konağı / Merkez",
                    MaxHealth = 1500f,
                    MaxRoadDistanceThreshold = 12f,
                    ConfidenceDropRatePerTile = 0.05f,
                    MaxGarrisonCap = 5,
                    BaseProductionSpeed = 1.0f,
                    ProductionCost = new Dictionary<ResourceType, int>(),
                    FootprintWidth = 3,
                    FootprintHeight = 3,
                    RequiredTier = 1,
                    IsSupplyNode = true,
                    SupplyRadius = 14f,
                    IsNetworkRoot = true // Ana üs - tedarik ağının kalbi
                },
                new BuildingStatData
                {
                    BuildingId = "building_barracks",
                    DisplayName = "Kışla",
                    MaxHealth = 600f,
                    MaxRoadDistanceThreshold = 5f,
                    ConfidenceDropRatePerTile = 0.15f,
                    MaxGarrisonCap = 2,
                    BaseProductionSpeed = 1.0f,
                    ProductionCost = new Dictionary<ResourceType, int> { { ResourceType.Wood, 120 } },
                    FootprintWidth = 2,
                    FootprintHeight = 2,
                    RequiredTier = 1
                },
                new BuildingStatData
                {
                    BuildingId = "building_storage",
                    DisplayName = "Tahıl Deposu",
                    MaxHealth = 400f,
                    MaxRoadDistanceThreshold = 4f,
                    ConfidenceDropRatePerTile = 0.20f,
                    MaxGarrisonCap = 3,
                    BaseProductionSpeed = 1.0f,
                    ProductionCost = new Dictionary<ResourceType, int> { { ResourceType.Wood, 60 } },
                    FootprintWidth = 2,
                    FootprintHeight = 2,
                    RequiredTier = 1
                },
               
                               new BuildingStatData
                {
                    BuildingId = "building_farm",
                    DisplayName = "Tarla",
                    MaxHealth = 250f,
                    MaxRoadDistanceThreshold = 8f,
                    ConfidenceDropRatePerTile = 0.10f,
                    MaxGarrisonCap = 1,
                    BaseProductionSpeed = 1.0f,
                    ProductionCost = new Dictionary<ResourceType, int> { { ResourceType.Wood, 70 } },
                    FootprintWidth = 2,
                    FootprintHeight = 2,
                    RequiredTier = 1
                },

                new BuildingStatData
                {
                    BuildingId = "building_supply_post",
                    DisplayName = "Tedarik Karakolu",
                    MaxHealth = 250f,
                    MaxRoadDistanceThreshold = 10f,
                    ConfidenceDropRatePerTile = 0.10f,
                    MaxGarrisonCap = 1,
                    BaseProductionSpeed = 1.0f,
                    ProductionCost = new Dictionary<ResourceType, int> { { ResourceType.Wood, 80 } },
                    FootprintWidth = 1,
                    FootprintHeight = 1,
                    RequiredTier = 1,
                    IsSupplyNode = true,
                    SupplyRadius = 9f
                },
                new BuildingStatData
                {
                    BuildingId = "building_tier2_hall",
                    DisplayName = "Gelişmiş Konak (Tier 2)",
                    MaxHealth = 1200f,
                    MaxRoadDistanceThreshold = 12f,
                    ConfidenceDropRatePerTile = 0.05f,
                    MaxGarrisonCap = 3,
                    BaseProductionSpeed = 1.0f,
                    ProductionCost = new Dictionary<ResourceType, int> { { ResourceType.Wood, 250 }, { ResourceType.Food, 150 } },
                    FootprintWidth = 3,
                    FootprintHeight = 3,
                    RequiredTier = 1,
                    IsTierAdvancementBuilding = true
                },
                new BuildingStatData
                {
                    BuildingId = "building_tier3_hall",
                    DisplayName = "Gelişmiş Konak (Tier 3)",
                    MaxHealth = 1800f,
                    MaxRoadDistanceThreshold = 12f,
                    ConfidenceDropRatePerTile = 0.05f,
                    MaxGarrisonCap = 4,
                    BaseProductionSpeed = 1.0f,
                    ProductionCost = new Dictionary<ResourceType, int> { { ResourceType.Wood, 400 }, { ResourceType.Food, 250 }, { ResourceType.Gold, 100 } },
                    FootprintWidth = 3,
                    FootprintHeight = 3,
                    RequiredTier = 2,
                    IsTierAdvancementBuilding = true
                }
            };
        }
    }
}
