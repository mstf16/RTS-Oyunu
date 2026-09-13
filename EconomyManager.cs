using System;
using System.Collections.Generic;

namespace RTSProje
{
    // ============================================================
    // EconomyManager.cs
    // ------------------------------------------------------------
    // Burası belediyenin kasası ve inşaat izin bürosu bir arada.
    // Her takımın (mahallenin) ne kadar odunu, taşı, yemeği,
    // altını olduğunu tutar. Birisi "bir kışla kurmak istiyorum"
    // dediğinde, kasaya bakar - para yetiyorsa keseden düşer ve
    // izni verir, yetmiyorsa "olmaz" der.
    //
    // Neden bina/birim DOĞURMA işi de burada?
    // Çünkü bir birim/bina doğması ile kaynak harcanması AYNI ANDA,
    // BÖLÜNMEZ bir işlem olmalı. Eğer bu ikisini ayrı yerlerde
    // yapsaydık (önce parayı düş, sonra ayrı bir yerde binayı kur),
    // arada bir hata olursa (örn. bina kurulamadı ama para zaten
    // düşürüldü) parayı boşa kaybetmiş olurduk. Tek bir metod
    // içinde ikisini birden yapmak bu riski ortadan kaldırıyor.
    // ============================================================
    public class EconomyManager : ISystem
    {
        // Şu an kaç takım destekliyoruz - ileride çok oyunculu/AI
        // için 4'e çıkarıldı (daha önce 2 idi).
        private const int MaxTeams = 4;

        // ResourceType enum'ında 5 değer var (None, Wood, Stone, Food, Gold).
        // Dizi boyutunu buna göre sabitliyoruz.
        private const int ResourceTypeCount = 5;

        // Kasa defteri: tek boyutlu dizi olarak tutuluyor - tıpkı
        // GridManager'daki _occupancy gibi. 2 boyutlu dizi (float[,])
        // C#'ta arka planda ekstra bir sınır kontrolü katmanı taşır
        // ve bellekte tek boyutlu diziler kadar sıkı sıralı durmaz.
        // Erişim formülü: takım * ResourceTypeCount + kaynak_indeksi.
        private readonly float[] _stockpile = new float[MaxTeams * ResourceTypeCount];

        // Tek boyutlu diziye erişirken bu formülü tekrar tekrar
        // yazmamak için küçük bir yardımcı metod.
        private static int StockpileIndex(int teamId, ResourceType type)
        {
            return teamId * ResourceTypeCount + (int)type;
        }

        // Her takımın şu an hangi evrede (Tier) olduğu. 1'den başlar.
        private readonly int[] _currentTier = new int[MaxTeams];

        // JSON'dan okunan birim/bina şablonları - oyunun başında
        // BİR KERE yüklenir, sonra sürekli buradan okunur.
        private Dictionary<string, UnitStatData> _unitDatabase = null!;
        private Dictionary<string, BuildingStatData> _buildingDatabase = null!;

        // Bina/birim doğduğunda kaydolması gereken diğer sistemler.
        // Belediye binası tek başına çalışmaz, tapu dairesine
        // (GridManager), nüfus müdürlüğüne (LogisticsConfidence vb.)
        // haber vermesi gerekir.
        private readonly GridManager _gridManager;
        private readonly System_LogisticsConfidence _logisticsConfidence;
        private readonly BuildingStateMonitor _buildingStateMonitor;
        private readonly TerrainPhysics _terrainPhysics;
        private readonly System_FogOfWar _fogOfWar;

        private Position[]? _positions;
        private Health[]? _healths;
        private CombatStats[]? _combatStats;
        private MovementSpeed[]? _movementSpeeds;
        private OwnerTag[]? _ownerTags;
        private SynergyTags[]? _synergyTags;
        private VisionRadius[]? _visionRadii;
        private ResourceCarrier[]? _resourceCarriers;
        private TierInfo[]? _tierInfos;
        private CollisionFlag[]? _collisionFlags;
        private BuildingConfidence[]? _buildingConfidences;
        private SupplyNode[]? _supplyNodes;

        private bool _isInitialized;

        public EconomyManager(
            GridManager gridManager,
            System_LogisticsConfidence logisticsConfidence,
            BuildingStateMonitor buildingStateMonitor,
            TerrainPhysics terrainPhysics,
            System_FogOfWar fogOfWar)
        {
            _gridManager = gridManager;
            _logisticsConfidence = logisticsConfidence;
            _buildingStateMonitor = buildingStateMonitor;
            _terrainPhysics = terrainPhysics;
            _fogOfWar = fogOfWar;
        }

