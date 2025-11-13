# MLPickingEstimator — Hızlı Başlangıç ve GitHub Yayına Hazırlık

Bu depo, ML.NET tabanlı bir tahmin ve görev atama uygulamasını içerir. Aşağıdaki adımlar projeyi yerelde çalıştırmanızı, UI ve API’yi doğrulamanızı ve GitHub’a yüklemeye hazır hale getirmenizi sağlar.

## Gereksinimler
- `NET SDK 9.0` (dotnet 9)
- Windows 10/11, PowerShell

## Kurulum
- Depoyu klonlayın ve paketleri geri yükleyin:
  - `git clone <repository-url>`
  - `dotnet restore`

## Çalıştırma (Geliştirme)
- Web API’yi ve statik UI’yi başlatın:
  - `cd MLPickingEstimator/MLPickingEstimator`
  - `dotnet run --urls http://localhost:5100`
- UI’yi açın:
  - `http://localhost:5100/personnel-assign.html`

## Yayın ve .exe ile Çalıştırma (Son Kullanıcılar)
- Self-contained Windows yayın (geliştirici tarafı):
  - `dotnet publish MLPickingEstimator/MLPickingEstimator.csproj -c Release -r win-x64 --self-contained true`
- Çıktı dizini: `publish/win-x64/`
- Çalıştırma:
  - `publish/win-x64/MLPickingEstimator.exe`
- Varsayılan URL: `http://localhost:5000/`
- Sağlık kontrolü:
  - `http://localhost:5000/health` ve `http://localhost:5000/healthz`
- UI kısa yolu:
  - `http://localhost:5000/warehouse` (otomatik `personnel-assign.html` sayfasına yönlendirir)
- Notlar:
  - `.exe` self-contained olduğu için .NET runtime kurulu olması gerekmez.
  - Port çakışması veya güvenlik duvarı engeli varsa `ASPNETCORE_URLS` ile port değiştirilebilir (örn. `http://localhost:5200`).
  - Büyük model dosyaları Git LFS ile yönetilir; yayın klasörü tüm bağımlılıkları içerir.

## Canlı Doğrulama
- Personel bilgileri: `GET http://localhost:5100/personnel`
- Son lokasyonlar: `GET http://localhost:5100/personnel/locations`
- Haftalık performans: `GET http://localhost:5100/personnel/performance`
- Görev atama örneği: `POST http://localhost:5100/assign-picking`
  - Gövde örneği:
    ```json
    {
      "tasks": [
        {"taskId": 1, "firstLocation": "PX-MZ-D08-171F", "todoQuantity": 6},
        {"taskId": 2, "firstLocation": "PX-MC-A106", "todoQuantity": 4}
      ],
      "personnel": [
        {"id": "p1", "name": "Ayşe", "lastLocationCode": "PX-MZ-D08-171F", "pickerExperience": 3, "speedFactor": 1.0},
        {"id": "p2", "name": "Mehmet", "lastLocationCode": "PX-MC-A106", "pickerExperience": 2, "speedFactor": 0.9}
      ]
    }
    ```

## Test ve Release Derleme
- Testleri çalıştır: `dotnet test`
- Release derleme: `dotnet build -c Release`
 - Yayın al ve tek klasörden çalıştır: `dotnet publish -c Release -r win-x64 --self-contained true`
 - Çalıştır: `./publish/win-x64/MLPickingEstimator.exe`

## Ek Dokümanlar
- Detaylı API için: `MLPickingEstimator/API.md`
- Postman koleksiyonu: `Docs/MLPickingEstimator.postman_collection.json`
- Makale: `makale.md`

---
# C# ile Machine Learning: ML.NET ile Depo Operasyonları Tahmin Motoru

**Yazar:** Ahmet Sezer Dindin  
**Etiketler:** C#, .NET, ML.NET, Machine Learning, AI, Predictive Analytics, ONNX, AutoML

## ğŸ¯ Proje Ã–zeti

