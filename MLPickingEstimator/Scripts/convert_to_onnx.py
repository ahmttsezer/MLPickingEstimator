# Python ile ONNX Model Oluşturma

Bu dosya Python ortamında ML.NET ile eğitilmiş modeli ONNX formatına çevirmek için kullanılır.

## Kurulum
```bash
pip install scikit-learn onnx skl2onnx
```

## Kod
```python
import numpy as np
import pandas as pd
from sklearn.ensemble import RandomForestRegressor
from sklearn.model_selection import train_test_split
from sklearn.preprocessing import StandardScaler
from skl2onnx import convert_sklearn
from skl2onnx.common.data_types import FloatTensorType
import joblib

# Veriyi yükle
data = pd.read_csv('picking_data.csv')
X = data[['ItemCount', 'Weight', 'Volume', 'Distance', 'PickerExperience', 'StockDensity']]
y = data['PickingTime']

# Veriyi böl
X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42)

# Model eğit
model = RandomForestRegressor(n_estimators=100, random_state=42)
model.fit(X_train, y_train)

# Modeli kaydet
joblib.dump(model, 'python_model.pkl')

# ONNX'e çevir
initial_type = [('input', FloatTensorType([None, 6]))]
onnx_model = convert_sklearn(model, initial_types=initial_type)

# ONNX modeli kaydet
with open('model.onnx', 'wb') as f:
    f.write(onnx_model.SerializeToString())

print("✅ ONNX model oluşturuldu: model.onnx")
```

## Kullanım
Bu Python scriptini çalıştırdıktan sonra oluşan `model.onnx` dosyasını C# projesinde kullanabilirsiniz.