        public void Initialize()
        {
            // Tarif defterini (JSON) oyunun başında bir kere oku.
            _unitDatabase = DataSerializer.LoadUnitDatabase();
            _buildingDatabase = DataSerializer.LoadBuildingDatabase();

            _positions = World.GetArray<Position>();
            _healths = World.GetArray<Health>();
            _combatStats = World.GetArray<CombatStats>();
            _movementSpeeds = World.GetArray<MovementSpeed>();
            _ownerTags = World.GetArray<OwnerTag>();
            _synergyTags = World.GetArray<SynergyTags>();
            _visionRadii = World.GetArray<VisionRadius>();
            _resourceCarriers = World.GetArray<ResourceCarrier>();
            _tierInfos = World.GetArray<TierInfo>();
            _collisionFlags = World.GetArray<CollisionFlag>();
            _buildingConfidences = World.GetArray<BuildingConfidence>();
            _supplyNodes = World.GetArray<SupplyNode>();

            // Her takım 1. evreden (Başlangıç) başlar.
            for (int t = 0; t < MaxTeams; t++)
            {
                _currentTier[t] = 1;
            }

            _isInitialized = true;
        }

        public void Update(float deltaTime)
        {
            // EconomyManager pasif bir kasadır - kendi kendine bir
            // şey yapmaz, sadece SpawnUnit/SpawnBuilding gibi
            // çağrıldığında iş yapar. Tıpkı GridManager gibi.
        }

        public void Shutdown()
        {
            // Abone olunmuş bir event yok, temizlenecek bir şey yok.
        }


        // ------------------------------------------------------------
        // KAYNAK SORGU VE YÖNETİMİ
        // ------------------------------------------------------------

        public float GetResourceAmount(int teamId, ResourceType type)
        {
            return _stockpile[StockpileIndex(teamId, type)];
        }

        public void AddResource(int teamId, ResourceType type, float amount)
        {
            _stockpile[StockpileIndex(teamId, type)] += amount;
        }

        public int GetCurrentTier(int teamId)
        {
            return _currentTier[teamId];
        }

        // Bir maliyet listesini karşılayacak kaynak var mı, VE varsa
        // hemen düş. İkisi TEK metotta çünkü "önce kontrol et, sonra
        // düş" arasında geçen sürede başka bir işlem araya girip
        // parayı iki kere harcatabilir (RTS'lerde buna "kaynak
        // yarışı" hatası denir).
        private bool TrySpendResources(int teamId, Dictionary<ResourceType, int> cost)
        {
            // Önce TÜM kaynaklar yeterli mi diye bak, hiçbirini düşme.
            foreach (KeyValuePair<ResourceType, int> entry in cost)
            {
                if (_stockpile[StockpileIndex(teamId, entry.Key)] < entry.Value)
                {
                    return false;
                }
            }

            // Hepsi yeterliyse şimdi gerçekten düş.
            foreach (KeyValuePair<ResourceType, int> entry in cost)
            {
                _stockpile[StockpileIndex(teamId, entry.Key)] -= entry.Value;
            }

            return true;
        }


        // ------------------------------------------------------------
        // BİRİM DOĞURMA (SPAWN UNIT)
        // ------------------------------------------------------------
        public EntityHandle SpawnUnit(string unitId, int teamId, int gridX, int gridY)
        {
            if (!_isInitialized) return EntityHandle.Invalid;

            if (!_unitDatabase.TryGetValue(unitId, out UnitStatData? stats) || stats == null)
            {
                return EntityHandle.Invalid; // Böyle bir birim şablonu yok
            }

            if (!TrySpendResources(teamId, stats.ProductionCost))
            {
                return EntityHandle.Invalid; // Kasa yetersiz
            }

            EntityHandle entity = World.CreateEntity();
            if (!entity.IsValid)
            {
                return EntityHandle.Invalid; // Şehir dolu (1024 sınırı)
            }

            int index = entity.Index;

            // Component'leri sıfırdan, JSON verisine göre dolduruyoruz.
            // Önceki sahibinden kalma "artık" veri kalmasın diye HER
            // alanı elle yazıyoruz (World'ün otomatik sıfırlama yapmadığını
            // hatırla - bu bilinçli bir tasarım kararıydı).
            _positions![index] = new Position
            {
                GridX = gridX,
                GridY = gridY,
                TargetGridX = gridX,
                TargetGridY = gridY
            };
            _gridManager.GridToPixel(gridX, gridY, out float pixelX, out float pixelY);
            _positions[index].PixelX = pixelX;
            _positions[index].PixelY = pixelY;

            _healths![index] = new Health
            {
                Current = stats.MaxHealth,
                Max = stats.MaxHealth,
                RegenRate = stats.HealthRegenRate
            };

            _combatStats![index] = new CombatStats
            {
                AttackDamage = stats.AttackDamage,
                AttackRange = stats.AttackRange,
                AttackCooldown = stats.AttackCooldown,
                ArmorValue = stats.ArmorValue
            };

            _movementSpeeds![index] = new MovementSpeed
            {
                BaseSpeed = stats.MoveSpeed,
                TerrainMultiplier = 1f,
                CurrentSpeed = stats.MoveSpeed
            };

            _ownerTags![index] = new OwnerTag { TeamId = teamId };

            _synergyTags![index] = new SynergyTags
            {
                UnitClass = stats.UnitClass,
                NearbyMeleeCount = 0,
                NearbyRangedCount = 0
            };

            _visionRadii![index] = new VisionRadius
            {
                BaseRadius = stats.VisionRadius,
                CurrentRadius = stats.VisionRadius
            };

            // Sadece işçi tipi birimler için anlamlı, ama her birime
            // 0 dolu bir "boş çuval" vermek zararsız.
            _resourceCarriers![index] = new ResourceCarrier
            {
                CarriedAmount = 0f,
                Capacity = 50f,
                CarriedResourceType = ResourceType.None
            };

            _tierInfos![index] = new TierInfo
            {
                CurrentTier = _currentTier[teamId],
                UnlockedAt = 1
            };

            _collisionFlags![index] = new CollisionFlag
            {
                IsBlocking = true,
                IsStatic = false // Birim hareket eder, bina gibi çakılı değil
            };

            // Haritaya yerleştir - eğer hücre doluysa entity'yi geri al.
            if (!_gridManager.PlaceEntity(entity, gridX, gridY))
            {
                World.DestroyEntity(entity);
                return EntityHandle.Invalid;
            }

            // Diğer sistemlere "yeni bir birim doğdu" diye haber ver.
            _terrainPhysics.RegisterUnit(entity);
            _fogOfWar.RegisterUnit(entity);

            return entity;
        }


