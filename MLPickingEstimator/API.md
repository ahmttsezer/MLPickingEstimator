# API Dokümantasyonu

## Tahmin
- `POST /predict` — ML.NET modeli ile tahmin (rate limit: `predict` 30/dk, cache: yok)
- `POST /predict-with-locations` — Lokasyon kodu/koordinat ile mesafeyi hesaplayıp tahmin yapar
  - Gövde: `PredictionWithLocationsRequest`
    - Zorunlu: `itemCount`, `weight`, `volume`, `pickerExperience`, `stockDensity`
    - Lokasyon: `lastLocationCode` + `taskLocationCode` veya `lastLocation` + `taskLocation`
    - Opsiyonel: `distanceOverride` (verilirse lokasyondan hesaplanmaz)
    - Diğer opsiyoneller: `fragileRatio`, `aisleCrowding`, `cartLoadKg`, `pickerFatigue`, `stopCount`, `urgencyLevel`, `zoneComplexity`, `temperatureHandling`
  - Çalışma: Lokasyon kodları `LocationResolver` ile koordinata çözülür; eğer `distanceOverride` sağlanmazsa iki nokta arası öklidyen mesafe `distance` olarak kullanılır ve standart `PredictionRequest` doğrulamasından geçirilir.
  - Rate limit: `predict` 30/dk, cache: yok
- `POST /ensemble-predict` — Çoklu modelden ortalama tahmin (rate limit: `predict` 30/dk, cache: yok)
- `POST /predict-onnx` — ONNX modeli ile tahmin (opsiyonel normalizasyon) (rate limit: `predict` 30/dk, cache: yok)
  - Query yok; gövde: `PredictionRequest`
  - Normalizasyon: `appsettings.json` `Onnx.Normalize` true ise baseline ortalamaları ile mean-centering uygulanır
- `POST /predict-batch` — Toplu tahmin (rate limit: `batch` 10/dk, cache: yok)

## Eğitim
- `POST /train` — Modeli eğitir, kaydeder ve versiyonlar (rate limit: `train` 3/5dk, cache: yok)
- `POST /train-multiple` — Birden fazla algoritmayı dener ve metrikleri döner (rate limit: `train` 3/5dk, cache: yok)
- `POST /retrain-if-drift` — Drift tespit edilirse yeniden eğitim (opsiyonel `thresholdPercent`) (rate limit: `train` 3/5dk, cache: yok)

## ONNX
- `GET /onnx-info` — ONNX giriş/çıkış bilgilerini konsola yazar (rate limit: `general` 60/dk, cache: 30sn)

## İzleme ve Metrikler
- `GET /monitor` — Performans analizi ve tahmin dağılımı (rate limit: `general` 60/dk, cache: 30sn)
- `GET /metrics` — Basit durum bilgisi (rate limit: `general` 60/dk, cache: 15sn)
- `GET /metrics-full` — Versiyonlar, izleme özeti ve drift durumu (opsiyonel `thresholdPercent`) (rate limit: `general` 60/dk, cache: 30sn)
- `GET /performance-report` — Konsola rapor yazdırır (rate limit: `general` 60/dk, cache: 30sn)
- `GET /export-logs` — `prediction_logs.csv` oluşturur (rate limit: `general` 60/dk, cache: yok)

## Rota ve Dispatch
- `POST /simulate-route` — Rota simülasyonu (JSON) (rate limit: `simulate` 20/dk, cache: yok)
  - Gövde (iki kullanım desteği):
    - Koordinat tabanlı: `{ lastLocation:{x,y}, taskLocations:[{x,y}] }`
    - Kod tabanlı: `{ lastLocationCode:"PX-MZ-D01-100A", taskLocationCodes:["PX-MZ-D08-171F", ...] }`
  - Yanıt: `{ orderedLocations:[{x,y}], totalDistance }`
  
