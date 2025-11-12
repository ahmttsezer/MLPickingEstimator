# Opsiyonel Alanlar Model Entegrasyon Planı

Bu plan, `PredictionRequest` içine eklenen opsiyonel alanların (örneğin `FragileRatio`, `AisleCrowding`, `CartLoadKg`, `PickerFatigue`, `StopCount`, `UrgencyLevel`, `ZoneComplexity`, `TemperatureHandling`) ML modeline ve ONNX hattına entegrasyonunu kapsar.

## Amaç
- Tahmin doğruluğunu artırmak için yeni özellikleri eğitim verisine ve modele dahil etmek.
- API uçlarının bu alanları güvenli şekilde kabul edip normalize etmesini sağlamak.

## Adımlar
1. Özellik seti güncellemesi
   - `Models/ProductPickingData.cs` içinde özellik vektörünü doğrulayın.
   - Eğitim veri şemasını (`Data/picking_data.csv`) yeni kolonlarla genişletin.

2. Veri hazırlama ve normalizasyon
   - `MLPickingService` içinde veri yüklerken yeni kolonları okuyun.
   - 0–1 aralığındaki alanlar için min-max doğrulaması ve gerektiğinde klipsleme uygulayın.
   - `CartLoadKg` gibi mutlak değerleri z-skor veya min-max ile ölçekleyin.

3. Model eğitimi
   - Feature setini güncellediğiniz pipeline ile FastTree, LightGBM ve SdcaRegression alternatiflerini eğitin.
   - `AdvancedMLPickingService` içinde karşılaştırma metriklerini (RMSE, MAE, R²) raporlayın.

4. ONNX dönüşümü
   - `Scripts/convert_to_onnx.py` ile güncel modeli ONNX’e dönüştürün.
   - `OnnxService` giriş vektörünü yeni özellik sıralamasıyla eşleştirin.

5. API ve doğrulama
   - `Program.cs` içinde opsiyonel alanların doğrulamasını sürdürün; eksikse `0f` default verin.
   - Hata durumlarında açıklayıcı log üretin (ValidationError, BindingError).

6. Test ve izleme
   - `/predict`, `/predict-batch`, `/predict-onnx` uçları için smoke testleri ve örnek istekler ekleyin.
   - `ModelMonitoringService` ile dağılım ve performans takibi yapın.

## Beklenen Çıktılar
- Genişletilmiş eğitim verisi ve güncel model dosyaları.
- ONNX modeli ve uyumlu API entegrasyonu.
- İyileştirilmiş tahmin performansı ve daha zengin kriterlerle personel ataması.