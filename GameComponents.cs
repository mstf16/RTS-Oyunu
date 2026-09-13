using System;
using System.Runtime.InteropServices;

namespace RTSProje
{
    // ============================================================
    // GameComponents.cs
    // ------------------------------------------------------------
    // Burası bizim bakkal defteri. İçinde tek bir satır iş, hesap
    // kitap ya da fonksiyon bulamazsın. Sadece "kimin neyi var"
    // bilgisini tutan saf veri kutularıdır. 
    //
    // Neden class değil de struct?
    // Düşün ki elinde 1000 tane fiş var. Bunları masaya rastgele
    // saçarsan (class mantığı), aradığını bulana kadar canın çıkar,
    // bilgisayar da bellekte oraya buraya koştururken nefesi kesilir.
    // Ama fişleri alt alta tek bir klasöre zımbalarsan (struct mantığı),
    // işlemci göz ucuyla tek seferde hepsini tarar geçer.
    // ============================================================


    // ------------------------------------------------------------
    // POSITION (Konum)
    // Bir birim ya da bina haritanın neresinde duruyor?
    // ------------------------------------------------------------
    [StructLayout(LayoutKind.Sequential)]
    public struct Position
    {
        // Ekrandaki pikseller: Birim kareler arasında takır tukur ışınlanmasın,
        // yağ gibi kaysın diye ara koordinatı burada tutarız.
        public float PixelX;
        public float PixelY;

        // Haritadaki arsa numarası: Satranç tahtası gibi düşün;
        // hangi karede duruyor, hangi kareye gitmek istiyor?
        public int GridX;
        public int GridY;
        public int TargetGridX;
        public int TargetGridY;
    }


    // ------------------------------------------------------------
    // HEALTH (Can / Sağlık)
    // Askerin ya da binanın ayakta kalma gücü.
    // ------------------------------------------------------------
    [StructLayout(LayoutKind.Sequential)]
    public struct Health
    {
        public float Current;   // Kalan canı
        public float Max;       // Ağzına kadar dolu hali
        public float RegenRate; // Kendi kendine yara sarma hızı (çoğunda sıfırdır)
    }


    // ------------------------------------------------------------
    // BUILDING CONFIDENCE (Lojistik ve Yol Güveni)
    // Şehrin göbeğindeki dükkanla dağın başındaki dükkan bir olur mu?
    // Fabrikayı yoldan uzağa dikersen tırlar gecikir, işler yavaşlar.
    // ------------------------------------------------------------
    [StructLayout(LayoutKind.Sequential)]
    public struct BuildingConfidence
    {
        public float DistanceToRoad;       // En yakın asfalta kaç metre uzakta?
        public float ConfidenceValue;      // 0 ile 1 arası moral/güven (1 = tıkırında, 0 = felç)
        public float ProductionMultiplier; // Güven düştükçe askeri/malı kaç kat yavaş üretecek?

        // Bina doğarken (spawn anında) JSON'daki BuildingStatData'dan
        // kopyalanıp buraya damgalanacak iki değer. Her karede JSON'a
        // gidip "bu binanın eşiği kaçtı" diye sormak yavaş olurdu.
        // Doğumda bir kere kopyalarsın, sonra hep buradan okursun.
        public float MaxRoadDistanceThreshold;
        public float ConfidenceDropRatePerTile;
    }


    // ------------------------------------------------------------
    // COMBAT STATS (Dövüş Değerleri)
    // Yumruğu ne kadar sert, zırhı ne kadar kalın?
    // ------------------------------------------------------------
    [StructLayout(LayoutKind.Sequential)]
    public struct CombatStats
    {
        public float AttackDamage;   // Vurunca kaç can koparır?
        public float AttackRange;    // Kılıç mı sallar, uzaktan ok mu atar?
        public float AttackCooldown; // İki tokat arasında kaç saniye soluklanması lazım?
        public float ArmorValue;     // Gelen darbenin ne kadarını savuşturur?
    }


    // ------------------------------------------------------------
    // SYNERGY TAGS (Birlik Dayanışması)
    // Sadece okçu basıp arkana yaslanamazsın. Önlerinde onları
    // koruyacak kalkanlı piyade yoksa okçular çil yavrusu gibi dağılır.
    // ------------------------------------------------------------
    public enum UnitClass : byte
    {
        Melee = 0, // Göğüs göğüse dövüşen piyade
        Ranged = 1, // Uzaktan sallayan okçu
        Elite = 2   // Pahalı süvari ya da şövalye
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SynergyTags
    {
        public int NearbyMeleeCount;  // Yanında kaç tane kalkanlı abi var?
        public int NearbyRangedCount; // Arkasında kaç tane okçu dizilmiş?
        public UnitClass UnitClass;   // Bu askerin cinsi ne?
    }


