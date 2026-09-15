using System;

namespace RTSProje
{
    // ============================================================
    // System_DynamicSynergy.cs
    // ------------------------------------------------------------
    // Kale surunda nöbet tutan askerleri düşün: okçular surun
    // içinde kalkanlı muhafızların arkasında dururken, ok yağmuruna
    // rahatça dayanır (görünmez zırh kazanır). Ama okçuları öne,
    // korumasız bırakırsan aynı okçular tir tir titrer, tek darbede
    // devrilir. Bu sistem tam olarak bunu hesaplıyor - bir birimin
    // etrafındaki yakın dövüşçü/menzilli oranına bakıp anlık zırhını
    // ayarlıyor.
    //
    // Neden her karede SIFIRDAN hesaplıyoruz, üstüne eklemiyoruz?
    // CombatStats.BaseArmorValue hiç değişmeyen, doğumda verilen
    // sabit değer. Her karede "BaseArmorValue + bonus" ya da
    // "BaseArmorValue - ceza" diye YENİDEN hesaplıyoruz. Üstüne
    // üstüne eklemiş olsaydık, bir asker 10 kare aynı yerde durunca
    // zırhı 10 kat artardı - bu bariz bir hata olurdu.
    // ============================================================
    public class System_DynamicSynergy : ISystem
    {
        // "Yakın" sayılmak için bir birimin diğerine en fazla kaç
        // hücre uzakta olması gerektiği. Kare alınmış hali kullanılıyor
        // ki karşılaştırmalarda kare kök almaya hiç gerek kalmasın.
        private const float ScanRadius = 3f;
        private const float ScanRadiusSquared = ScanRadius * ScanRadius;

        // Bir menzilli birim, yanında en az 1 yakın dövüşçü varsa
        // bu kadar EK zırh kazanır - "korunuyorum" hissi.
        private const float MeleeProtectionArmorBonus = 3f;

        // Bir menzilli birim HİÇ yakın dövüşçü koruması yoksa bu
        // kadar zırh KAYBEDER - "açıktayım, korkuyorum" hissi.
        private const float ExposedRangedArmorPenalty = 2f;

        // Bir elit birimin yanında bu sayıdan FAZLA başka elit
        // birim varsa, "kalabalıkta organizasyon bozuluyor" diye
        // saldırı hızı yavaşlar (cooldown uzar).
        private const int EliteClusterThreshold = 2;
        private const float EliteClusterCooldownMultiplier = 1.3f;

        private readonly EntityHandle[] _combatUnits = new EntityHandle[World.MaxEntities];
        private int _unitCount;

        private Position[]? _positions;
        private CombatStats[]? _combatStats;
        private SynergyTags[]? _synergyTags;
        private OwnerTag[]? _ownerTags;

        private bool _isInitialized;

        public void Initialize()
        {
            _positions = World.GetArray<Position>();
            _combatStats = World.GetArray<CombatStats>();
            _synergyTags = World.GetArray<SynergyTags>();
            _ownerTags = World.GetArray<OwnerTag>();
            _isInitialized = true;

            EventManager.Subscribe<EntityDestroyedEvent>(OnEntityDestroyed);
        }

        public void Update(float deltaTime)
        {
            if (!_isInitialized) return;

            // NOT: Bu döngü O(N²) çalışıyor - yani kayıtlı birim
            // sayısı arttıkça (N) maliyet N*N şeklinde büyüyor.
            // Küçük çatışmalarda (birkaç düzine birim) sorun değil,
            // ama yüzlerce birim aynı anda savaşırsa bu, ileride
            // GridManager tabanlı bir "komşu arama" (spatial
            // partitioning) ile optimize edilmesi gereken bir yer
            // olacak - şimdilik notu düşüyoruz, profiling'e kadar
            // erteliyoruz.
            for (int i = 0; i < _unitCount; i++)
            {
                EntityHandle unit = _combatUnits[i];
                if (!World.IsAlive(unit)) continue;

                int unitGridX = _positions![unit.Index].GridX;
                int unitGridY = _positions[unit.Index].GridY;
                int unitTeam = _ownerTags![unit.Index].TeamId;

                int nearbyMelee = 0;
                int nearbyRanged = 0;
                int nearbyElite = 0;

                for (int j = 0; j < _unitCount; j++)
                {
                    if (i == j) continue;

                    EntityHandle other = _combatUnits[j];
                    if (!World.IsAlive(other)) continue;
                    if (_ownerTags[other.Index].TeamId != unitTeam) continue;

                    float dx = unitGridX - _positions[other.Index].GridX;
                    float dy = unitGridY - _positions[other.Index].GridY;
                    float distSq = dx * dx + dy * dy;

                    if (distSq > ScanRadiusSquared) continue;

                    switch (_synergyTags![other.Index].UnitClass)
                    {
                        case UnitClass.Melee:
                            nearbyMelee++;
                            break;
                        case UnitClass.Ranged:
                            nearbyRanged++;
                            break;
                        case UnitClass.Elite:
                            nearbyElite++;
                            break;
                    }
                }

                _synergyTags![unit.Index].NearbyMeleeCount = nearbyMelee;
                _synergyTags[unit.Index].NearbyRangedCount = nearbyRanged;

                ApplySynergyEffects(unit, nearbyMelee, nearbyElite);
            }
        }

        private void ApplySynergyEffects(EntityHandle unit, int nearbyMelee, int nearbyElite)
        {
            UnitClass unitClass = _synergyTags![unit.Index].UnitClass;

            float baseArmor = _combatStats![unit.Index].BaseArmorValue;
            float newArmor = baseArmor;

            // Sadece menzilli birimler bu koruma/açıklık mekaniğinden
            // etkileniyor - yakın dövüşçüler zaten göğüs göğüse
            // dövüştüğü için "korunma" kavramı onlar için anlamsız.
            if (unitClass == UnitClass.Ranged)
            {
                newArmor = nearbyMelee > 0
                    ? baseArmor + MeleeProtectionArmorBonus
                    : baseArmor - ExposedRangedArmorPenalty;

                if (newArmor < 0f) newArmor = 0f;
            }

            _combatStats[unit.Index].ArmorValue = newArmor;

            // Elit birimler kalabalıklaşınca organizasyonu bozuluyor -
            // saldırı hızları yavaşlıyor (cooldown uzuyor).
            float baseCooldown = _combatStats[unit.Index].BaseAttackCooldown;
            float newCooldown = baseCooldown;

            if (unitClass == UnitClass.Elite && nearbyElite >= EliteClusterThreshold)
            {
                newCooldown = baseCooldown * EliteClusterCooldownMultiplier;
            }

            _combatStats[unit.Index].AttackCooldown = newCooldown;
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
                if (_combatUnits[i].Equals(unit)) return;
            }

            if (_unitCount >= _combatUnits.Length) return;

            _combatUnits[_unitCount] = unit;
            _unitCount++;
        }

        public void UnregisterUnit(EntityHandle unit)
        {
            for (int i = 0; i < _unitCount; i++)
            {
                if (_combatUnits[i].Equals(unit))
                {
                    _unitCount--;
                    _combatUnits[i] = _combatUnits[_unitCount];
                    return;
                }
            }
        }
    }
}