Bu proje, Microsoft'un ML.NET framework'Ã¼ kullanÄ±larak geliÅŸtirilmiÅŸ kapsamlÄ± bir makine Ã¶ÄŸrenmesi uygulamasÄ±dÄ±r. Depo operasyonlarÄ±nda Ã¼rÃ¼n toplama sÃ¼relerini tahmin etmek iÃ§in tasarlanmÄ±ÅŸtÄ±r.

## ğŸš€ Ã–zellikler

- **ML.NET Pipeline**: FastTree algoritmasÄ± ile regression modeli
- **AutoML Entegrasyonu**: Otomatik algoritma seÃ§imi
- **REST API**: Web servisi ile tahmin sunumu
- **ONNX DesteÄŸi**: Python modelleri ile entegrasyon
- **Konsol UygulamasÄ±**: Model eÄŸitimi ve test
- **DetaylÄ± Metrikler**: Model performans analizi
- **Batch Tahmin**: Birden fazla isteÄŸi toplu iÅŸleme
- **SaÄŸlÄ±k KontrolÃ¼**: Servis durumu ve model yÃ¼kÃ¼ izleme

## ğŸ“ Proje YapÄ±sÄ±

```
MLPickingEstimator/
â”œâ”€â”€ MLPickingEstimator/                # Web API projesi
â”‚   â”œâ”€â”€ Models/
â”‚   â”‚   â””â”€â”€ ProductPickingData.cs     # Veri modelleri
â”‚   â”œâ”€â”€ Services/
â”‚   â”‚   â”œâ”€â”€ MLPickingService.cs       # Ana ML servisi
â”‚   â”‚   â”œâ”€â”€ AutoMLService.cs          # AutoML servisi
â”‚   â”‚   â””â”€â”€ OnnxService.cs            # ONNX entegrasyonu
â”‚   â”œâ”€â”€ Data/
â”‚   â”‚   â””â”€â”€ picking_data.csv          # Ã–rnek veri seti
â”‚   â”œâ”€â”€ Scripts/
â”‚   â”‚   â””â”€â”€ convert_to_onnx.py        # Python ONNX dÃ¶nÃ¼ÅŸtÃ¼rÃ¼cÃ¼
â”‚   â”œâ”€â”€ Program.cs                     # Web API ana dosyasÄ±
â”‚   â””â”€â”€ MLPickingEstimator.csproj      # Proje dosyasÄ±
â”œâ”€â”€ MLConsoleApp/                      # Konsol uygulamasÄ±
â”‚   â”œâ”€â”€ Program.cs                     # Konsol ana dosyasÄ±
â”‚   â””â”€â”€ MLConsoleApp.csproj            # Proje dosyasÄ±
â”œâ”€â”€ MLPickingEstimator.sln             # Solution dosyasÄ±
â”œâ”€â”€ README.md                          # Ana dokÃ¼mantasyon
â”œâ”€â”€ PROFESSIONAL_MLPickingEstimator.md                          # DetaylÄ± makale
â””â”€â”€ LICENSE                            # MIT lisansÄ±
```

## ğŸ› ï¸ Kurulum ve Ã‡alÄ±ÅŸtÄ±rma

### Gereksinimler
- .NET 8.0 SDK
- Visual Studio 2022 veya VS Code

### AdÄ±mlar

1. **Projeyi klonlayÄ±n:**
```bash
git clone <repository-url>
cd MLPickingEstimator\\MLPickingEstimator
```

2. **Paketleri yÃ¼kleyin:**
```bash
dotnet restore MLPickingEstimator.sln
```

3. **Konsol uygulamasÄ±nÄ± Ã§alÄ±ÅŸtÄ±rÄ±n:**
```bash
dotnet run --project MLConsoleApp
```

4. **Web API'yi baÅŸlatÄ±n:**
```bash
dotnet run --project MLPickingEstimator
```

## ğŸ“Š Veri Modeli

### ProductPickingData
- `ItemCount`: Toplanacak Ã¼rÃ¼n sayÄ±sÄ±
- `Weight`: ÃœrÃ¼n aÄŸÄ±rlÄ±ÄŸÄ± (kg)
- `Volume`: ÃœrÃ¼n hacmi (mÂ³)
- `Distance`: Depo iÃ§i mesafe (metre)
- `PickerExperience`: ToplayÄ±cÄ± deneyim seviyesi (1-10)
- `StockDensity`: Stok yoÄŸunluÄŸu (0-1)
- `PickingTime`: GerÃ§ek toplama sÃ¼resi (dakika) - Label

