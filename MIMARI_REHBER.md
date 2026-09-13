# RTS Projesi — Ana Mimari ve Sistem Rehberi (Bakkal Usulü Kılavuz)

Bu dosya, projedeki **istisnasız tüm dosyaların** ne işe yaradığını, günlük hayattaki karşılığını, hangi sorunu çözdüğünü ve neden bu şekilde kodlandığını anlatan ana kılavuzdur. 
İleride kodları kopyala-yapıştır ile sıfırlasan veya başka bir yapay zekaya baştan yazdırsan bile buradaki açıklamalar **çelik kasada güvendedir**.

---

## 1. Çekirdek & Veri Dosyaları (Faz 1)

### 📄 `GameComponents.cs` (Bakkalın Veresiye Defteri)
* **Günlük Hayat Karşılığı:** Masadaki boş başvuru formları ya da bakkalın veresiye defteri. İçinde tek bir işlem, karar, fonksiyon ya da hesap-kitap yoktur. Sadece "kimin neyi var" bilgisini tutar.
* **Neden Class Değil de Struct?** Masaya 1000 tane fişi dağınık saçarsan (`class`), aradığını bulana kadar canın çıkar; işlemci de bellekte oraya buraya koştururken nefesi kesilir. Ama fişleri alt alta tek bir klasöre zımbalarsan (`struct`), işlemci göz ucuyla tek hamlede hepsini sırayla okur geçer (CPU Cache-Friendly).
* **İçindeki Kutular:**
  * `Position`: Askerin ekrandaki pikseli ve haritadaki arsa/kare numarası.
  * `Health`: Kalan canı, tavan canı ve yara sarma hızı.
  * `BuildingConfidence`: Asfalta/yola uzaklığına göre binanın moral ve verim katsayısı.
  * `SupplyNode`: Baz istasyonu gibi sinyal yayan lojistik merkezinin etki yarıçapı (`Radius`) ve doğrudan hatta bağlı olup olmadığı (`IsRootHub`).
  * `CombatStats`: Yumruğunun sertliği, menzili ve iki tokat arasındaki dinlenme süresi.
  * `SynergyTags`: Askerin cinsi (piyade/okçu/elit) ve çevresinde onu kollayan kalkanlı sayısı.
  * `VisionRadius`: Güvenli yoldan saptıkça askerin gözüne korku düşmesiyle daralan görüş mesafesi.
  * `MovementSpeed`: Asfaltta fişek gibi, çamurda kağnı gibi yürümesini sağlayan zemin çarpanı.
  * `ResourceCarrier`: Amele askerin sırtındaki çuvalda kaç kilo odun/taş/demir/barut olduğu.
  * `TierInfo`: Çağ seviyesi (taş devrinde mi, demir çağında mı).
  * `CollisionFlag`: Kapı-duvar mı (yolu tıkıyor mu), bina gibi çakılı mı duruyor?

---

### 📄 `EntityHandle.cs` (Otel Anahtarlığı ve Kimlik Kartı)
* **Günlük Hayat Karşılığı:** Üzerinde oda numarası ve "kaçıncı misafir" olduğu yazan otel oda anahtarlığı.
* **Neden Sadece `int id` Değil?** Düşün ki oyunda 5 numaralı ID'ye sahip bir asker öldü. Az sonra yeni bir asker doğdu ve boşalan 5 numaralı ID ona verildi. Eğer eski bir okçu mermisi havada uçarken "5 numaralı hedefe vuracağım" diyorsa, yeni doğan masum askeri vurur!
* **Çözüm:** `EntityHandle` içinde hem `Index` (oda numarası) hem de `Version` (nesil/misafir numarası) tutulur. Eski hedefin nesli ile yeni hedefin nesli tutmazsa mermi havada boşa düşer.

---