    // ------------------------------------------------------------
    // VISION RADIUS (Görüş Açısı / Sis)
    // Güvenli yoldan saptıkça askerin gözüne korku düşer, önünü göremez.
    // ------------------------------------------------------------
    [StructLayout(LayoutKind.Sequential)]
    public struct VisionRadius
    {
        public float BaseRadius;    // Gündüz gözüyle normalde ne kadar uzağı görür?
        public float CurrentRadius; // Dağda bayırda kayboldukça daralan gerçek görüşü
    }


    // ------------------------------------------------------------
    // OWNER TAG (Aitlik)
    // Bu asker bizim uşak mı, elin gavuru mu?
    // ------------------------------------------------------------
    [StructLayout(LayoutKind.Sequential)]
    public struct OwnerTag
    {
        public int TeamId; // 0 = Bizim mahalle, 1 = Karşı mahalle
    }


    // ------------------------------------------------------------
    // MOVEMENT SPEED (Koşu Hızı)
    // Asfalt yolda koşmakla bataklıkta debelenmek bir değildir.
    // ------------------------------------------------------------
    [StructLayout(LayoutKind.Sequential)]
    public struct MovementSpeed
    {
        public float BaseSpeed;         // Çırılçıplak düz yoldaki taban hızı
        public float TerrainMultiplier; // Zemin çarpanı (yolda 1.2x fişek gibi, çamurda 0.5x kağnı gibi)
        public float CurrentSpeed;      // O anki gerçek adım atma hızı
    }


    // ------------------------------------------------------------
    // RESOURCE CARRIER (İşçinin Sırtındaki Çuval)
    // Odun kesen amele sırtında kaç kütük taşıyor?
    // ------------------------------------------------------------
    public enum ResourceType : byte
    {
        None = 0,
        Wood = 1,  // Odun
        Stone = 2, // Taş
        Food = 3,  // Ekmek / Aş
        Gold = 4   // Altın
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ResourceCarrier
    {
        public float CarriedAmount;               // Çuvalda şu an ne kadar mal var?
        public float Capacity;                     // Çuval en fazla kaç kilo çeker?
        public ResourceType CarriedResourceType; // Çuvalın içindeki malın cinsi ne?
    }


    // ------------------------------------------------------------
    // TIER INFO (Çağ / Teknoloji Seviyesi)
    // Taş devrinde misin, barut devrinde mi?
    // ------------------------------------------------------------
    [StructLayout(LayoutKind.Sequential)]
    public struct TierInfo
    {
        public int CurrentTier; // Şu an kaçıncı seviyede (1, 2, 3, 4)?
        public int UnlockedAt;  // Bu birim kaçıncı seviyede kilit açtı?
    }


