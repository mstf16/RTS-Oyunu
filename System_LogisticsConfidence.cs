using System;

namespace RTSProje
{
    // ============================================================
    // System_LogisticsConfidence.cs
    // ------------------------------------------------------------
    // Cep telefonu şebekesi gibi düşün: Ana üs (Town Center) her
    // zaman "çekiyor". Ona yakın bir tedarik binası da ondan sinyal
    // alıp kendi çapında sinyal yaymaya başlıyor, zincir böyle
    // yayılıyor. Bir bina bu zincire bir şekilde bağlıysa tam
    // verimli çalışıyor. Zincirden kopmuşsa yine de en yakın
    // tedarik binasına göre kısmi bir verim alabiliyor.
    //
    // Neden takım kontrolü var? Düşmanın kurduğu bir tedarik ağına
    // yaklaşıp bedavadan sinyal çalamazsın - tıpkı komşunun WiFi'ına
    // şifresiz giremeyeceğin gibi, sinyal sadece aynı takımdan
    // olanlar arasında yayılır ve fayda sağlar.
    // ============================================================
    public class System_LogisticsConfidence : ISystem
    {
        private readonly EntityHandle[] _supplyEntities = new EntityHandle[World.MaxEntities];
        private int _supplyCount;

        private readonly EntityHandle[] _buildingEntities = new EntityHandle[World.MaxEntities];
        private int _buildingCount;

        private readonly bool[] _isConnected = new bool[World.MaxEntities];
        private bool _networkDirty = true;

        private readonly EntityHandle[] _bfsQueue = new EntityHandle[World.MaxEntities];

        private Position[]? _positions;
        private BuildingConfidence[]? _buildingConfidences;
        private SupplyNode[]? _supplyNodes;
        private OwnerTag[]? _ownerTags;

        private bool _isInitialized;

        public void Initialize()
        {
            _positions = World.GetArray<Position>();
            _buildingConfidences = World.GetArray<BuildingConfidence>();
            _supplyNodes = World.GetArray<SupplyNode>();
            _ownerTags = World.GetArray<OwnerTag>();
            _isInitialized = true;

            // Bir entity yok edilince anında haberimiz olsun, aylarca
            // birikmiş ölü kayıt taramaya gerek kalmasın.
            EventManager.Subscribe<EntityDestroyedEvent>(OnEntityDestroyed);
        }

        public void Update(float deltaTime)
        {
            if (!_isInitialized) return;

            if (_networkDirty)
            {
                RecomputeNetwork();
                _networkDirty = false;
            }

            for (int b = 0; b < _buildingCount; b++)
            {
                EntityHandle building = _buildingEntities[b];

                if (!World.IsAlive(building))
                {
                    continue;
                }

                int buildingGridX = _positions![building.Index].GridX;
                int buildingGridY = _positions[building.Index].GridY;

                float nearestDistance = float.MaxValue;
                bool coveredByConnectedNode = false;

                for (int s = 0; s < _supplyCount; s++)
                {
                    EntityHandle supply = _supplyEntities[s];
                    if (!World.IsAlive(supply))
                    {
                        continue;
                    }

                    float dx = buildingGridX - _positions[supply.Index].GridX;
                    float dy = buildingGridY - _positions[supply.Index].GridY;
                    float distance = MathF.Sqrt(dx * dx + dy * dy);

                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                    }

                    bool sameTeamAsSupply = _ownerTags![building.Index].TeamId == _ownerTags[supply.Index].TeamId;

                    if (sameTeamAsSupply && _isConnected[supply.Index] && distance <= _supplyNodes![supply.Index].Radius)
                    {
                        coveredByConnectedNode = true;
                    }
                }

                float confidence;

                if (coveredByConnectedNode)
                {
                    confidence = 1f;
                }
                else if (nearestDistance == float.MaxValue)
                {
                    confidence = 0f;
                }
                else
                {
                    float threshold = _buildingConfidences![building.Index].MaxRoadDistanceThreshold;
                    float dropRate = _buildingConfidences[building.Index].ConfidenceDropRatePerTile;

                    if (nearestDistance <= threshold)
                    {
                        confidence = 1f;
                    }
                    else
                    {
                        confidence = 1f - (nearestDistance - threshold) * dropRate;
                        if (confidence < 0f)
                        {
                            confidence = 0f;
                        }
                    }
                }

                _buildingConfidences![building.Index].DistanceToRoad = nearestDistance == float.MaxValue ? -1f : nearestDistance;
                _buildingConfidences[building.Index].ConfidenceValue = confidence;
            }
        }