### 📄 `World.cs` (Nüfus ve Tapu Dairesi)
* **Günlük Hayat Karşılığı:** Şehrin tapu dairesi ve 1024 odalı dev bir otelin resepsiyonu.
* **Ne Yapar?** 
  * Şehirde aynı anda en fazla 1024 kişi/bina yaşayabilir (`MaxEntities = 1024`).
  * Biri doğduğunda resepsiyondan boş bir oda anahtarı verir (`CreateEntity`).
  * Biri öldüğünde odasını boşaltır ve nesil sayacını +1 artırır (`DestroyEntity`).
* **Sıfır Çöp (Zero-GC) Prensibi:** 1024 kişilik kontenjan baştan açılır. Oyun çalışırken yeni dizi (`new`) tahsis edilmez, çöp toplayıcı (GC) asla devreye girip oyunu tekletmez.
* **`ComponentArray<T>` Sırrı:** Her veri türü için (`Position`, `Health` vb.) koca bir çekmece tutar. Arama yapmadan, tip dönüştürmeden (cast olmadan) doğrudan çekmeceye elini atar ve askerin verisini alırsın.

---

### 📄 `EventManager.cs` (Şantiye Megafonu / Telsiz Santrali)
* **Günlük Hayat Karşılığı:** Şantiyedeki telsiz sistemi. Biri kaza geçirdiğinde gidip şantiye şefinin yakasına yapışmaz. Megafondan "3. blokta kaza var!" diye anons geçer (`Publish`). İlk yardımla kim ilgileniyorsa kulak kabartıp koşar (`Subscribe`).
* **Neden Bu Şekilde Yazıldı?** Savaş mantığı ile ekran çizim motorunun birbirini tanımasına gerek yoktur (Gevşek Bağlılık / Loose Coupling). Çizim motoru sadece telsizi dinler.
* **C#'ın Multicast Delege Gücü:** Normalde her anonsta liste karıştırmak bellekte çöp üretir. Burada doğrudan C#'ın telsiz kablosu (`Delegate.Combine` / `Delegate.Remove`) çekildi. Anons sıfır çöp (Zero-GC) ile ışık hızında dinleyenlere ulaşır.

---

### 📄 `CoreEngine.cs` (Fabrika Şalteri ve Duvar Saati)
* **Günlük Hayat Karşılığı:** Fabrikanın ana elektrik şalteri ve duvardaki hassas kronometre.
* **Neden İhtiyaç Var?** Kimi oyuncunun bilgisayarı saniyede 60 kare çizer, zengin adamınki 240 kare çizer. Eğer oyunu zamana bağlamazsan, güçlü bilgisayarı olan adamın askeri 4 kat hızlı koşar! 
  `CoreEngine`, iki göz kırpması arasında geçen saliseyi (`DeltaTime`) hesaplar; herkese "bu salisede ne kadar hareket ettiysen o kadar ilerle" der.
* **Akıllı Fren (Hibrit FPS Sabitleme):** Windows'un varsayılan uyku mekanizması 15 milisaniye şaşar ve ekranda titreme (jitter) yapar. Bizim motor, hedefe 2 milisaniyeden fazla varsa işlemciyi dinlendirir (`Thread.Sleep(1)`), son milisaniyede ise tetikte bekler (`Thread.Yield`). Oyun taş gibi 60 FPS'e kilitlenir.

---

### 📄 `DataSerializer.cs` (Yemek Tarifi ve Reçete Defteri)
* **Günlük Hayat Karşılığı:** Askerin canını, kışlanın maliyetini kodun içine gömersen, her denge ayarında projeyi baştan derlemek zorunda kalırsın. Bunun yerine verileri harici JSON dosyalarından (`unit_stats.json`, `config_base.json`) okuyoruz.
* **Çökme Koruması (Crash Resilience):** Birisi yanlışlıkla JSON dosyasını silerse ya da içine yanlış bir virgül atıp bozarsa oyun patlayıp masaüstüne atmaz! Kendi cebinden fabrika ayarlarını çıkarır, oyunu tıkır tıkır başlatır ve bozulan dosyayı da arkada onarır.

