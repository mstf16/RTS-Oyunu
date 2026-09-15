using System;

namespace RTSProje
{
    // ============================================================
    // System_FormationMovement.cs
    // ------------------------------------------------------------
    // Bir askeri taburu düşün: hızlı bir er, yavaş bir topçuyu
    // geride bırakıp tek başına düşmana dalmaz - tabur birlikte,
    // en yavaş üyenin adımına göre ilerler. Bu sistem, aynı gruba
    // (FormationMember.GroupId) ait birimlerin hepsinin hızını,
    // gruptaki EN YAVAŞ üyenin hızına eşitliyor.
    //
    // Neden WorkerBehavior'daki hareket koduyla AYNI değil?
    // İşçiler tek başına, bağımsız çalışır - her biri kendi hızında
    // gidip gelir, "grup düzeni" onlar için anlamsız. Ama savaş
    // birimleri komutla birlikte hareket eder, birbirini geride
    // bırakmamalı. İki farklı davranış, iki farklı sistem.
    // ============================================================
    public class System_FormationMovement : ISystem
    {
        // Aynı anda var olabilecek maksimum tabur (grup) sayısı.
        // Grup kimlikleri (GroupId) 0 ile bu sayı arasında olmalı.
        private const int MaxGroups = 64;

        // Bir birimin "grubu yok" durumunu temsil eden değer.
        public const int NoGroup = -1;

        private const float ArrivalDistance = 4f;

        // Her karede sıfırdan hesaplanan, o karedeki her grubun
        // en yavaş üyesinin hızı. Zero-GC kuralı gereği sabit boyutlu
        // dizi olarak baştan ayrılmış, her karede İÇERİĞİ sıfırlanıyor.
        private readonly float[] _groupMinSpeed = new float[MaxGroups];
        private readonly bool[] _groupHasMembers = new bool[MaxGroups];

        private readonly EntityHandle[] _unitEntities = new EntityHandle[World.MaxEntities];
        private int _unitCount;

        private readonly GridManager _gridManager;

        private Position[]? _positions;
        private MovementSpeed[]? _movementSpeeds;
        private FormationMember[]? _formationMembers;

        private bool _isInitialized;

        public System_FormationMovement(GridManager gridManager)
        {
            _gridManager = gridManager;
        }

        public void Initialize()
        {
            _positions = World.GetArray<Position>();
            _movementSpeeds = World.GetArray<MovementSpeed>();
            _formationMembers = World.GetArray<FormationMember>();
            _isInitialized = true;

            EventManager.Subscribe<EntityDestroyedEvent>(OnEntityDestroyed);
        }

        public void Update(float deltaTime)
        {
            if (!_isInitialized) return;

            // 1. TUR: Her grubun en yavaş üyesini bul.
            for (int g = 0; g < MaxGroups; g++)
            {
                _groupMinSpeed[g] = float.MaxValue;
                _groupHasMembers[g] = false;
            }

            for (int i = 0; i < _unitCount; i++)
            {
                EntityHandle unit = _unitEntities[i];
                if (!World.IsAlive(unit)) continue;

                int groupId = _formationMembers![unit.Index].GroupId;
                if (groupId < 0 || groupId >= MaxGroups) continue; // Grupsuz birim - bu turu atla

                float speed = _movementSpeeds![unit.Index].CurrentSpeed;
                if (speed < _groupMinSpeed[groupId])
                {
                    _groupMinSpeed[groupId] = speed;
                }
                _groupHasMembers[groupId] = true;
            }

            // 2. TUR: Her birimi, kendi hedefine doğru, GRUBUN hızıyla
            // (grupsuzsa kendi hızıyla) bir adım ilerlet.
            for (int i = 0; i < _unitCount; i++)
            {
                EntityHandle unit = _unitEntities[i];
                if (!World.IsAlive(unit)) continue;

                int groupId = _formationMembers![unit.Index].GroupId;

                float effectiveSpeed;
                if (groupId >= 0 && groupId < MaxGroups && _groupHasMembers[groupId])
                {
                    effectiveSpeed = _groupMinSpeed[groupId];
                }
                else
                {
                    effectiveSpeed = _movementSpeeds![unit.Index].CurrentSpeed;
                }

                int targetGridX = _positions![unit.Index].TargetGridX;
                int targetGridY = _positions[unit.Index].TargetGridY;

                MoveEntityToward(unit.Index, targetGridX, targetGridY, effectiveSpeed, deltaTime);
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
        // HAREKET (WorkerBehavior'daki ile aynı mantık, ama hız
        // dışarıdan - grup hesabından - geliyor)
        // ------------------------------------------------------------
        private void MoveEntityToward(int entityIndex, int targetGridX, int targetGridY, float speed, float deltaTime)
        {
            _gridManager.GridToPixel(targetGridX, targetGridY, out float targetPixelX, out float targetPixelY);

            ref Position position = ref _positions![entityIndex];

            float dx = targetPixelX - position.PixelX;
            float dy = targetPixelY - position.PixelY;
            float distance = MathF.Sqrt(dx * dx + dy * dy);

            if (distance <= ArrivalDistance)
            {
                position.PixelX = targetPixelX;
                position.PixelY = targetPixelY;
                position.GridX = targetGridX;
                position.GridY = targetGridY;
                return;
            }

            float step = speed * deltaTime * 32f; // 32 = bir hücrenin yaklaşık piksel birimi

            if (step >= distance)
            {
                position.PixelX = targetPixelX;
                position.PixelY = targetPixelY;
            }
            else
            {
                position.PixelX += (dx / distance) * step;
                position.PixelY += (dy / distance) * step;
            }
        }


        // ------------------------------------------------------------
        // TABUR (GRUP) YÖNETİMİ
        // ------------------------------------------------------------

        // Bir birimi belirli bir tabura katar. groupId, 0 ile MaxGroups
        // arasında OYUNCUNUN/AI'ın kendi seçtiği bir numara olur - bu
        // sistem grup numaralarını kendisi üretmez, sadece takip eder.
        public void AssignToGroup(EntityHandle unit, int groupId)
        {
            if (!World.IsAlive(unit)) return;
            if (groupId < 0 || groupId >= MaxGroups) return;

            _formationMembers![unit.Index].GroupId = groupId;
        }

        // Bir birimi tüm gruplardan çıkarır - artık kendi başına yürür.
        public void RemoveFromGroup(EntityHandle unit)
        {
            if (!World.IsAlive(unit)) return;
            _formationMembers![unit.Index].GroupId = NoGroup;
        }

        public void SetTarget(EntityHandle unit, int targetGridX, int targetGridY)
        {
            if (!World.IsAlive(unit)) return;

            _positions![unit.Index].TargetGridX = targetGridX;
            _positions[unit.Index].TargetGridY = targetGridY;
        }


        // ------------------------------------------------------------
        // KAYIT METOTLARI
        // ------------------------------------------------------------
        public void RegisterUnit(EntityHandle unit)
        {
            for (int i = 0; i < _unitCount; i++)
            {
                if (_unitEntities[i].Equals(unit)) return;
            }

            if (_unitCount >= _unitEntities.Length) return;

            _formationMembers![unit.Index].GroupId = NoGroup;
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