        public void Shutdown()
        {
            EventManager.Unsubscribe<EntityDestroyedEvent>(OnEntityDestroyed);
        }

        // İsimlendirilmiş metod (lambda değil) - bir entity öldüğünde
        // hem bina hem tedarik defterinden anında siliyoruz.
        private void OnEntityDestroyed(EntityDestroyedEvent e)
        {
            UnregisterBuilding(e.Handle);
            UnregisterSupplyNode(e.Handle);
        }

        private void RecomputeNetwork()
        {
            for (int i = 0; i < World.MaxEntities; i++)
            {
                _isConnected[i] = false;
            }

            int queueHead = 0;
            int queueTail = 0;

            for (int s = 0; s < _supplyCount; s++)
            {
                EntityHandle supply = _supplyEntities[s];
                if (!World.IsAlive(supply)) continue;

                if (_supplyNodes![supply.Index].IsNetworkRoot)
                {
                    _isConnected[supply.Index] = true;
                    _bfsQueue[queueTail] = supply;
                    queueTail++;
                }
            }

            while (queueHead < queueTail)
            {
                EntityHandle current = _bfsQueue[queueHead];
                queueHead++;

                int currentGridX = _positions![current.Index].GridX;
                int currentGridY = _positions[current.Index].GridY;
                float currentRadius = _supplyNodes![current.Index].Radius;

                for (int s = 0; s < _supplyCount; s++)
                {
                    EntityHandle candidate = _supplyEntities[s];
                    if (!World.IsAlive(candidate) || _isConnected[candidate.Index])
                    {
                        continue;
                    }

                    float dx = currentGridX - _positions[candidate.Index].GridX;
                    float dy = currentGridY - _positions[candidate.Index].GridY;
                    float distance = MathF.Sqrt(dx * dx + dy * dy);

                    bool sameTeam = _ownerTags![current.Index].TeamId == _ownerTags[candidate.Index].TeamId;

                    if (sameTeam && (distance <= currentRadius || distance <= _supplyNodes[candidate.Index].Radius))
                    {
                        _isConnected[candidate.Index] = true;
                        _bfsQueue[queueTail] = candidate;
                        queueTail++;
                    }
                }
            }
        }
// Aynı bina yanlışlıkla iki kere kaydedilmeye çalışılırsa ikisini de kaydetme - deftere aynı isim iki kere yazılmasın.
        public void RegisterSupplyNode(EntityHandle supply)
        {
            for (int i = 0; i < _supplyCount; i++)
            {
                if (_supplyEntities[i].Equals(supply))
                {
                    return;
                }
            }

            if (_supplyCount >= _supplyEntities.Length) return;

            _supplyEntities[_supplyCount] = supply;
            _supplyCount++;
            _networkDirty = true;
        }

        public void UnregisterSupplyNode(EntityHandle supply)
        {
            for (int i = 0; i < _supplyCount; i++)
            {
                if (_supplyEntities[i].Equals(supply))
                {
                    _supplyCount--;
                    _supplyEntities[i] = _supplyEntities[_supplyCount];
                    _networkDirty = true;
                    return;
                }
            }
        }

        public void RegisterBuilding(EntityHandle building)
        {
            // Aynı bina yanlışlıkla iki kere kaydedilmeye çalışılırsa
            // sessizce reddet - deftere aynı isim iki kere yazılmasın.
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
            bool anySupplyRemoved = false;

            for (int i = _buildingCount - 1; i >= 0; i--)
            {
                if (!World.IsAlive(_buildingEntities[i]))
                {
                    _buildingCount--;
                    _buildingEntities[i] = _buildingEntities[_buildingCount];
                }
            }

            for (int i = _supplyCount - 1; i >= 0; i--)
            {
                if (!World.IsAlive(_supplyEntities[i]))
                {
                    _supplyCount--;
                    _supplyEntities[i] = _supplyEntities[_supplyCount];
                    anySupplyRemoved = true;
                }
            }

            if (anySupplyRemoved)
            {
                _networkDirty = true;
            }
        }
    }
}