---

## 2. Izgara Harita, Yol Ağları ve Güven Sistemi (Faz 2)

### 📄 `GridManager.cs` (Kadastro ve Arsa Parselasyonu)
* **Günlük Hayat Karşılığı:** Şehrin arsa parselasyon haritası. Dünyayı satranç tahtası gibi kare arsalara böler.
* **Neden Tek Boyutlu Dizi?** İki boyutlu diziler (`[x,y]`) bellekte dağınık durur. Tek boyutlu dizi tarladaki karıklar gibidir; 1. sıra biter, ucuna hemen 2. sıra eklenir. `y * Genişlik + x` formülüyle aradığın arsayı işlemci gözü kapalı bulur.
* **Çok Kareli İnşaat (`PlaceBuildingFootprint`):** Kışla gibi 2x2 veya 3x3 binalar birden fazla arsayı kaplar. Arsalardan biri bile doluysa tapu verilmez, inşaat engellenir.
* **Dinamik Ekran:** Oyuncu pencereyi köşesinden tutup büyüttüğünde veya tam ekran yaptığında, arsaların ekrandaki piksel boyutu anında yeniden biçilir; harita ekrana tam oturur.

---

### 📄 `System_LogisticsConfidence.cs` (WiFi / Baz İstasyonu Şebekesi)
* **Günlük Hayat Karşılığı:** Cep telefonu şebekesi veya mahalledeki WiFi sinyali.
* **Nasıl Çalışır?** 
  * Şehir Merkezi (`Town Center`) ana vericidir (Root Hub), sinyali her zaman yüzde yüzdür.
  * Şehir merkezine yakın kurulan depolar ve karakollar bu sinyali yakalayıp menzili ileriye taşır (BFS dalga yayılımı).
  * Bir bina (kışla, maden ocağı vb.) bu şebekenin menzili içindeyse tam verimle (Güven = 1.0) çalışır.
  * Şebekeden kopuksa veya çok uzaktaysa mesafeye göre morali/güveni kademe kademe erir.
* **Karekök Optimizasyonu ($dx^2 + dy^2 \le R^2$):** Döngü içinde saniyede on binlerce kez pahalı `MathF.Sqrt` çağırmak yerine mesafenin karesi üzerinden kıyaslama yapılır. Karekök yalnızca ceza çarpanı hesabı için en sonda 1 kez alınır.
* **Şifreli Ağ (Takım Kontrolü):** Komşunun WiFi'ına kaçak bağlanamazsın! Düşmanın kurduğu şebekeden bizim binalar sinyal çalamaz; sadece kendi rengimizdeki üslerden sinyal yayılır.

---

### 📄 `BuildingStateMonitor.cs` (Şalter / Debuff Vardiya Amiri)
* **Günlük Hayat Karşılığı:** Jeneratörün ibresine bakıp fabrikadaki makinelerin hız ayarını çeken vardiya amiri.
* **Neden Ayrı Bir Sistem?** Lojistik sistemi sadece "sinyal kaç metre çekiyor" hesabını yapar (matematik). Ama binaların üretimini kısmak, işçileri yavaşlatmak ayrı bir iştir (Tek Sorumluluk Prensibi - SRP).
* **Kepenk Kapatma Kuralı (`ShutdownThreshold = 0.05f`):** Eğer bir binanın şebekeye güveni %5'in altına düşerse, bina verimsiz çalışmayı da bırakıp komple şalter indirir (`ProductionMultiplier = 0`). Böylece düşmanın burnunun dibine yolu ve lojistiği olmadan kışla dikip asker basamazsın!

---

## 3. Ekran, Çizim ve Olay Sistemi

### 📄 `RenderFrameStartSystem.cs` (Perdeyi Kapatan Sahne Görevlisi)
* **Günlük Hayat Karşılığı:** Tiyatroda yeni sahneye geçmeden önce perdeyi kapatıp sahneyi süpüren görevli.
* **Ne Yapar?** Sistemler sırasının en başında çalışır. Ekranı temizler (`SDL_RenderClear`) ve arka plan rengini boyar. Böylece diğer ustalar tertemiz bir tuvale çizim yapar.

