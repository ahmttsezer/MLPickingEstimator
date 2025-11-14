# MLPickingEstimator (Web API) — Proje Dokümantasyonu

Bu klasör, web API projesi için dosyaları içerir. Geniş kapsamlı kurulum, yayın ve doğrulama adımları için kök dizindeki `README.md` dosyasını izleyin.

Hızlı başlatma:
- `dotnet restore ../MLPickingEstimator.sln`
- `dotnet run --project . --urls http://localhost:5100`
- UI: `http://localhost:5100/personnel-assign.html`

Detaylar ve API referansı: kök `README.md` ve `API.md`.

## ğŸš€ HÄ±zlÄ± BaÅŸlangÄ±Ã§

```bash
# Projeyi klonlayÄ±n
git clone <repository-url>
# Paketleri yÃ¼kleyin (solution kÃ¶kÃ¼nde)
dotnet restore ../MLPickingEstimator.sln

# Web API'yi baÅŸlatÄ±n (bu klasÃ¶rde)
dotnet run --project .

# Konsol uygulamasÄ±nÄ± Ã§alÄ±ÅŸtÄ±rÄ±n (Ã¼st klasÃ¶rden)
dotnet run --project ../MLConsoleApp
```

## ğŸ“Š Ã–zellikler

- âœ… ML.NET Pipeline ile model eÄŸitimi
- âœ… AutoML ile otomatik algoritma seÃ§imi
- âœ… REST API ile web servisi
- âœ… ONNX entegrasyonu
- âœ… DetaylÄ± performans metrikleri
- âœ… KapsamlÄ± dokÃ¼mantasyon
- âœ… HTTP Logging ve JSON camelCase
- âœ… Rate Limiting (yÃ¼ksek trafikte koruma)
- âœ… Output Caching (GET endpointâ€™lerinde TTL)

## ğŸ”§ API KullanÄ±mÄ±

### Tahmin Yapma

```bash
curl -X POST "https://localhost:5001/predict" \
  -H "Content-Type: application/json" \
  -d '{
    "itemCount": 12,
    "weight": 6.0,
    "volume": 1.8,
    "distance": 90,
    "pickerExperience": 3,
    "stockDensity": 0.85,
    "fragileRatio": 0.2,
    "aisleCrowding": 0.4,
    "cartLoadKg": 8.5,
    "pickerFatigue": 0.3,
    "stopCount": 5,
    "urgencyLevel": 0.6,
    "zoneComplexity": 0.3,
    "temperatureHandling": 0.1
  }'
```

### Model EÄŸitimi

```bash
curl -X POST "https://localhost:5001/train"
```

### Toplu Tahmin

```bash
curl -X POST "https://localhost:5001/predict-batch" \
  -H "Content-Type: application/json" \
  -d '[
    {"itemCount":12,"weight":6.0,"volume":1.8,"distance":90,"pickerExperience":3,"stockDensity":0.85,"fragileRatio":0.2,"aisleCrowding":0.4},
    {"itemCount":25,"weight":10.0,"volume":3.0,"distance":120,"pickerExperience":4,"stockDensity":0.92,"cartLoadKg":12.0,"stopCount":8}
  ]'
```

### SaÄŸlÄ±k KontrolÃ¼

```bash
curl "https://localhost:5001/health"
```

### Rate Limiting ve Cache NotlarÄ±

- `429 Too Many Requests` alÄ±rsanÄ±z hÄ±z limitini aÅŸtÄ±nÄ±z:
  - `predict` grubu: 30/dk
  - `batch` grubu: 10/dk
  - `train` grubu: 3/5dk
  - `simulate` grubu: 20/dk
  - `general` grubu (GET): 60/dk
- GET endpointâ€™lerinde varsayÄ±lan `OutputCache` sÃ¼resi 30 snâ€™dir; bazÄ± endpointâ€™ler Ã¶zel TTL kullanÄ±r (Ã¶rn. `metrics` 15 sn, `health` 5 sn, Ã¶rnek SVGâ€™ler 60 sn).
- POST endpointâ€™lerde cache yoktur (`NoCache`).

## ğŸ§ª Postman KullanÄ±mÄ± (HazÄ±r Koleksiyon)