    // ------------------------------------------------------------
    // COLLISION FLAG (Kapı Duvar / Engel)
    // Üzerinden geçip gidilir mi, yoksa kafayı duvara mı çarparsın?
    // ------------------------------------------------------------
    [StructLayout(LayoutKind.Sequential)]
    public struct CollisionFlag
    {
        public bool IsBlocking; // Geçişe kapalı mı (yolu tıkıyor mu)?
        public bool IsStatic;   // Bina gibi çakılı mı duruyor, asker gibi geziyor mu?
    }

// ------------------------------------------------------------
// ROAD TAG
// Bir düşün: mahallede bazı evlerin kapısına "burası PTT'dir"
// diye tabela asıyorsun ki postacı nereye gideceğini bilsin.
// RoadTag da tam olarak bu - içi boş, sadece "bu entity bir
// yoldur" diye üstüne asılmış bir tabela. Hiçbir veri taşımıyor,
// çünkü ihtiyacımız olan tek şey "bu mu yol, değil mi" sorusuna
// hızlı cevap vermek. System_LogisticsConfidence bu tabelayı
// taşıyan tüm entity'leri tarayıp "en yakın yol neresi" diye
// arayacak.
// ------------------------------------------------------------
public struct RoadTag { }


// ------------------------------------------------------------
// SIEGE-ONLY DEFENSE
// Bir kale suru düşün - sıradan bir asker kılıcıyla vursa sur
// gülüp geçer, ama bir mancınık gelirse çöker. Bu struct, tam
// olarak bu "sadece belirli saldırganlara açığım" kuralını taşıyor.
// Sadece yollar değil, ileride surlar/kapılar gibi başka yapılar
// da bunu kullanabilir diye genel bir isim verdik (RoadDefense
// değil, FortifiedDefense).
// ------------------------------------------------------------
public struct FortifiedDefense
{
    public bool RequiresSiegeWeapon;   // true ise sadece kuşatma makineleri hasar verebilir
}


// ------------------------------------------------------------
// SUPPLY NODE
// Bir cep telefonu kulesini düşün - belli bir çap içinde sinyal
// (destek) yayar. Bir tedarik binası da böyle, kendi menzilindeki
// diğer binalara "seni destekliyorum" der. IsNetworkRoot ise
// "ben ana şebeke kaynağıyım, benden başlayan her şey her zaman
// bağlıdır" demek - genelde ana üs (Town Center) bu bayrağı taşır.
// ------------------------------------------------------------
[StructLayout(LayoutKind.Sequential)]
public struct SupplyNode
{
    public float Radius;
    public bool IsNetworkRoot;
}


// ------------------------------------------------------------
// TERRAIN TYPE
// Bir şehir planı düşün: bazı yerler asfalt, bazı yerler çamurlu
// tarla, bazı yerler bataklık, bazı yerler de gölet. Her birinin
// üzerinde yürümek farklı zorlukta. Bu enum, haritadaki her
// hücrenin HANGİ zemin türünde olduğunu tutar - GridManager
// bunu hücre bazında saklayacak, TerrainPhysics de bu bilgiye
// bakıp birimin hızını ona göre ayarlayacak.
// ------------------------------------------------------------
public enum TerrainType : byte
{
    Grass = 0,   // Normal çimen - hız çarpanı 1.0
    Road = 1,    // Yol - hız çarpanı yüksek (örn. 1.3)
    Swamp = 2,   // Bataklık - hız çarpanı düşük (örn. 0.5)
    Water = 3    // Su - neredeyse hiç ilerlenemez (örn. 0.05)
}

// ------------------------------------------------------------
// RESOURCE DEPOSIT (Maden/Kaynak Yatağı)
// Red Alert 2 mantığı: Kaynak azaldıkça verim düşer, zamanla tazelenir.
// ------------------------------------------------------------
[StructLayout(LayoutKind.Sequential)]
public struct ResourceDeposit
{
    public ResourceType Type;              // Kaynak türü (Wood/Stone/Food/Gold)
    public float CurrentAmount;            // Mevcut miktar
    public float MaxAmount;                // Maksimum kapasite
    public float DepletionThreshold;       // Verim düşme eşiği
    public float FullYieldRate;            // Saniyede çıkarılan standart miktar
    public float DepletedYieldMultiplier;  // Eşik altı verim çarpanı
    public float RegenRatePerSecond;       // Saniyelik tazelenme hızı
    public bool RegenPaused;               // Yenilenme durduruldu mu?
}

// ------------------------------------------------------------
// WORKER STATE (İşçinin Görev Durumu)
// İşçinin döngüdeki yerini belirleyen State Machine.
// ------------------------------------------------------------
public enum WorkerStateType : byte
{
    Idle = 0,           // Bekliyor
    MovingToNode = 1,   // Kaynağa gidiyor
    Harvesting = 2,     // Topluyor
    MovingToDropoff = 3,// Depoya dönüyor
    Depositing = 4      // Boşaltıyor
}

[StructLayout(LayoutKind.Sequential)]
public struct WorkerState
{
    public WorkerStateType CurrentState;
    public EntityHandle AssignedNode;      // Atanan kaynak yatağı
    public EntityHandle AssignedDropoff;   // Atanan boşaltma binası
}

// ------------------------------------------------------------
// GARRISON TAG (Kışla/Bina İçi Durumu)
// Bir askerin dışarıda mı yoksa bir binanın koruması altında mı 
// olduğunu belirtir. Binadaki askerler hareket etmez ve 
// dışarıdan gelen saldırılara (binanın canı bitene kadar) kapalıdır.
// ------------------------------------------------------------
[StructLayout(LayoutKind.Sequential)]
public struct GarrisonTag
{
    public bool IsGarrisoned;         // Asker şu an binanın içinde mi?
    public EntityHandle BuildingHandle; // Hangi binanın içinde?
}


}

