using System;
using System.Collections.Generic;

namespace RTSProje
{
    // ============================================================
    // GarrisonManager.cs
    // ------------------------------------------------------------
    // Burası binaların "güvenlik kapısı". Kimin içeri girebileceğine,
    // içeride kaç kişi olduğuna ve çıkış sırasında nereye 
    // spawn olacaklarına karar verir.
    // ============================================================
    public class GarrisonManager : ISystem
    {
        // Binaların kapasite bilgilerini tutan JSON verileri
        private Dictionary<string, BuildingStatData> _buildingDatabase = null!;
        
        // Hangi binada kimler var? 
        // Key: Bina Handle, Value: İçerideki askerlerin listesi
        private readonly Dictionary<EntityHandle, List<EntityHandle>> _garrisonedUnits = new Dictionary<EntityHandle, List<EntityHandle>>();

        private GarrisonTag[]? _garrisonTags;
        private CollisionFlag[]? _collisionFlags;
        private Position[]? _positions;
        private GridManager _gridManager;

        private bool _isInitialized;

        public GarrisonManager(GridManager gridManager)
        {
            _gridManager = gridManager;
        }

        public void Initialize()
        {
            _buildingDatabase = DataSerializer.LoadBuildingDatabase();
            _garrisonTags = World.GetArray<GarrisonTag>();
            _collisionFlags = World.GetArray<CollisionFlag>();
            _positions = World.GetArray<Position>();
            _isInitialized = true;

            EventManager.Subscribe<EntityDestroyedEvent>(OnEntityDestroyed);
        }

        public void Update(float deltaTime)
        {
            // GarrisonManager pasif bir yönetim sistemidir. 
            // Sadece EnterGarrison veya ExitGarrison çağrıldığında çalışır.
        }

        public void Shutdown()
        {
            EventManager.Unsubscribe<EntityDestroyedEvent>(OnEntityDestroyed);
        }

        // ------------------------------------------------------------
        // GİRİŞ (ENTER GARRISON)
        // ------------------------------------------------------------
        public bool EnterGarrison(EntityHandle unit, EntityHandle building)
        {
            if (!_isInitialized || !World.IsAlive(unit) || !World.IsAlive(building)) return false;

            // 1. Binanın kapasitesini kontrol et
            // (Burada bina ID'sini bulup database'den MaxGarrisonCap'e bakıyoruz)
            // Not: Gerçek projede bina entity'sinde bir 'BuildingId' componenti olmalı.
            // Şimdilik sabit bir kapasite veya basit bir kontrol yapalım.
            int capacity = 5; // Varsayılan kapasite (Siz bunu BuildingStatData'dan çekebilirsiniz)

            if (!_garrisonedUnits.ContainsKey(building))
            {
                _garrisonedUnits[building] = new List<EntityHandle>();
            }

            if (_garrisonedUnits[building].Count >= capacity)
            {
                return false; // Bina dolu!
            }

            // 2. Askeri "dünyadan sil" (Pasif hale getir)
            _garrisonTags![unit.Index].IsGarrisoned = true;
            _garrisonTags[unit.Index].BuildingHandle = building;
            _collisionFlags![unit.Index].IsBlocking = false; // İçerideyken yolu tıkamaz

            // 3. Listeye ekle
            _garrisonedUnits[building].Add(unit);

            return true;
        }

        // ------------------------------------------------------------
        // ÇIKIŞ (EXIT GARRISON)
        // ------------------------------------------------------------
        public bool ExitGarrison(EntityHandle unit)
        {
            if (!_isInitialized || !World.IsAlive(unit)) return false;

            EntityHandle building = _garrisonTags![unit.Index].BuildingHandle;

            if (!World.IsAlive(building) || !_garrisonedUnits.ContainsKey(building))
            {
                return false;
            }

            // 1. Listeden çıkar
            _garrisonedUnits[building].Remove(unit);

            // 2. Askeri tekrar "aktif" hale getir
            _garrisonTags[unit.Index].IsGarrisoned = false;
            _collisionFlags![unit.Index].IsBlocking = true;

            // 3. Binanın yanına spawn et (GridManager kullanarak)
            int bX = _positions![building.Index].GridX;
            int bY = _positions![building.Index].GridY;
            
            // Binanın hemen yanındaki boş bir kareyi bul ve oraya koy
            // (Basitlik adına şu an binanın konumuna koyuyoruz, 
            // ileride 'en yakın boş kare' mantığı eklenecek)
            _positions[unit.Index].GridX = bX;
            _positions[unit.Index].GridY = bY;
            _gridManager.GridToPixel(bX, bY, out float pX, out float pY);
            _positions[unit.Index].PixelX = pX;
            _positions[unit.Index].PixelY = pY;

            return true;
        }

        private void OnEntityDestroyed(EntityDestroyedEvent e)
        {
            // Eğer yok edilen şey bir askerse ve binadaysa, binanın listesinden sil
            if (_garrisonTags![e.Handle.Index].IsGarrisoned)
            {
                EntityHandle building = _garrisonTags[e.Handle.Index].BuildingHandle;
                if (_garrisonedUnits.ContainsKey(building))
                {
                    _garrisonedUnits[building].Remove(e.Handle);
                }
            }

            // Eğer yok edilen şey bir BİNAYSA, içindeki herkesi dışarı çıkar/öldür.
            //
            // ÖNEMLİ: Önce binayı DEFTERDEN SİLİYORUZ, sonra askerleri yok
            // ediyoruz. Neden sıralama böyle? Çünkü her asker yok edilirken
            // KENDİ EntityDestroyedEvent'ini fırlatıyor, bu da bu metodu
            // TEKRAR (iç içe/recursive) çağırıyor. Eğer bina hâlâ deftere
            // kayıtlıysa, o iç çağrı yukarıdaki "listeden sil" satırına
            // girip AYNI ANDA gezdiğimiz listeyi değiştirmeye çalışır -
            // bu da "Collection was modified" çökmesine sebep olur.
            // Binayı ÖNCE silersek, iç çağrılar "bu bina zaten kayıtlı
            // değil" deyip hiçbir şey yapmadan çıkar, çakışma olmaz.
            if (_garrisonedUnits.TryGetValue(e.Handle, out List<EntityHandle>? occupants))
            {
                // Ayrıca listenin bir KOPYASINI (ToArray) alıyoruz - orijinal
                // listeye artık kimse dokunmuyor olsa bile, bu ekstra bir
                // güvenlik katmanı, "asla aynı listeyi hem okuyup hem
                // değiştirme" prensibini net tutuyor.
                EntityHandle[] occupantsCopy = occupants.ToArray();
                _garrisonedUnits.Remove(e.Handle);

                for (int i = 0; i < occupantsCopy.Length; i++)
                {
                    if (World.IsAlive(occupantsCopy[i]))
                    {
                        World.DestroyEntity(occupantsCopy[i]);
                    }
                }
            }
        }
    }
}
