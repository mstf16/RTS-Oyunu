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
                new UnitStatData
                {
                    UnitId = "unit_worker",
                    DisplayName = "Amele / İşçi",
                    UnitClass = UnitClass.Melee,
                    MaxHealth = 50f,
                    AttackDamage = 3f,
                    AttackRange = 1f,
                    AttackCooldown = 1.2f,
                    ArmorValue = 0f,
                    MoveSpeed = 3.2f,
                    VisionRadius = 6f,
                    ProductionCost = new Dictionary<ResourceType, int> { { ResourceType.Wood, 40 }, { ResourceType.Food, 50 } },
                    BuildTimeSeconds = 6f
                },
                new UnitStatData
                {
                    UnitId = "unit_infantry",
                    DisplayName = "Kılıçlı Yiğit",
                    UnitClass = UnitClass.Melee,
                    MaxHealth = 120f,
                    AttackDamage = 14f,
                    AttackRange = 1.2f,
                    AttackCooldown = 0.9f,
                    ArmorValue = 3f,
                    MoveSpeed = 2.8f,
                    VisionRadius = 7f,
                    ProductionCost = new Dictionary<ResourceType, int> { { ResourceType.Wood, 60 }, { ResourceType.Food, 40 } },
                    BuildTimeSeconds = 12f
                },
                new UnitStatData
                {
                    UnitId = "unit_archer",
                    DisplayName = "Yaylı Okçu",
                    UnitClass = UnitClass.Ranged,
                    MaxHealth = 70f,
                    AttackDamage = 10f,
                    AttackRange = 5.5f,
                    AttackCooldown = 1.4f,
                    ArmorValue = 1f,
                    MoveSpeed = 2.7f,
                    VisionRadius = 9f,
                    ProductionCost = new Dictionary<ResourceType, int> { { ResourceType.Wood, 75 }, { ResourceType.Food, 45 } },
                    BuildTimeSeconds = 14f
                },
                new UnitStatData
                {
                    UnitId = "unit_knight_elite",
                    DisplayName = "Zırhlı Süvari (Elit)",
                    UnitClass = UnitClass.Elite,
                    MaxHealth = 220f,
                    AttackDamage = 26f,
                    AttackRange = 1.4f,
                    AttackCooldown = 1.1f,
                    ArmorValue = 6f,
                    MoveSpeed = 3.6f,
                    VisionRadius = 8f,
                    ProductionCost = new Dictionary<ResourceType, int> { { ResourceType.Wood, 120 }, { ResourceType.Food, 150 } },
                    BuildTimeSeconds = 25f
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
                    BaseProductionSpeed = 1.0f
                },
                new BuildingStatData
                {
                    BuildingId = "building_barracks",
                    DisplayName = "Kışla",
                    MaxHealth = 600f,
                    MaxRoadDistanceThreshold = 5f,
                    ConfidenceDropRatePerTile = 0.15f,
                    MaxGarrisonCap = 2,
                    BaseProductionSpeed = 1.0f
                },
                new BuildingStatData
                {
                    BuildingId = "building_storage",
                    DisplayName = "Tahıl Deposu",
                    MaxHealth = 400f,
                    MaxRoadDistanceThreshold = 4f,
                    ConfidenceDropRatePerTile = 0.20f,
                    MaxGarrisonCap = 3,
                    BaseProductionSpeed = 1.0f
                }
            };
        }
    }
}