## ğŸ”§ API Endpoints

### POST /predict
Tahmin yapar.

**Ä°stek:**
```json
{
  "itemCount": 12,
  "weight": 6.0,
  "volume": 1.8,
  "distance": 90,
  "pickerExperience": 3,
  "stockDensity": 0.85
}
```

**YanÄ±t:**
```json
{
  "predictedTime": 5.23,
  "confidence": 0.85,
  "modelVersion": "1.0",
  "predictionTime": "2024-01-15T10:30:00Z"
}
```

### POST /train
Modeli yeniden eÄŸitir.

### GET /metrics
Model performans metriklerini dÃ¶ner.

### POST /predict-batch
Birden fazla tahmin isteÄŸini toplu olarak iÅŸler.

**Ä°stek:**
```json
[
  { "itemCount": 12, "weight": 6.0, "volume": 1.8, "distance": 90, "pickerExperience": 3, "stockDensity": 0.85 },
  { "itemCount": 25, "weight": 10.0, "volume": 3.0, "distance": 120, "pickerExperience": 4, "stockDensity": 0.92 }
]
```

**YanÄ±t:**
```json
[
  { "predictedTime": 5.23, "confidence": 0.85, "modelVersion": "1.0", "predictionTime": "2024-01-15T10:30:00Z", "algorithm": "FastTree" },
  { "predictedTime": 8.11, "confidence": 0.85, "modelVersion": "1.0", "predictionTime": "2024-01-15T10:30:00Z", "algorithm": "FastTree" }
]
```

### GET /health
Servis saÄŸlÄ±k durumunu dÃ¶ner.

**YanÄ±t:**
```json
{ "status": "Healthy", "timestamp": "2024-01-15T10:30:00Z" }
```

## ğŸ§  ML.NET Pipeline

```csharp
var pipeline = mlContext.Transforms.Concatenate("Features",
                    nameof(ProductPickingData.ItemCount),
                    nameof(ProductPickingData.Weight),
                    nameof(ProductPickingData.Volume),
                    nameof(ProductPickingData.Distance),
                    nameof(ProductPickingData.PickerExperience),
                    nameof(ProductPickingData.StockDensity))
                .Append(mlContext.Transforms.NormalizeMinMax("Features"))
                .Append(mlContext.Regression.Trainers.FastTree(labelColumnName: "PickingTime"));
```

## ğŸ¤– AutoML KullanÄ±mÄ±

```csharp
var experimentSettings = new RegressionExperimentSettings
{
    MaxExperimentTimeInSeconds = 60,
    OptimizingMetric = RegressionMetric.RSquared
};

var experiment = mlContext.Auto().CreateRegressionExperiment(experimentSettings);
var result = experiment.Execute(data, labelColumnName: "PickingTime");
```

## ğŸ”— ONNX Entegrasyonu

### Python'dan ONNX'e DÃ¶nÃ¼ÅŸtÃ¼rme
```python
from skl2onnx import convert_sklearn
from skl2onnx.common.data_types import FloatTensorType

onnx_model = convert_sklearn(trained_model, 
    initial_types=[('input', FloatTensorType([None, 6]))])
```

### C#'da ONNX KullanÄ±mÄ±
```csharp
var session = new InferenceSession("model.onnx");
var input = new DenseTensor<float>(inputData, new[] { 1, 6 });
var inputs = new List<NamedOnnxValue> 
{ 
    NamedOnnxValue.CreateFromTensor("input", input) 
};
```

## ğŸ“ˆ Performans Metrikleri

- **RÂ² Skoru**: Model aÃ§Ä±klama gÃ¼cÃ¼
- **MAE**: Ortalama Mutlak Hata
- **RMSE**: KÃ¶k Ortalama Kare Hata
- **EÄŸitim SÃ¼resi**: Model eÄŸitim sÃ¼resi

