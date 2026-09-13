using System;

namespace RTSProje
{
    // ============================================================
    // System_FogOfWar.cs
    // ------------------------------------------------------------
    // Elinde feneri olan bir bekçi düşün: ana yoldayken feneri
    // gür yanar, geniş bir çap aydınlatır. Ama ıssız, tedarik
    // ağından kopuk bir sokağa daldıkça korkudan feneri titrer,
    // ışık çapı daralır. Bu sistem tam olarak bunu hesaplıyor -
    // bir birimin tedarik ağına ne kadar yakın/bağlı olduğuna göre
    // VisionRadius.CurrentRadius'unu büyütüp küçültüyor.
    //
    // Neden System_LogisticsConfidence'ın hesabını tekrar yazmıyoruz?
    // Aynı BFS'i iki kere hesaplamak hem gereksiz iş hem de iki
    // sistemin birbirinden farklı sonuç üretme riski taşır. Bunun
    // yerine LogisticsConfidence'ın açtığı iki sorgu kapısını
    // (IsPositionFullyCovered, GetNearestSupplyDistance) kullanıyoruz.
    // ============================================================
    public class System_FogOfWar : ISystem
    {
        // Görüşü tam kapsama dışında ne kadar daralacağını belirleyen
        // sabitler. Buradaki sayılar değişirse, tüm oyunun "sis"
        // hissi değişir - dengeleme yaparken değişecek TEK yer burası.
        private const float MaxUncoveredDistance = 10f;   // Bu mesafeden sonra görüş minimuma iner
        private const float MinVisionMultiplier = 0.3f;   // Görüş asla sıfıra inmez, en kötü %30 kalır

        private readonly EntityHandle[] _unitEntities = new EntityHandle[World.MaxEntities];
        private int _unitCount;

        private readonly System_LogisticsConfidence _logisticsConfidence;

        private Position[]? _positions;
        private VisionRadius[]? _visionRadii;
        private OwnerTag[]? _ownerTags;

        private bool _isInitialized;

        public System_FogOfWar(System_LogisticsConfidence logisticsConfidence)
        {
            _logisticsConfidence = logisticsConfidence;
        }

        public void Initialize()
        {
            _positions = World.GetArray<Position>();
            _visionRadii = World.GetArray<VisionRadius>();
            _ownerTags = World.GetArray<OwnerTag>();
            _isInitialized = true;

            EventManager.Subscribe<EntityDestroyedEvent>(OnEntityDestroyed);
        }

        public void Update(float deltaTime)
        {
            if (!_isInitialized) return;

            for (int i = 0; i < _unitCount; i++)
            {
                EntityHandle unit = _unitEntities[i];

                if (!World.IsAlive(unit))
                {
                    continue;
                }

                int gridX = _positions![unit.Index].GridX;
                int gridY = _positions[unit.Index].GridY;
                int teamId = _ownerTags![unit.Index].TeamId;

                float visionMultiplier;

                if (_logisticsConfidence.IsPositionFullyCovered(gridX, gridY, teamId))
                {
                    // Tam kapsama altında - fener gür yanıyor.
                    visionMultiplier = 1f;
                }
                else
                {
                    float distance = _logisticsConfidence.GetNearestSupplyDistance(gridX, gridY, teamId);

                    if (distance == float.MaxValue)
                    {
                        // Ortalıkta hiç tedarik binası yok - en karanlık durum.
                        visionMultiplier = MinVisionMultiplier;
                    }
                    else
                    {
                        float t = distance / MaxUncoveredDistance;
                        if (t > 1f) t = 1f;

                        // Mesafe arttıkça 1'den MinVisionMultiplier'a doğru
                        // yumuşakça iniyor (doğrusal geçiş).
                        visionMultiplier = 1f - (1f - MinVisionMultiplier) * t;
                    }
                }

                _visionRadii![unit.Index].CurrentRadius = _visionRadii[unit.Index].BaseRadius * visionMultiplier;
            }
        }

        public void Shutdown()
        {
            EventManager.Unsubscribe<EntityDestroyedEvent>(OnEntityDestroyed);
        }

        private void OnEntityDestroyed(EntityDestroyedEvent e)
        {
            UnregisterUnit(e.Handle);
        }

        public void RegisterUnit(EntityHandle unit)
        {
            for (int i = 0; i < _unitCount; i++)
            {
                if (_unitEntities[i].Equals(unit))
                {
                    return;
                }
            }

            if (_unitCount >= _unitEntities.Length) return;

            _unitEntities[_unitCount] = unit;
            _unitCount++;
        }

        public void UnregisterUnit(EntityHandle unit)
        {
            for (int i = 0; i < _unitCount; i++)
            {
                if (_unitEntities[i].Equals(unit))
                {
                    _unitCount--;
                    _unitEntities[i] = _unitEntities[_unitCount];
                    return;
                }
            }
        }
    }
}