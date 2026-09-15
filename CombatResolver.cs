using System;

namespace RTSProje
{
    // ============================================================
    // CombatResolver.cs
    // ------------------------------------------------------------
    // Bir hakem düşün: iki dövüşçü birbirine vurduğunda, hakem
    // araya girip "senin kılıcın onun zırhından ne kadarını
    // deldi" diye karar verir, canı düşürür, gerekirse "yenildin"
    // der. Bu sınıf tam olarak bu hakemi temsil ediyor.
    //
    // Neden bu sistemin Update() metodu boş?
    // Çünkü "kim kime, ne zaman saldırıyor" kararını verecek bir
    // hedefleme/yapay zeka sistemi henüz yok (bu, ileride ayrı bir
    // iş). CombatResolver şimdilik EconomyManager gibi "aksiyon
    // bazlı" çalışıyor - dışarıdan (ileride bir AI ya da oyuncu
    // komutu) ResolveAttack çağrıldığında iş yapıyor.
    // ============================================================
    public class CombatResolver : ISystem
    {
        // Hasar en az bu kadar olur - zırh ne kadar yüksek olursa
        // olsun, "sıfır hasar" gibi anlamsız bir sonuç çıkmasın diye
        // (gerçek hayatta bile bir kılıç darbesi hafif de olsa iz bırakır).
        private const float MinimumDamage = 1f;

        private Health[]? _healths;
        private CombatStats[]? _combatStats;
        private CollisionFlag[]? _collisionFlags;
        private FortifiedDefense[]? _fortifiedDefenses;

        private bool _isInitialized;

        public void Initialize()
        {
            _healths = World.GetArray<Health>();
            _combatStats = World.GetArray<CombatStats>();
            _collisionFlags = World.GetArray<CollisionFlag>();
            _fortifiedDefenses = World.GetArray<FortifiedDefense>();
            _isInitialized = true;
        }

        public void Update(float deltaTime)
        {
            // Kasıtlı olarak boş - bu sistem aksiyon bazlı çalışır,
            // her karede kendiliğinden bir şey yapmaz.
        }

        public void Shutdown()
        {
            // Abone olunmuş bir event yok, temizlenecek bir şey yok.
        }

        // ------------------------------------------------------------
        // SALDIRIYI ÇÖZ (RESOLVE ATTACK)
        // Bir saldırganın bir hedefe vurmasının sonucunu hesaplar ve
        // uygular. Hedefin canı biterse World.DestroyEntity çağrılır.
        //
        // Geriye gerçekte verilen hasarı döner (log/debug amaçlı,
        // ileride test tezgahında işine yarayacak).
        // ------------------------------------------------------------
        public float ResolveAttack(EntityHandle attacker, EntityHandle target)
        {
            if (!_isInitialized) return 0f;
            if (!World.IsAlive(attacker) || !World.IsAlive(target)) return 0f;

            ref CombatStats attackerStats = ref _combatStats![attacker.Index];
            bool targetIsBuilding = _collisionFlags![target.Index].IsStatic;

            // Kural: eğer hedef "sadece kuşatma makinesine açığım"
            // diyorsa (FortifiedDefense.RequiresSiegeWeapon) ve
            // saldırgan kuşatma birimi DEĞİLSE, hasar sıfırdır -
            // kılıç suru gülüp geçer.
            if (_fortifiedDefenses![target.Index].RequiresSiegeWeapon && !attackerStats.IsSiegeUnit)
            {
                return 0f;
            }

            float rawDamage = attackerStats.AttackDamage;

            // Kuşatma birimi bir binaya vuruyorsa, JSON'dan gelen
            // bina bonusunu ekle - koçbaşı/mancınık binaya balyoz gibi çarpar.
            if (attackerStats.IsSiegeUnit && targetIsBuilding)
            {
                rawDamage += attackerStats.SiegeDamageBonusVsBuildings;
            }

            ref CombatStats targetStats = ref _combatStats[target.Index];
            float mitigatedDamage = rawDamage - targetStats.ArmorValue;

            if (mitigatedDamage < MinimumDamage)
            {
                mitigatedDamage = MinimumDamage;
            }

            ref Health targetHealth = ref _healths![target.Index];
            targetHealth.Current -= mitigatedDamage;

            if (targetHealth.Current <= 0f)
            {
                targetHealth.Current = 0f;
                World.DestroyEntity(target);
            }

            return mitigatedDamage;
        }
    }
}