## ğŸš€ Ãœretim Ã–nerileri

1. **Veri KaynaÄŸÄ±**: SQL Server entegrasyonu
2. **Performans**: PredictionEnginePool kullanÄ±mÄ±
   - Not: PredictionEngine thread-safe deÄŸildir; APIâ€™de her tahmin iÃ§in yeni engine oluÅŸturulur.
3. **GÃ¼venlik**: JWT veya API Key doÄŸrulama
4. **Monitoring**: Model drift takibi
5. **CI/CD**: Otomatik model yeniden eÄŸitimi

## ğŸ“š Ã–ÄŸrenme KaynaklarÄ±

- [ML.NET DokÃ¼mantasyonu](https://docs.microsoft.com/en-us/dotnet/machine-learning/)
- [AutoML Rehberi](https://docs.microsoft.com/en-us/dotnet/machine-learning/how-to-guides/how-to-use-the-automl-api)
- [ONNX Runtime](https://onnxruntime.ai/)

## ğŸ¤ KatkÄ±da Bulunma

1. Fork yapÄ±n
2. Feature branch oluÅŸturun (`git checkout -b feature/AmazingFeature`)
3. Commit yapÄ±n (`git commit -m 'Add some AmazingFeature'`)
4. Push yapÄ±n (`git push origin feature/AmazingFeature`)
5. Pull Request oluÅŸturun

## ğŸ“„ Lisans

Bu proje MIT lisansÄ± altÄ±nda lisanslanmÄ±ÅŸtÄ±r. Detaylar iÃ§in `LICENSE` dosyasÄ±na bakÄ±n.

## ğŸ“ Ä°letiÅŸim

**Ahmet Sezer Dindin**  
Email: ahmet@example.com  
LinkedIn: [linkedin.com/in/ahmetsezerdindin](https://linkedin.com/in/ahmetsezerdindin)

---

*Bu proje, C# ve ML.NET ile makine Ã¶ÄŸrenmesi uygulamalarÄ± geliÅŸtirmek isteyen geliÅŸtiriciler iÃ§in kapsamlÄ± bir Ã¶rnek sunmaktadÄ±r.*

## 🚀 HÄ±zlÄ± DoÄŸrulama AdÄ±mlarÄ±

- Proje kÃ¶kÃ¼nde derle: `dotnet build`
- Web API klasÃ¶rÃ¼ne geÃ§: `cd MLPickingEstimator/MLPickingEstimator`
- Sunucuyu baÅŸlat: `dotnet run --urls http://localhost:5100`
- SaÄŸlÄ±k kontrolÃ¼: `GET http://localhost:5100/healthz`
- Ã–rnek tahmin:
  - `POST http://localhost:5100/predict` ve gÃ¶vde olarak:
    ```json
    {"ItemCount":12,"Weight":8.5,"Volume":3.2,"Distance":1500,"PickerExperience":4,"StockDensity":0.65}
    ```
- Drift metrikleri:
  - `GET http://localhost:5100/metrics-full?thresholdPercent=20`
  - `GET http://localhost:5100/drift-status?thresholdPercent=20`

Notlar:
- Ä°lk Ã§alÄ±ÅŸtÄ±rmada canlÄ± ortalamalar sÄ±fÄ±r olabilir ve `driftRatio` yÃ¼ksek gÃ¶rÃ¼nebilir; `POST /predict` Ã§aÄŸrÄ±larÄ± geldikÃ§e canlÄ± ortalamalar dolacaktÄ±r.
- Postman koleksiyonlarÄ±: `Docs/MLPickingEstimator.postman_collection.json` ve `Docs/MLPickingEstimator.postman_environment.json`.

##  ï¸ Git Ä°gnore (Repo Temizlik)

- `bin/`, `obj/`, `.vs/` gibi derleme ve IDE Ã§Ä±ktÄ±larÄ± izlenmez.
- Ã‡alÄ±ÅŸma zamanÄ±na ait `MLPickingEstimator/Data/telemetry.db` ve `Models/archive/` iÃ§eriÄŸi izlenmez.