- Koleksiyon: `Docs/MLPickingEstimator.postman_collection.json`
- Ortam: `Docs/MLPickingEstimator.postman_environment.json`

AdÄ±mlar:
- Postmanâ€™Ä± aÃ§Ä±n ve koleksiyonu iÃ§e aktarÄ±n.
- Ortam dosyasÄ±nÄ± iÃ§e aktarÄ±n ve aktif edin.
- Ortam deÄŸiÅŸkenleri:
  - `baseUrl`: varsayÄ±lan `http://localhost:5000`
  - `apiKey`: `appsettings.json > Security:ApiKey:Value` ile eÅŸleÅŸmeli (gÃ¼venlik aktifse)
- GÃ¼venlik aktif deÄŸilse `X-API-Key` header boÅŸ kalsa da istekler baÅŸarÄ±lÄ± olur.

## ğŸ” GÃ¼venlik ve CORS (GÃ¼ncel)

- API Key (opsiyonel): `X-API-Key` headerâ€™Ä± ile doÄŸrulama (yalnÄ±zca POST isteklerinde, `Security:ApiKey:Enabled=true` ise).
- CORS: `Cors:AllowedOrigins` ile daraltÄ±lmÄ±ÅŸ; Ã¶rneÄŸin `http://localhost:5173` gibi belirli kaynaklara izin verilir. Liste boÅŸsa geliÅŸtirme kolaylÄ±ÄŸÄ± iÃ§in tÃ¼m kaynaklara izin verilir.
- ProblemDetails: Ãœretimde beklenmeyen hatalar RFC7807 uyumlu JSON problem yanÄ±tÄ± olarak dÃ¶ner.
- HTTPS yÃ¶nlendirme: `UseHttpsRedirection` aktif.

## ğŸ†• Yeni UÃ§ Noktalar ve SaÄŸlÄ±k Kontrolleri

- `POST /predict-pooled` â€” YÃ¼ksek eÅŸzamanlÄ±lÄ±k iÃ§in PredictionEnginePool kullanÄ±r.
- `GET /health/live` â€” Process canlÄ± mÄ± (liveness).
- `GET /health/ready` â€” Model ve DB dizini hazÄ±r mÄ± (readiness).

Ã–rnek Postman istekleri koleksiyonda mevcuttur.

## ğŸ“ˆ Model PerformansÄ±

- **RÂ² Skoru**: 0.92
- **MAE**: 0.45 dakika
- **RMSE**: 0.67 dakika
- **EÄŸitim SÃ¼resi**: 2.3 saniye

## ğŸ“ Proje YapÄ±sÄ±

```
MLPickingEstimator/
â”œâ”€â”€ Models/           # Veri modelleri
â”œâ”€â”€ Services/         # ML servisleri
â”œâ”€â”€ Data/            # Ã–rnek veri seti
â”œâ”€â”€ Scripts/         # Python ONNX dÃ¶nÃ¼ÅŸtÃ¼rÃ¼cÃ¼
â”œâ”€â”€ Program.cs       # Web API
â””â”€â”€ README.md        # Bu proje dokÃ¼mantasyonu
â””â”€â”€ wwwroot/         # Web arayÃ¼zÃ¼ (personnel-assign.html: gÃ¶rev atama)
```

## ğŸ§  ML Pipeline

1. **Veri YÃ¼kleme**: CSV dosyasÄ±ndan veri okuma
2. **Veri BÃ¶lme**: %80 eÄŸitim, %20 test
3. **Ã–zellik BirleÅŸtirme**: TÃ¼m Ã¶zellikleri tek vektÃ¶rde toplama
4. **Normalizasyon**: Min-Max normalizasyonu
5. **Model EÄŸitimi**: FastTree algoritmasÄ±
6. **DeÄŸerlendirme**: Performans metrikleri

## ğŸ¤– AutoML

Otomatik olarak en iyi algoritmayÄ± seÃ§er:
- FastTree
- FastForest
- LinearRegression
- SdcaRegression
- GamRegression

## ğŸ”— ONNX Entegrasyonu

Python'da eÄŸitilmiÅŸ modelleri C# ile kullanabilirsiniz:

```python
# Python'da ONNX'e Ã§evir
from skl2onnx import convert_sklearn
onnx_model = convert_sklearn(model, initial_types=[('input', FloatTensorType([None, 6]))])
```

