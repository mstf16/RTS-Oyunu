using System;

namespace RTSProje
{
    // ============================================================
    // TerrainPhysics.cs
    // ------------------------------------------------------------
    // Bir asker düşün: asfalt yolda tabur halinde tempolu yürüyor,
    // ama aynı asker çamurlu bir tarlaya girdiğinde her adımda
    // botları çamura saplanıyor, ilerlemesi yavaşlıyor. Bu sistem
    // tam olarak bunu hesaplıyor: birimin şu an hangi zeminde
    // durduğuna bakıp, MovementSpeed.CurrentSpeed'i ona göre ayarlıyor.
    //
    // Neden bu bilgiyi her karede tekrar hesaplıyoruz, bir kere
    // hesaplayıp saklamıyoruz? Çünkü bir birim sürekli hareket
    // halinde - bir kare çimende, sonraki karede yolda olabilir.
    // Zemin bilgisi anlık ve sürekli değişen bir şey, bu yüzden
    // "doğumda bir kere damgala" mantığı (BuildingConfidence'taki
    // gibi) burada işlemez.
    // ============================================================
    public class TerrainPhysics : ISystem
    {
        // Bu sistem de kendi birim defterini tutuyor - hangi
        // entity'lerin hareket eden birim olduğunu bilmemiz lazım,
        // yoksa binalar gibi hiç hareket etmeyen şeyleri de boşuna
        // işlemeye kalkarız.
        private readonly EntityHandle[] _unitEntities = new EntityHandle[World.MaxEntities];
        private int _unitCount;

        private readonly GridManager _gridManager;

        private Position[]? _positions;
        private MovementSpeed[]? _movementSpeeds;

        private bool _isInitialized;

        public TerrainPhysics(GridManager gridManager)
        {
            _gridManager = gridManager;
        }

        public void Initialize()
        {
            _positions = World.GetArray<Position>();
            _movementSpeeds = World.GetArray<MovementSpeed>();
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

                TerrainType terrain = _gridManager.GetTerrainType(gridX, gridY);
                float multiplier = GetMultiplierForTerrain(terrain);

                _movementSpeeds![unit.Index].TerrainMultiplier = multiplier;
                _movementSpeeds[unit.Index].CurrentSpeed = _movementSpeeds[unit.Index].BaseSpeed * multiplier;
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

        // ------------------------------------------------------------
        // ZEMİN ÇARPANLARI
        // İleride bu sayıları dengelemek istersen değişecek TEK yer
        // burası - kod içinde başka hiçbir yerde bu sayılar tekrar
        // yazılmıyor.
        // ------------------------------------------------------------
        private static float GetMultiplierForTerrain(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Road:
                    return 1.3f;
                case TerrainType.Swamp:
                    return 0.5f;
                case TerrainType.Water:
                    return 0.05f;
                case TerrainType.Grass:
                default:
                    return 1.0f;
            }
        }

        // ------------------------------------------------------------
        // KAYIT METOTLARI
        // ------------------------------------------------------------
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