---

### 📄 `RenderFrameEndSystem.cs` (Perdeyi Açan Sahne Görevlisi)
* **Günlük Hayat Karşılığı:** Tiyatroda sahne hazır olduğunda seyirciye perdeyi açan görevli.
* **Ne Yapar?** Sistemler sırasının en sonunda çalışır. Bütün çizimler bittikten sonra tek hamlede sahneyi ekrana basar (`SDL_RenderPresent`). Karede yırtılma ve titreme olmasını engeller.

---

### 📄 `TempGridLineRenderSystem.cs` (Geçici Tebeşirli Çizgi Ustası)
* **Günlük Hayat Karşılığı:** Temel atılmadan önce arsanın sınırlarını tebeşirle yere çizen çırak.
* **Görevi:** Faz 6'daki ana görsel motor (`SpriteRenderer`) gelene kadar, gözümüzle harita karelerinin ekrana doğru oturup oturmadığını görebilmemiz için ekrana gri ızgara çizgileri çeker.

---

### 📄 `SdlEventPumpSystem.cs` (Kapıdaki Nöbetçi Bekçi)
* **Günlük Hayat Karşılığı:** Fabrika kapısında bekleyen nöbetçi. Dışarıdan gelen misafirleri (klavye tuşları, fare tıklamaları, pencere kapatma ve boyutlandırma) tek tek karşılar (`SDL_PollEvent`) ve içeriye telsizle anons geçer.

---

### 📄 `QuitRequestedEvent.cs` & `WindowResizedEvent.cs` (Telsiz Şablonları)
* `QuitRequestedEvent`: "Oyuncu çarpıya bastı, dükkanı kapatın" diyen kırmızı alarm anonsu.
* `WindowResizedEvent`: "Pencerenin yeni ölçüsü budur, haritayı buna uydurun" diyen boyut bildirimi.

---

### 📄 `Program.cs` (Fabrikanın Ana Giriş Kapısı)
* **Günlük Hayat Karşılığı:** Sabah fabrikayı açan baş usta. Konsoldan harita ölçüsünü alır, pencereyi kurar, ustaları sıraya dizer ve ana şalteri kaldırıp oyunu başlatır.

---

## 4. 🚀 Kutsal Seviye (God Tier) Optimizasyon Yol Haritası
*(Bu bölüm Faz 5 tamamlandıktan sonra, Faz 6 grafik katmanına geçilmeden önce motora uygulanacaktır!)*

1. **SIMD (Single Instruction, Multiple Data - Vektörleştirme):**
   * *Bakkal Mantığı:* Yumurtaları tek tek teraziye koymak yerine 8 gözlü teraziyle 8 yumurtayı tek seferde tartmak.
   * *Uygulama:* `System.Runtime.Intrinsics` ile 8 askerin hareket/fizik hesabı tek bir CPU saat döngüsünde yapılacak.
2. **Spatial Partitioning (Uzamsal / Mahalle Bölümlemesi):**
   * *Bakkal Mantığı:* Kadıköy'deki arıza için Beylikdüzü'ndeki vanayı boşuna kontrol etmemek.
   * *Uygulama:* $O(B \times S)$ karmaşıklığını bitirip, binaları sadece kendi mahallelerindeki tedarik noktalarıyla eşleştirmek ($O(1)$ yakınsama).
3. **Multithreading (Çoklu İş Parçacığı / Çift Vardiya):**
   * *Bakkal Mantığı:* Sıvacı bir duvarda çalışırken boyacının öbür odada çalışması.
   * *Uygulama:* Birbirine bağımlı olmayan sistemleri (`TerrainPhysics` ve `BuildingStateMonitor` gibi) ayrı çekirdeklere dağıtmak.
