using System;

namespace RTSProje
{
    // ============================================================
    // World.cs
    // ------------------------------------------------------------
    // Burası şehrin tapu ve nüfus dairesi.
    // Şehirde aynı anda en fazla 1024 kişi/bina yaşayabilir (MaxEntities).
    //
    // Neden otel odası gibi çalışıyoruz (Nesil / Version Mantığı)?
    // Şöyle düşün: 101 numaralı odada Ahmet Bey kalıyordu. Ahmet Bey
    // otelden ayrıldı (DestroyEntity). Az sonra Mehmet Bey geldi,
    // resepsiyon boşalan 101 numaralı odayı ona verdi.
    // Eğer dışarıdaki kurye "Ahmet Bey'e mektup getirdim, 101'e verin"
    // derse ne olur? Mektup yanlış adama gider!
    // İşte `EntityHandle` dediğimiz şey hem oda numarasını (Index)
    // hem de "bu oda kaçıncı misafirini ağırlıyor" bilgisini (Version)
    // taşır. Mektup eski misafire aitse hemen çöpe atılır, karışıklık çıkmaz.
    // ============================================================
    public static class World
    {
        // Şehirde aynı anda yaşayabilecek maksimum vatandaş sayısı
        public const int MaxEntities = 1024;

        // Kim hayatta, kim göçtü defteri
        private static readonly bool[] _alive = new bool[MaxEntities];

        // Her odanın kaçıncı nesil kiracıyı ağırladığını tutan sayaç
        private static readonly int[] _versions = new int[MaxEntities];

        private static int _activeCount = 0;

        // Boşta duran, hemen verilebilecek anahtarların asılı olduğu tahta
        private static readonly int[] _freeIds = new int[MaxEntities];
        private static int _freeTop;

        static World()
        {
            // Sabah dükkanı açarken tüm anahtarları tahtaya diziyoruz
            for (int i = 0; i < MaxEntities; i++)
            {
                _freeIds[i] = MaxEntities - 1 - i;
                _versions[i] = 0; // Herkes 0. nesilden bismillah der
            }
            _freeTop = MaxEntities;
        }

        public static int EntityCapacity => MaxEntities;
        public static int ActiveCount => _activeCount;

        // ------------------------------------------------------------
        // IS ALIVE (Bu Adam Hâlâ Yaşıyor Mu?)
        // Kapıyı çaldığında içerdeki adam gerçekten aradığın adam mı?
        // ------------------------------------------------------------
        public static bool IsAlive(EntityHandle handle)
        {
            if (!handle.IsValid) return false;
            return _alive[handle.Index] && _versions[handle.Index] == handle.Version;
        }

        // ------------------------------------------------------------
        // CREATE ENTITY (Yeni Vatandaş Kaydı Aç)
        // Boş anahtarlardan birini alıp yeni gelene teslim eder.
        // ------------------------------------------------------------
        public static EntityHandle CreateEntity()
        {
            if (_freeTop <= 0)
            {
                return EntityHandle.Invalid; // Otelde yer kalmadı, kapıdan çevir
            }

            _freeTop--;
            int newIndex = _freeIds[_freeTop];
            _alive[newIndex] = true;
            _activeCount++;

            return new EntityHandle(newIndex, _versions[newIndex]);
        }

        // ------------------------------------------------------------
        // DESTROY ENTITY (Kaydı Sil / Odayı Boşalt)
        // Adam öldüğünde oda boşalır, nesil sayacı +1 artar ki
        // eski mektuplar yeni gelene teslim edilmesin.
        // ------------------------------------------------------------
        public static void DestroyEntity(EntityHandle handle)
        {
            if (!IsAlive(handle)) return;

            _alive[handle.Index] = false;
            _versions[handle.Index]++; // Nesil atlat, eski handle'lar anında geçersiz olsun!
            _freeIds[_freeTop] = handle.Index;
            _freeTop++;
            _activeCount--;
        }

        // ------------------------------------------------------------
        // BİLEŞEN DEPOSU (Component Arrays)
        // Her bileşen türü (Position, Health vs.) için koca bir çekmece.
        // Dictionary araması yok, kutulama yok; çekmeceyi çekip doğrudan
        // sıradaki kayda bakarsın.
        // ------------------------------------------------------------
        private static class ComponentArray<T> where T : struct
        {
            public static readonly T[] Data = new T[MaxEntities];
        }

        public static T[] GetArray<T>() where T : struct
        {
            return ComponentArray<T>.Data;
        }
    }
}