```csharp
// C#'da ONNX kullan
var session = new InferenceSession("model.onnx");
var result = session.Run(inputs);
```

## ğŸ“š DokÃ¼mantasyon

- [DetaylÄ± Makale](PROFESSIONAL_MLPickingEstimator.md) - KapsamlÄ± teknik dokÃ¼mantasyon
- [README](README.md) - Proje genel bilgileri
- [API DokÃ¼mantasyonu](API.md) - Endpoint detaylarÄ±

### Demo ArayÃ¼z

- `GET /warehouse` yÃ¶nlendirmesi ile `personnel-assign.html` arayÃ¼zÃ¼ne ulaÅŸÄ±p araÃ§ ve gÃ¶revleri girerek `POST /optimize-dispatch` ile atama ve rota optimizasyonu yapÄ±lÄ±r.
- Animasyonlu SVG arayÃ¼zleri ve Ã¶rnekleri kaldÄ±rÄ±lmÄ±ÅŸtÄ±r; yerini gÃ¶rev atama arayÃ¼zÃ¼ almÄ±ÅŸtÄ±r.

### Dispatch ve Raf-ID Girdisi (GÃ¼ncel)

- `POST /optimize-dispatch` aÅŸaÄŸÄ±daki alanlarÄ± destekler:
  - `vehicles`: `[{ id, currentLocation:{x,y}, currentLocationCode?, capacityKg?, capacityCases?, zone? }]`
  - `jobs`: `[{ id, rackId?, location:{x,y}?, quantity?, weightKg?, priority?, earliestTime?, latestTime?, zone?, orderId?, corridor? }]`
    - `rackId` verildiÄŸinde koordinat ve koridor sunucu tarafÄ±ndan Ã§Ã¶zÃ¼lÃ¼r (`LocationResolver`), `corridor` boÅŸsa raf kodundan tÃ¼retilir.
  - `averageSpeedMps?` (m/s, varsayÄ±lan 1.2), `handlingSecondsPerTask?` (saniye, varsayÄ±lan 15)
  - Kurallar: Zone bazlÄ± atama; koli ve kg kapasite kontrolÃ¼; aynÄ± koridor iÅŸleri gruplanÄ±r; aynÄ± sipariÅŸ + aynÄ± zone iÅŸleri mÃ¼mkÃ¼nse aynÄ± robota atanÄ±r; zaman pencereleri uygunsa Ã¶nceliklendirilir.
  - YanÄ±tta `estimatedTimeSeconds` ve `assignedJobCount` dÃ¶ner.
- Ä°stemci doÄŸrudan raf kimliÄŸi (`PX-MZ-D08-171F` vb.) gÃ¶nderebilir; koordinata/koridor Ã§Ã¶zÃ¼mlemesi sunucu iÃ§inde yapÄ±lÄ±r.

### KÄ±sa KullanÄ±m KÄ±lavuzu

- Zaman Pencereleri:
  - `jobs[].earliestTime` ve `jobs[].latestTime` ile dar pencereli iÅŸleri belirtin.
  - Pencere daraldÄ±kÃ§a iÅŸin maliyeti artar; sistem yakÄ±n/uygun araÃ§larÄ± Ã¶nceliklendirir.
- Enerji Modu:
  - `energyMode: true` ve `energyWeight: 0â€“1` ayarÄ± ile dÃ¼ÅŸÃ¼k bataryalÄ± araÃ§larÄ±n daha yakÄ±n hedefleri seÃ§mesini saÄŸlarsÄ±nÄ±z.
  - AraÃ§ta `batteryPercent` belirtmek etkisinin Ã¶lÃ§Ã¼lmesini saÄŸlar.
- Koridor Gruplama:
  - `corridor` vermezseniz `rackId` Ã¼zerinden otomatik Ã§Ä±karÄ±lÄ±r ve aynÄ± koridordaki iÅŸler mÃ¼mkÃ¼nse aynÄ± araca gruplanÄ±r.
- Zone BazlÄ± Atama:
  - `vehicles[].zone` ve `jobs[].zone` eÅŸleÅŸirse aday havuzu daraltÄ±lÄ±r; yanlÄ±ÅŸ zoneâ€™daki iÅŸler araÃ§ tarafÄ±ndan gÃ¶rÃ¼lmez.

