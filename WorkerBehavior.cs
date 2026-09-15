using System;

namespace RTSProje
{
    // ============================================================
    // WorkerBehavior.cs
    // ------------------------------------------------------------
    // İşçi döngüsünü yöneten sistem: 
    // Kaynağa Git -> Topla -> Depoya Dön -> Boşalt -> Tekrarla.
    // ============================================================
    public class WorkerBehavior : ISystem
    {
        private const float ArrivalDistance = 4f;

        private readonly EntityHandle[] _workerEntities = new EntityHandle[World.MaxEntities];
        private int _workerCount;

        private readonly EntityHandle[] _nodeEntities = new EntityHandle[World.MaxEntities];
        private int _nodeCount;

        private readonly GridManager _gridManager;
        private readonly EconomyManager _economyManager;

        private Position[]? _positions;
        private MovementSpeed[]? _movementSpeeds;
        private ResourceCarrier[]? _resourceCarriers;
        private WorkerState[]? _workerStates;
        private ResourceDeposit[]? _resourceDeposits;
        private OwnerTag[]? _ownerTags;

        private bool _isInitialized;

        public WorkerBehavior(GridManager gridManager, EconomyManager economyManager)
        {
            _gridManager = gridManager;
            _economyManager = economyManager;
        }

        public void Initialize()
        {
            _positions = World.GetArray<Position>();
            _movementSpeeds = World.GetArray<MovementSpeed>();
            _resourceCarriers = World.GetArray<ResourceCarrier>();
            _workerStates = World.GetArray<WorkerState>();
            _resourceDeposits = World.GetArray<ResourceDeposit>();
            _ownerTags = World.GetArray<OwnerTag>();
            _isInitialized = true;

            EventManager.Subscribe<EntityDestroyedEvent>(OnEntityDestroyed);
        }

        public void Update(float deltaTime)
        {
            if (!_isInitialized) return;

            // 1. Kaynak yataklarının tazelenmesini işle
            for (int n = 0; n < _nodeCount; n++)
            {
                EntityHandle node = _nodeEntities[n];
                if (!World.IsAlive(node)) continue;

                ref ResourceDeposit deposit = ref _resourceDeposits![node.Index];
                if (!deposit.RegenPaused && deposit.CurrentAmount < deposit.MaxAmount)
                {
                    deposit.CurrentAmount += deposit.RegenRatePerSecond * deltaTime;
                    if (deposit.CurrentAmount > deposit.MaxAmount) deposit.CurrentAmount = deposit.MaxAmount;
                }
            }

            // 2. Her işçinin durumunu güncelle
            for (int i = 0; i < _workerCount; i++)
            {
                EntityHandle worker = _workerEntities[i];
                if (!World.IsAlive(worker)) continue;

                ProcessWorker(worker, deltaTime);
            }
        }

        public void Shutdown()
        {
            EventManager.Unsubscribe<EntityDestroyedEvent>(OnEntityDestroyed);
        }

        private void OnEntityDestroyed(EntityDestroyedEvent e)
        {
            UnregisterWorker(e.Handle);
            UnregisterResourceNode(e.Handle);
        }

        private void ProcessWorker(EntityHandle worker, float deltaTime)
        {
            ref WorkerState state = ref _workerStates![worker.Index];

            switch (state.CurrentState)
            {
                case WorkerStateType.Idle:
                    if (state.AssignedNode.IsValid && World.IsAlive(state.AssignedNode))
                        state.CurrentState = WorkerStateType.MovingToNode;
                    break;

                case WorkerStateType.MovingToNode:
                    if (!World.IsAlive(state.AssignedNode)) { state.CurrentState = WorkerStateType.Idle; break; }
                    
                    int nodeX = _positions![state.AssignedNode.Index].GridX;
                    int nodeY = _positions[state.AssignedNode.Index].GridY;

                    if (MoveEntityToward(worker.Index, nodeX, nodeY, deltaTime))
                        state.CurrentState = WorkerStateType.Harvesting;
                    break;

                case WorkerStateType.Harvesting:
                    ProcessHarvesting(worker, ref state, deltaTime);
                    break;

                case WorkerStateType.MovingToDropoff:
                    if (!World.IsAlive(state.AssignedDropoff)) { state.CurrentState = WorkerStateType.Idle; break; }

                    int dropX = _positions![state.AssignedDropoff.Index].GridX;
                    int dropY = _positions[state.AssignedDropoff.Index].GridY;

                    if (MoveEntityToward(worker.Index, dropX, dropY, deltaTime))
                        state.CurrentState = WorkerStateType.Depositing;
                    break;

                case WorkerStateType.Depositing:
                    ProcessDepositing(worker, ref state);
                    break;
            }
        }

        private void ProcessHarvesting(EntityHandle worker, ref WorkerState state, float deltaTime)
        {
            if (!World.IsAlive(state.AssignedNode)) { state.CurrentState = WorkerStateType.Idle; return; }

            ref ResourceDeposit deposit = ref _resourceDeposits![state.AssignedNode.Index];
            ref ResourceCarrier carrier = ref _resourceCarriers![worker.Index];

            float rate = deposit.CurrentAmount <= deposit.DepletionThreshold 
                ? deposit.FullYieldRate * deposit.DepletedYieldMultiplier 
                : deposit.FullYieldRate;

            float wanted = rate * deltaTime;
            float extracted = MathF.Min(wanted, MathF.Min(deposit.CurrentAmount, carrier.Capacity - carrier.CarriedAmount));

            deposit.CurrentAmount -= extracted;
            carrier.CarriedAmount += extracted;
            carrier.CarriedResourceType = deposit.Type;

            if (carrier.CarriedAmount >= carrier.Capacity || deposit.CurrentAmount <= 0f)
                state.CurrentState = WorkerStateType.MovingToDropoff;
        }

