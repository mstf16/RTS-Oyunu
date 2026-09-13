using System;

namespace RTSProje
{
    // ============================================================
    // BuildingStateMonitor.cs
    // ------------------------------------------------------------
    // Bir jeneratör düşün: göstergesi yakıt seviyesini gösterir
    // ama fabrikadaki makinelerin hızını o göstergeye göre AYARLAYAN
    // ayrı bir vardiya amiri lazımdır.
    // ============================================================
    public class BuildingStateMonitor : ISystem
    {
        private readonly EntityHandle[] _buildingEntities = new EntityHandle[World.MaxEntities];
        private int _buildingCount;

        private const float ShutdownThreshold = 0.05f;

        private BuildingConfidence[]? _buildingConfidences;

        private bool _isInitialized;

        public void Initialize()
        {
            _buildingConfidences = World.GetArray<BuildingConfidence>();
            _isInitialized = true;
            EventManager.Subscribe<EntityDestroyedEvent>(OnEntityDestroyed);
        }

        public void Update(float deltaTime)
        {
            if (!_isInitialized) return;

            for (int i = 0; i < _buildingCount; i++)
            {
                EntityHandle building = _buildingEntities[i];

                if (!World.IsAlive(building))
                {
                    continue;
                }

                float confidence = _buildingConfidences![building.Index].ConfidenceValue;
                float multiplier = confidence;

                if (confidence < ShutdownThreshold)
                {
                    multiplier = 0f;
                }

                _buildingConfidences[building.Index].ProductionMultiplier = multiplier;
            }
        }

        public void Shutdown()
        {
            EventManager.Unsubscribe<EntityDestroyedEvent>(OnEntityDestroyed);
        }

        private void OnEntityDestroyed(EntityDestroyedEvent e)
        {
            UnregisterBuilding(e.Handle);
        }

        public void RegisterBuilding(EntityHandle building)
        {
            for (int i = 0; i < _buildingCount; i++)
            {
                if (_buildingEntities[i].Equals(building))
                {
                    return;
                }
            }

            if (_buildingCount >= _buildingEntities.Length) return;

            _buildingEntities[_buildingCount] = building;
            _buildingCount++;
        }

        public void UnregisterBuilding(EntityHandle building)
        {
            for (int i = 0; i < _buildingCount; i++)
            {
                if (_buildingEntities[i].Equals(building))
                {
                    _buildingCount--;
                    _buildingEntities[i] = _buildingEntities[_buildingCount];
                    return;
                }
            }
        }

        public void PruneDeadEntries()
        {
            for (int i = _buildingCount - 1; i >= 0; i--)
            {
                if (!World.IsAlive(_buildingEntities[i]))
                {
                    _buildingCount--;
                    _buildingEntities[i] = _buildingEntities[_buildingCount];
                }
            }
        }
    }
}