### Ä°ÅŸ Listesi Ingest ve Export

- Ä°ÅŸleri kaydet:
```bash
curl -X POST "http://localhost:5000/jobs/ingest" -H "Content-Type: application/json" -d '{
  "jobs": [
    {"id":"job-1","location":{"x":110,"y":85},"weightKg":5,"priority":1,"zone":"A","orderId":"ORD-1001","corridor":"D08"},
    {"id":"job-2","location":{"x":460,"y":165},"weightKg":4,"priority":1,"zone":"A","orderId":"ORD-1001","corridor":"D08"}
  ]
}'
```

- CSV indir:
```bash
curl -L "http://localhost:5000/jobs/export?format=csv" -o jobs.csv
```

- Excel (XLSX) indir:
```bash
curl -L "http://localhost:5000/jobs/export?format=xlsx" -o jobs.xlsx
```

## ğŸ” Production HazÄ±rlÄ±klarÄ± (Yeni)

- HTTP Logging aktif: Ä°stek/yanÄ±t baÅŸlÄ±klarÄ± ve temel Ã¶zellikler loglanÄ±r.
- JSON yanÄ±tlar camelCase: alan adlarÄ± camelCase dÃ¶ner.
- Rate Limiting politikalarÄ± ile kÃ¶tÃ¼ye kullanÄ±m Ã¶nlenir.
- Output Caching ile GET yanÄ±tlarÄ± TTL sÃ¼resince hÄ±zlÄ± dÃ¶ner.

## ğŸ›°ï¸ CanlÄ± Telemetry (Yeni)

- Personel performans telemetrisi:
  - `POST /telemetry` â€” personel performans verileri (itemsPicked, hoursWorked, errors, travelDistance)
  - `GET /personnel/performance/live` â€” canlÄ± PF (Performance Factor) hesaplama

### HÄ±zlÄ± Deneme

```bash
curl -X POST "http://localhost:5000/telemetry" -H "Content-Type: application/json" -d '{
  "personId":"P001","itemsPicked":120,"hoursWorked":8,"errors":2,"travelDistanceMeters":1500
}'

curl "http://localhost:5000/personnel/performance/live"
```

## ğŸ› ï¸ GeliÅŸtirme

### Gereksinimler
- .NET 9.0 SDK
- Visual Studio 2022 veya VS Code

### Test Etme
```bash
dotnet test
```

### Build
```bash
dotnet build --configuration Release
```

## ğŸ§© Opsiyonel Toplama Kriterleri (Yeni)

- `fragileRatio` â€” KÄ±rÄ±lgan Ã¼rÃ¼n oranÄ± (0-1)
- `aisleCrowding` â€” Koridor yoÄŸunluk indeksi (0-1)
- `cartLoadKg` â€” Sepet/araÃ§ yÃ¼kÃ¼ (kg)
- `pickerFatigue` â€” ToplayÄ±cÄ± yorgunluk seviyesi (0-1)
- `stopCount` â€” Ziyaret edilecek durak sayÄ±sÄ±
- `urgencyLevel` â€” Ä°ÅŸ aciliyet seviyesi (0-1)
- `zoneComplexity` â€” Zone karmaÅŸÄ±klÄ±ÄŸÄ± (0-1)
- `temperatureHandling` â€” SÄ±caklÄ±k kontrollÃ¼ Ã¼rÃ¼n zorluÄŸu (0-1)

Notlar:
- Bu alanlar opsiyoneldir; gÃ¶nderilmezse 0 kabul edilir.
- EÄŸitim pipelineâ€™Ä± mevcut haliyle Ã§alÄ±ÅŸÄ±r; veri zenginleÅŸtikÃ§e pipelineâ€™a eklenecektir.

## ğŸ“ Ä°letiÅŸim