        private void ProcessDepositing(EntityHandle worker, ref WorkerState state)
        {
            ref ResourceCarrier carrier = ref _resourceCarriers![worker.Index];
            int teamId = _ownerTags![worker.Index].TeamId;

            _economyManager.AddResource(teamId, carrier.CarriedResourceType, carrier.CarriedAmount);
            carrier.CarriedAmount = 0f;
            carrier.CarriedResourceType = ResourceType.None;

            if (World.IsAlive(state.AssignedNode) && _resourceDeposits![state.AssignedNode.Index].CurrentAmount > 0f)
                state.CurrentState = WorkerStateType.MovingToNode;
            else
                state.CurrentState = WorkerStateType.Idle;
        }

        private bool MoveEntityToward(int entityIndex, int targetGridX, int targetGridY, float deltaTime)
        {
            _gridManager.GridToPixel(targetGridX, targetGridY, out float targetPixelX, out float targetPixelY);
            ref Position position = ref _positions![entityIndex];

            float dx = targetPixelX - position.PixelX;
            float dy = targetPixelY - position.PixelY;
            float distance = MathF.Sqrt(dx * dx + dy * dy);

            if (distance <= ArrivalDistance)
            {
                position.PixelX = targetPixelX; position.PixelY = targetPixelY;
                position.GridX = targetGridX; position.GridY = targetGridY;
                return true;
            }

            float step = _movementSpeeds![entityIndex].CurrentSpeed * deltaTime * 32f;
            if (step >= distance)
            {
                position.PixelX = targetPixelX; position.PixelY = targetPixelY;
            }
            else
            {
                position.PixelX += (dx / distance) * step;
                position.PixelY += (dy / distance) * step;
            }
            return false;
        }

        public void RegisterWorker(EntityHandle worker)
        {
            for (int i = 0; i < _workerCount; i++) if (_workerEntities[i].Equals(worker)) return;
            if (_workerCount >= _workerEntities.Length) return;
            _workerEntities[_workerCount++] = worker;
        }

        public void UnregisterWorker(EntityHandle worker)
        {
            for (int i = 0; i < _workerCount; i++)
                if (_workerEntities[i].Equals(worker)) { _workerCount--; _workerEntities[i] = _workerEntities[_workerCount]; return; }
        }

        public void RegisterResourceNode(EntityHandle node)
        {
            for (int i = 0; i < _nodeCount; i++) if (_nodeEntities[i].Equals(node)) return;
            if (_nodeCount >= _nodeEntities.Length) return;
            _nodeEntities[_nodeCount++] = node;
        }

        public void UnregisterResourceNode(EntityHandle node)
        {
            for (int i = 0; i < _nodeCount; i++)
                if (_nodeEntities[i].Equals(node)) { _nodeCount--; _nodeEntities[i] = _nodeEntities[_nodeCount]; return; }
        }

        public void AssignWorker(EntityHandle worker, EntityHandle node, EntityHandle dropoff)
        {
            if (!World.IsAlive(worker)) return;
            ref WorkerState state = ref _workerStates![worker.Index];
            state.AssignedNode = node;
            state.AssignedDropoff = dropoff;
            state.CurrentState = WorkerStateType.Idle;
        }

        // ------------------------------------------------------------
        // KAYNAK YATAĞI DOĞURMA (SPAWN RESOURCE NODE)
        // Bir taş ocağı ya da tarla parçası oluşturur. EconomyManager'daki
        // SpawnUnit/SpawnBuilding'e benzer ama daha basit - kaynak
        // yatağının canı, savaş değeri gibi şeyleri yok, sadece
        // konumu ve ne kadar/nasıl üretim yaptığı var.
        // ------------------------------------------------------------
        public EntityHandle SpawnResourceNode(
            ResourceType type,
            int gridX,
            int gridY,
            float maxAmount,
            float fullYieldRate,
            float depletionThreshold,
            float depletedYieldMultiplier,
            float regenRatePerSecond)
        {
            EntityHandle entity = World.CreateEntity();
            if (!entity.IsValid) return EntityHandle.Invalid;

            int index = entity.Index;

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

            _resourceDeposits![index] = new ResourceDeposit
            {
                Type = type,
                CurrentAmount = maxAmount,
                MaxAmount = maxAmount,
                DepletionThreshold = depletionThreshold,
                FullYieldRate = fullYieldRate,
                DepletedYieldMultiplier = depletedYieldMultiplier,
                RegenRatePerSecond = regenRatePerSecond,
                RegenPaused = false
            };

            // Kaynak yatağı yolu tıkamaz - işçi üzerine/içine girip
            // toplayabilsin diye engel olarak işaretlenmiyor.
            CollisionFlag[] collisionFlags = World.GetArray<CollisionFlag>();
            collisionFlags[index] = new CollisionFlag { IsBlocking = false, IsStatic = true };

            RegisterResourceNode(entity);
            return entity;
        }

        public void SetNodeRegenPaused(EntityHandle node, bool paused)
        {
            if (!World.IsAlive(node)) return;
            _resourceDeposits![node.Index].RegenPaused = paused;
        }
    }
}
