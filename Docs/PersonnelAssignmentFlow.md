# Personel Görev Atama Süreci

Bu doküman, Personel Görev Atama animasyonu ve API bütünleşik sürecini anlatır.

## Amaç
- Personellerin son lokasyonlarını sistemden otomatik çekmek
- İş listesini personellere adil ve verimli şekilde dağıtmak
- Toplam tamamlanma süresini tahmin etmek ve görselleştirmek
- Son 1 haftalık performans, tecrübe, hız katsayısı gibi kriterleri dikkate almak

## Veri Kaynakları
- `IPersonnelRepository` JSON tabanlı veri: `personnel.json`
  - `personnel`: id, ad, `lastLocationCode`, `pickerExperience`, `speedFactor`, `zone`
  - `locations`: personel-id → `(x,y)` gerçek zamanlı lokasyon haritası
  - `weeklyPerformance`: personel-id → performans katsayısı
- `JobsRepository`: iş tanımları ve geçmişi (tamamlanma vs.)

## Ana Servisler
- `MLPickingService`: tahmin motoru (süre tahmini)
- `PersonnelDispatchService`: iş-parça → personel atama ve süre hesaplama

## API Uçları
- `GET /personnel/locations`: son `(x,y)` lokasyonları
- `GET /personnel/performance`: haftalık performans katsayıları
- `POST /assign-picking`:
  - Body: `multipart/form-data` (Excel) veya `application/json` (PickingTask[])
  - Çıktı: `assignments[]`, `estimatedCompletionSeconds`, `personnelCount`, `taskCount`

## Atama Algoritması (Özet)
1. Personel listesi ve son konumları alınır.
2. Her görev için başlangıç lokasyonu çözümlenir (`FirstLocation`).
3. Personel bazında `Mesafe`, `Tecrübe`, `Yapılacak Miktar` özellikleriyle ML tahmini yapılır.
4. En düşük tahmini süreye sahip personele görev atanır, personelin son konumu güncellenir.
5. Kişi bazlı toplam süre ve mesafe toplanır; genel `estimatedCompletionSeconds = max(personel toplam süre)` hesaplanır.

## Animasyon (Frontend Taslak)
- Zaman çizelgesi (timeline): kişi bazlı bloklar, görevin tahmini süresi boyunca ilerleyen barlar.
- Harita/plan: raf koridorları ve kişi konumu, iş başlangıcına gitme mesafesi ile vurgulama.
- Filtreler: `zone`, öncelik, iş emri, marka.
- Yenileme: `/personnel/locations` 5 saniye önbellekleme ile auto-refresh.

## Kriterler ve Ağırlıklandırma
- `Mesafe`: başlangıç konumuna uzaklık
- `Tecrübe`: `PickerExperience` (1–5)
- `Hız`: `SpeedFactor` (varsa)
- `Haftalık Performans`: `/personnel/performance` ile ek katsayı (UI’de ağırlık slider)
- `Miktar`: `TodoQuantity` veya `Quantity`

## Test Önerileri
- Excel örneği ile `/assign-picking` çağrısı: beklenen kişi sayısı ve toplam süre.
- JSON ile çağrı: görev sayısı eşleşmeli, `estimatedCompletionSeconds` > 0 olmalı.

## Gelecek Geliştirmeler
- `AssignTasks` içinde konum override desteği (repo’daki `(x,y)` haritası).
- Zaman pencereleri ve öncelik tabanlı atama.
- UI canlı animasyon ve WebSocket güncellemeleri (SignalR’sız sade SSE opsiyonu).