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

                float nearestDistSq = float.MaxValue;
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
                    float distSq = dx * dx + dy * dy; // Karekök YOK! Saf çarpma ve toplama.

                    if (distSq < nearestDistSq)
                    {
                        nearestDistSq = distSq;
                    }

                    bool sameTeamAsSupply = _ownerTags![building.Index].TeamId == _ownerTags[supply.Index].TeamId;
                    float supplyRadius = _supplyNodes![supply.Index].Radius;

                    // Mesafe menzilden küçük mü? dist <= R yerine distSq <= R * R
                    if (sameTeamAsSupply && _isConnected[supply.Index] && distSq <= (supplyRadius * supplyRadius))
                    {
                        coveredByConnectedNode = true;
                    }
                }

                float confidence;
                float nearestDistance = float.MaxValue;

                if (coveredByConnectedNode)
                {
                    confidence = 1f;
                    // Şebekeye tam bağlıysa metre hesabı kritik değil ama güven tam
                    nearestDistance = nearestDistSq == float.MaxValue ? -1f : MathF.Sqrt(nearestDistSq);
                }
                else if (nearestDistSq == float.MaxValue)
                {
                    confidence = 0f;
                    nearestDistance = -1f;
                }
                else
                {
                    // YALNIZCA şebekeden kopuksa ve ceza puanı hesaplanacaksa TEK BİR KEZ karekök alıyoruz!
                    nearestDistance = MathF.Sqrt(nearestDistSq);

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

                _buildingConfidences![building.Index].DistanceToRoad = nearestDistance;
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
                    float distSq = dx * dx + dy * dy;

                    bool sameTeam = _ownerTags![current.Index].TeamId == _ownerTags[candidate.Index].TeamId;
                    float candRadius = _supplyNodes[candidate.Index].Radius;

                    // Mesafe iki düğümden birinin menziline giriyor mu? (Karesel kıyaslama)
                    if (sameTeam && (distSq <= (currentRadius * currentRadius) || distSq <= (candRadius * candRadius)))
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

        // ------------------------------------------------------------
        // DIŞARIYA AÇIK SORGU KAPILARI
        // System_FogOfWar gibi başka sistemlerin, aynı BFS hesabını
        // tekrar yazmadan bu sınıfın zaten bildiği tedarik ağı
        // bilgisini sorabilmesi için.
        // ------------------------------------------------------------

        // Bu nokta, aynı takımdan bağlı bir tedarik binasının
        // menzili içinde mi? (Tam kapsama = "şebeke tam çekiyor")
        public bool IsPositionFullyCovered(int gridX, int gridY, int teamId)
        {
            for (int s = 0; s < _supplyCount; s++)
            {
                EntityHandle supply = _supplyEntities[s];
                if (!World.IsAlive(supply)) continue;
                if (_ownerTags![supply.Index].TeamId != teamId) continue;
                if (!_isConnected[supply.Index]) continue;

                float dx = gridX - _positions![supply.Index].GridX;
                float dy = gridY - _positions[supply.Index].GridY;
                float distSq = dx * dx + dy * dy; // Karekök YOK

                float radius = _supplyNodes![supply.Index].Radius;
                if (distSq <= radius * radius)
                {
                    return true;
                }
            }

            return false;
        }

        // Bu noktaya, aynı takımdan (bağlı olsun olmasın) en yakın
        // tedarik binası ne kadar uzakta? Hiç yoksa float.MaxValue döner.
        public float GetNearestSupplyDistance(int gridX, int gridY, int teamId)
        {
            float nearestSq = float.MaxValue;

            for (int s = 0; s < _supplyCount; s++)
            {
                EntityHandle supply = _supplyEntities[s];
                if (!World.IsAlive(supply)) continue;
                if (_ownerTags![supply.Index].TeamId != teamId) continue;

                float dx = gridX - _positions![supply.Index].GridX;
                float dy = gridY - _positions[supply.Index].GridY;
                float distSq = dx * dx + dy * dy; // Karekök YOK

                if (distSq < nearestSq)
                {
                    nearestSq = distSq;
                }
            }

            // Çağıran taraf (System_FogOfWar) gerçek mesafe değeriyle
            // hesap yapıyor - burada TEK BİR KERE, döngü BİTTİKTEN
            // SONRA kök alıyoruz.
            return nearestSq == float.MaxValue ? float.MaxValue : MathF.Sqrt(nearestSq);
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