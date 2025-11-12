using Microsoft.ML.Data;

namespace MLPickingEstimator.Models
{
    /// <summary>
    /// Depo operasyonları için ürün toplama veri modeli
    /// </summary>
    public class ProductPickingData
    {
        /// <summary>
        /// Toplanacak ürün sayısı
        /// </summary>
        [LoadColumn(0)]
        public float ItemCount { get; set; }

        /// <summary>
        /// Ürün ağırlığı (kg)
        /// </summary>
        [LoadColumn(1)]
        public float Weight { get; set; }

        /// <summary>
        /// Ürün hacmi (m³)
        /// </summary>
        [LoadColumn(2)]
        public float Volume { get; set; }

        /// <summary>
        /// Depo içi mesafe (metre)
        /// </summary>
        [LoadColumn(3)]
        public float Distance { get; set; }

        /// <summary>
        /// Toplayıcı deneyim seviyesi (1-10)
        /// </summary>
        [LoadColumn(4)]
        public float PickerExperience { get; set; }

        /// <summary>
        /// Stok yoğunluğu (0-1 arası)
        /// </summary>
        [LoadColumn(5)]
        public float StockDensity { get; set; }

        /// <summary>
        /// Gerçek toplama süresi (dakika) - Label
        /// </summary>
        [LoadColumn(6)]
        public float PickingTime { get; set; }

        // Opsiyonel ek kriterler (CSV eğitim dosyasına bağlı değildir)
        // Gönderilmezse 0 kabul edilir.
        /// <summary>
        /// Kırılgan ürün oranı (0-1) — eğitim CSV'sinde yok, sadece runtime kullanım için
        /// </summary>
        public float FragileRatio { get; set; }
        /// <summary>
        /// Koridor yoğunluk indeksi (0-1)
        /// </summary>
        public float AisleCrowding { get; set; }
        /// <summary>
        /// Sepet/araç yükü (kg)
        /// </summary>
        public float CartLoadKg { get; set; }
        /// <summary>
        /// Toplayıcı yorgunluk seviyesi (0-1)
        /// </summary>
        public float PickerFatigue { get; set; }
        /// <summary>
        /// Ziyaret edilecek durak sayısı
        /// </summary>
        public float StopCount { get; set; }
        /// <summary>
        /// İşin aciliyet seviyesi (0-1)
        /// </summary>
        public float UrgencyLevel { get; set; }
        /// <summary>
        /// Zone karmaşıklığı (0-1)
        /// </summary>
        public float ZoneComplexity { get; set; }
        /// <summary>
        /// Sıcaklık kontrollü ürün zorluğu (0-1)
        /// </summary>
        public float TemperatureHandling { get; set; }
    }

    /// <summary>
    /// ML.NET tahmin sonucu modeli
    /// </summary>
    public class PickingTimePrediction
    {
        /// <summary>
        /// Tahmin edilen toplama süresi
        /// </summary>
        [ColumnName("Score")]
        public float PredictedTime { get; set; }
    }

    /// <summary>
    /// API için tahmin isteği modeli
    /// </summary>
    public class PredictionRequest
    {
        public float ItemCount { get; set; }
        public float Weight { get; set; }
        public float Volume { get; set; }
        public float Distance { get; set; }
        public float PickerExperience { get; set; }
        public float StockDensity { get; set; }

        // Opsiyonel alanlar (gönderilmezse 0 varsayılır)
        public float? FragileRatio { get; set; }
        public float? AisleCrowding { get; set; }
        public float? CartLoadKg { get; set; }
        public float? PickerFatigue { get; set; }
        public float? StopCount { get; set; }
        public float? UrgencyLevel { get; set; }
        public float? ZoneComplexity { get; set; }
        public float? TemperatureHandling { get; set; }
    }

    /// <summary>
    /// API için tahmin yanıtı modeli
    /// </summary>
    public class PredictionResponse
    {
        public float PredictedTime { get; set; }
        public float Confidence { get; set; }
        public string ModelVersion { get; set; } = "1.0";
        public DateTime PredictionTime { get; set; } = DateTime.UtcNow;
        public string Algorithm { get; set; } = "FastTree";
    }

    /// <summary>
    /// Lokasyon bilgileriyle birlikte tahmin isteği modeli
    /// </summary>
    public class PredictionWithLocationsRequest
    {
        // Ana özellikler
        public float ItemCount { get; set; }
        public float Weight { get; set; }
        public float Volume { get; set; }
        public float PickerExperience { get; set; }
        public float StockDensity { get; set; }

        // Lokasyon bilgileri (kod veya koordinat)
        public string? LastLocationCode { get; set; }
        public string? TaskLocationCode { get; set; }
        public LocationPoint? LastLocation { get; set; }
        public LocationPoint? TaskLocation { get; set; }

        // İsteğe bağlı: Mesafe manuel verildiyse lokasyondan hesaplanmaz
        public float? DistanceOverride { get; set; }

        // Opsiyonel alanlar (gönderilmezse 0 varsayılır)
        public float? FragileRatio { get; set; }
        public float? AisleCrowding { get; set; }
        public float? CartLoadKg { get; set; }
        public float? PickerFatigue { get; set; }
        public float? StopCount { get; set; }
        public float? UrgencyLevel { get; set; }
        public float? ZoneComplexity { get; set; }
        public float? TemperatureHandling { get; set; }
    }

    /// <summary>
    /// Model performans metrikleri
    /// </summary>
    public class ModelMetrics
    {
        public double RSquared { get; set; }
        public double MeanAbsoluteError { get; set; }
        public double RootMeanSquaredError { get; set; }
        public double Loss { get; set; }
        public string TrainerName { get; set; } = string.Empty;
        public TimeSpan TrainingTime { get; set; }
    }
}