**Ahmet Sezer Dindin**  
Email: ahmet@example.com  
LinkedIn: [linkedin.com/in/ahmetsezerdindin](https://linkedin.com/in/ahmetsezerdindin)

## ğŸ“„ Lisans

MIT License - Detaylar iÃ§in [LICENSE](LICENSE) dosyasÄ±na bakÄ±n.

---

*Bu proje, C# ve ML.NET ile makine Ã¶ÄŸrenmesi uygulamalarÄ± geliÅŸtirmek isteyen geliÅŸtiriciler iÃ§in kapsamlÄ± bir Ã¶rnek sunmaktadÄ±r.*
## GÃ¶rev Atama ArayÃ¼zÃ¼ ve Ã–rnekler (Yeni)

- UI: `http://localhost:5000/personnel-assign.html`
- Ã–rnek GÃ¶rev CSV: `http://localhost:5000/assets/samples/tasks-sample.csv`
- Excel GÃ¶rev Åablonu (dinamik): `http://localhost:5000/samples/tasks.xlsx`
- CanlÄ± PF Telemetri GÃ¶nder: `POST http://localhost:5000/telemetry`
- CanlÄ± PF SonuÃ§larÄ±: `GET http://localhost:5000/personnel/performance/live`

CSV/Excel baÅŸlÄ±klarÄ± `PersonnelDispatchService`â€™deki eÅŸlemeden alÄ±nÄ±r. Ã–rnek CSV dosyasÄ± ve Excel endpointi bu baÅŸlÄ±klarla uyumludur.
### Rota SimÃ¼lasyonu Kod DesteÄŸi

- `POST /simulate-route` iki tarz giriÅŸ destekler:
  - Koordinat: `{ lastLocation:{x,y}, taskLocations:[{x,y}] }`
  - Kod: `{ lastLocationCode:"PX-MZ-D01-100A", taskLocationCodes:["PX-MZ-D08-171F", ...] }`
  - YanÄ±t: `{ orderedLocations:[{x,y}], totalDistance }`

### Ã–rnek Ä°stekler (Mini Snippet)

Rota simÃ¼lasyonu â€” Kod tabanlÄ±:

```bash
curl -X POST "http://localhost:5000/simulate-route" -H "Content-Type: application/json" -d '{
  "lastLocationCode":"PX-MZ-D01-100A",
  "taskLocationCodes":["PX-MZ-D08-171F","PX-MC-D03-090B"]
}'
```

Lokasyon ile tek seferlik tahmin â€” Kod tabanlÄ± (Yeni):

```bash
curl -X POST "http://localhost:5000/predict-with-locations" -H "Content-Type: application/json" -d '{
  "itemCount": 12,
  "weight": 6.0,
  "volume": 1.8,
  "pickerExperience": 3,
  "stockDensity": 0.85,
  "lastLocationCode": "PX-MC-D03-090B",
  "taskLocationCode": "PX-MZ-D08-171F",
  "fragileRatio": 0.2,
  "aisleCrowding": 0.4
}'
```

Notlar:
- `distanceOverride` verildiÄinde lokasyondan mesafe hesaplanmaz; doÄrudan bu deÄer kullanÄ±lÄ±r.
- Kod yerine koordinat saÄlamak iÃ§in `lastLocation` ve `taskLocation` alanlarÄ±nÄ± kullanabilirsiniz.

Rota simÃ¼lasyonu â€” Koordinat tabanlÄ±:

```bash
curl -X POST "http://localhost:5000/simulate-route" -H "Content-Type: application/json" -d '{
  "lastLocation": {"x":460, "y":140},
  "taskLocations": [{"x":110, "y":90}, {"x":200, "y":180}]
}'
```

Dispatch optimizasyonu â€” AraÃ§lar kodla, iÅŸler raf kimliÄŸiyle:

```bash
curl -X POST "http://localhost:5000/optimize-dispatch" -H "Content-Type: application/json" -d '{
  "vehicles": [
    {"id":"V1","currentLocationCode":"PX-MC-D03-090B","zone":"MC"},
    {"id":"V2","currentLocationCode":"PX-MZ-D08-171F","zone":"MZ"}
  ],
  "jobs": [
    {"id":"J1","rackId":"PX-MC-D03-090B"},
    {"id":"J2","rackId":"PX-MZ-D08-171F"}
  ]
}'
```

Notlar:
- `rackId` verildiÄŸinde koordinat ve `corridor` sunucu iÃ§inde Ã§Ã¶zÃ¼lÃ¼r; `corridor` boÅŸsa raf kodundan otomatik Ã§Ä±karÄ±lÄ±r.
- `earliestTime`/`latestTime` ile dar pencereli iÅŸlerin maliyeti artar; planlama buna gÃ¶re dengelenir.