        // ------------------------------------------------------------
        // BİNA DOĞURMA (SPAWN BUILDING)
        // ------------------------------------------------------------
        public EntityHandle SpawnBuilding(string buildingId, int teamId, int originGridX, int originGridY)
        {
            if (!_isInitialized) return EntityHandle.Invalid;

            if (!_buildingDatabase.TryGetValue(buildingId, out BuildingStatData? stats) || stats == null)
            {
                return EntityHandle.Invalid;
            }

            // Evre şartını kontrol et - "Tier 2 gerekiyor" diyen bir
            // binayı, Tier 1'deyken kuramazsın.
            if (_currentTier[teamId] < stats.RequiredTier)
            {
                return EntityHandle.Invalid;
            }

            if (!TrySpendResources(teamId, stats.ProductionCost))
            {
                return EntityHandle.Invalid;
            }

            EntityHandle entity = World.CreateEntity();
            if (!entity.IsValid)
            {
                return EntityHandle.Invalid;
            }

            int index = entity.Index;

            _positions![index] = new Position
            {
                GridX = originGridX,
                GridY = originGridY,
                TargetGridX = originGridX,
                TargetGridY = originGridY
            };
            _gridManager.GridToPixel(originGridX, originGridY, out float pixelX, out float pixelY);
            _positions[index].PixelX = pixelX;
            _positions[index].PixelY = pixelY;

            _healths![index] = new Health
            {
                Current = stats.MaxHealth,
                Max = stats.MaxHealth,
                RegenRate = 0f
            };

            _ownerTags![index] = new OwnerTag { TeamId = teamId };

            _tierInfos![index] = new TierInfo
            {
                CurrentTier = _currentTier[teamId],
                UnlockedAt = stats.RequiredTier
            };

            _collisionFlags![index] = new CollisionFlag
            {
                IsBlocking = true,
                IsStatic = true // Bina hiç hareket etmez
            };

            // BuildingConfidence'ı JSON'daki eşik değerleriyle "damgalıyoruz"
            // - hatırlarsan bunu önceden konuşmuştuk, her karede JSON'a
            // gitmek yerine bir kere buraya kopyalıyoruz.
            _buildingConfidences![index] = new BuildingConfidence
            {
                DistanceToRoad = -1f,
                ConfidenceValue = 1f,
                ProductionMultiplier = 1f,
                MaxRoadDistanceThreshold = stats.MaxRoadDistanceThreshold,
                ConfidenceDropRatePerTile = stats.ConfidenceDropRatePerTile
            };

            // Sadece tedarik binası olarak işaretlenmiş binalar bu
            // component'i anlamlı şekilde kullanır, ama hepsine 0
            // yarıçaplı bir SupplyNode vermek zararsız.
            _supplyNodes![index] = new SupplyNode
            {
                Radius = stats.IsSupplyNode ? stats.SupplyRadius : 0f,
                IsNetworkRoot = stats.IsNetworkRoot
            };

            // Haritaya (birden fazla hücreye) yerleştir.
            if (!_gridManager.PlaceBuildingFootprint(entity, originGridX, originGridY, stats.FootprintWidth, stats.FootprintHeight))
            {
                World.DestroyEntity(entity);
                return EntityHandle.Invalid;
            }

            // Diğer sistemlere haber ver.
            _buildingStateMonitor.RegisterBuilding(entity);

            if (stats.IsSupplyNode)
            {
                _logisticsConfidence.RegisterSupplyNode(entity);
            }
            else
            {
                _logisticsConfidence.RegisterBuilding(entity);
            }

            // Evre atlatan bir binaysa, takımın evresini bir üst
            // seviyeye çıkar.
            if (stats.IsTierAdvancementBuilding)
            {
                _currentTier[teamId]++;
            }

            return entity;
        }
    }
}