Not: Eski statik SVG uçları kaldırıldı.
- `POST /optimize-dispatch` — Araçlara görev atama ve rota optimizasyonu (rate limit: `simulate` 20/dk, cache: yok)
  - Gövde: 
    - `vehicles`: `[{ id, currentLocation:{x,y}, currentLocationCode?, capacityKg?, capacityCases?, zone? }]`
    - `jobs`: `[{ id, rackId?, location:{x,y}?, quantity?, weightKg?, priority?, earliestTime?, latestTime?, zone?, orderId?, corridor? }]`
      - Not: `rackId` (örn. `PX-MZ-D08-171F`) verildiğinde sunucu koordinata ve koridora çözümler; `corridor` boşsa raf kodundan türetilir.
    - `averageSpeedMps?` (m/s, varsayılan 1.2), `handlingSecondsPerTask?` (saniye, varsayılan 15)
  - Kurallar:
    - Zone bazlı dağıtım: robot sadece kendi `zone` işlerindeki adaydır.
    - Kapasite: `capacityCases` ve `capacityKg` aşılmayacak şekilde atama yapılır.
    - Koridor gruplama: aynı `corridor` (örn. `D08`) işleri mümkünse aynı robota gruplanır.
    - Sipariş birleştirme: aynı `orderId` + aynı `zone` işleri mümkünse aynı robota verilir.
    - Zaman penceresi: `earliestTime`/`latestTime` uygunsa öncelikli değerlendirilir.
  - Yanıt: `{ results: [{ vehicleId, orderedLocations:[{x,y}], totalDistance, estimatedTimeSeconds, assignedJobCount }] }`
  - İstemci tarafı, raf kimliklerini doğrudan `rackId` olarak gönderebilir; koordinata ve koridora çözümleme sunucu içinde yapılır (`LocationResolver`).
- `GET /warehouse` — Görev atama arayüzü (redirect: `/personnel-assign.html`)

## Jobs Ingestion & Export (Yeni)

- `POST /jobs/ingest` — İş listesi kalıcı depoya kaydetme
  - Gövde: `DispatchRequest` içinde `jobs: JobTask[]`
  - Yanıt: `{ inserted: N }`

- `GET /jobs/export?format=csv|xlsx` — İş listesini CSV veya Excel (XLSX) olarak indir
  - Yanıt: Dosya indirimi (`jobs.csv` veya `jobs.xlsx`)
  - Not: CSV `text/csv`, XLSX `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` olarak servis edilir.

## Sağlık
- `GET /health` — Servis durumu (rate limit: `general` 60/dk, cache: 5sn)
- `GET /versions` — Sürüm listesi

## Yapılandırma
- `appsettings.json`
  - `Paths` — `ModelsDir`, `ZipModel`, `OnnxModel`, `VersionsFile`, `ArchiveDir`, `DataDir`, `TrainingData`
  - `Versioning.RetentionCount`
  - `Drift.ThresholdPercent`
  - `Onnx.Normalize`, `Onnx.BaselineDataPath`

## Hız Limitleri ve Cache Politikaları

- Hız limitleri, kötüye kullanımı önlemek ve sunucu istikrarını korumak için uygulanır.
  - `predict`: 30/dk
  - `batch`: 10/dk
  - `train`: 3/5dk
  - `simulate`: 20/dk
  - `general` (GET): 60/dk
- GET istekleri için `OutputCache` varsayılan TTL: 30sn; endpoint’e özel TTL’ler tanımlanmıştır.
- POST istekleri cache’lenmez.

## JSON Formatlama

- Varsayılan yanıtlar `camelCase` olarak döner.
- `null` alanlar yazılmaz.

---

## Telemetry API (Yeni)

### POST `/telemetry`
- Canlı personel performans telemetrisi için kullanılır
- Gövde: Telemetri verileri (JSON)
- 200: Başarılı yanıt

Not: Bu endpoint personel performans izleme için kullanılır ve bellek içi depolama yapar.

---

## Opsiyonel Toplama Kriterleri (Yeni)

- `fragileRatio` — Kırılgan ürün oranı (0-1)
- `aisleCrowding` — Koridor yoğunluk indeksi (0-1)
- `cartLoadKg` — Sepet/araç yükü (kg)
- `pickerFatigue` — Toplayıcı yorgunluk seviyesi (0-1)
- `stopCount` — Ziyaret edilecek durak sayısı
- `urgencyLevel` — İş aciliyet seviyesi (0-1)
- `zoneComplexity` — Zone karmaşıklığı (0-1)
- `temperatureHandling` — Sıcaklık kontrollü ürün zorluğu (0-1)

Notlar:
- Bu alanlar opsiyoneldir; gönderilmezse 0 kabul edilir.
- Mevcut eğitim pipeline’ı değişmeden çalışır; veri genişledikçe model pipeline’ına eklenecektir.
