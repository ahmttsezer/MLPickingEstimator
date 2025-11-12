using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using MLPickingEstimator.Models;

namespace MLPickingEstimator.Services
{
    /// <summary>
    /// ONNX model entegrasyonu servisi
    /// </summary>
    public class OnnxService
    {
        private InferenceSession? _session;

        /// <summary>
        /// ONNX modelini yükler
        /// </summary>
        public void LoadModel(string modelPath)
        {
            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"ONNX model dosyası bulunamadı: {modelPath}");

            _session = new InferenceSession(modelPath);
            Console.WriteLine($"✅ ONNX model yüklendi: {modelPath}");
        }

        /// <summary>
        /// Basit giriş doğrulaması
        /// </summary>
        public void ValidateInput(ProductPickingData input)
        {
            var errors = new List<string>();
            if (input.ItemCount < 1) errors.Add("ItemCount en az 1 olmalı");
            if (input.Weight < 0) errors.Add("Weight negatif olamaz");
            if (input.Volume < 0) errors.Add("Volume negatif olamaz");
            if (input.Distance < 0) errors.Add("Distance negatif olamaz");
            if (input.PickerExperience < 1 || input.PickerExperience > 10) errors.Add("PickerExperience 1-10 aralığında olmalı");
            if (input.StockDensity < 0 || input.StockDensity > 1) errors.Add("StockDensity 0-1 aralığında olmalı");
            if (errors.Count > 0)
                throw new ArgumentException(string.Join("; ", errors));
        }

        /// <summary>
        /// ONNX modeli ile tahmin yapar (opsiyonel normalizasyon ile)
        /// </summary>
        public float Predict(ProductPickingData input, bool normalize = false, FeatureMeans? baseline = null)
        {
            if (_session == null)
                throw new InvalidOperationException("ONNX model yüklenmemiş.");

            ValidateInput(input);

            // Giriş verilerini hazırla (opsiyonel mean-centering)
            float[] inputData;
            if (normalize && baseline != null)
            {
                inputData = new float[]
                {
                    input.ItemCount - (float)baseline.ItemCount,
                    input.Weight - (float)baseline.Weight,
                    input.Volume - (float)baseline.Volume,
                    input.Distance - (float)baseline.Distance,
                    input.PickerExperience - (float)baseline.PickerExperience,
                    input.StockDensity - (float)baseline.StockDensity
                };
            }
            else
            {
                inputData = new float[]
                {
                    input.ItemCount,
                    input.Weight,
                    input.Volume,
                    input.Distance,
                    input.PickerExperience,
                    input.StockDensity
                };
            }

            var inputTensor = new DenseTensor<float>(inputData, new[] { 1, 6 });
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", inputTensor)
            };

            // Tahmin yap
            using var results = _session.Run(inputs);
            var output = results.First().AsEnumerable<float>().First();
            return output;
        }

        /// <summary>
        /// ONNX model bilgilerini gösterir
        /// </summary>
        public void ShowModelInfo()
        {
            if (_session == null)
            {
                Console.WriteLine("❌ ONNX model yüklenmemiş.");
                return;
            }

            Console.WriteLine("\n📋 ONNX Model Bilgileri:");
            Console.WriteLine(new string('=', 40));

            foreach (var input in _session.InputMetadata)
            {
                Console.WriteLine($"Giriş: {input.Key}");
                Console.WriteLine($"  Boyut: {string.Join("x", input.Value.Dimensions)}");
                Console.WriteLine($"  Tip: {input.Value.ElementType}");
            }

            foreach (var output in _session.OutputMetadata)
            {
                Console.WriteLine($"Çıkış: {output.Key}");
                Console.WriteLine($"  Boyut: {string.Join("x", output.Value.Dimensions)}");
                Console.WriteLine($"  Tip: {output.Value.ElementType}");
            }
        }

        /// <summary>
        /// Kaynakları temizler
        /// </summary>
        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}
