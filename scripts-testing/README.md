# IoT Device Simulator for MQTT Testing

Công cụ này giúp mô phỏng thiết bị IoT gửi dữ liệu đến MQTT broker. Rất hữu ích cho việc kiểm thử hệ thống giám sát IoT, influxDB, và Grafana dashboards.

## Yêu cầu

- Python 3.7 hoặc mới hơn
- Thư viện `paho-mqtt`:
  ```
  pip install paho-mqtt
  ```

## Các tính năng

- Mô phỏng một hoặc nhiều thiết bị IoT
- Dữ liệu được gửi liên tục theo khoảng thời gian cấu hình (mặc định là 1 giây)
- Các thông số được mô phỏng:
  - ID thiết bị
  - Mức pin (thay đổi ngẫu nhiên và giảm dần, tự động sạc khi xuống thấp)
  - Cường độ tín hiệu (thay đổi ngẫu nhiên)
  - Trạng thái online/offline (đôi khi ngắt kết nối để mô phỏng thực tế)
  - Vị trí thiết bị (thay đổi nhỏ để mô phỏng chuyển động)
  - Địa chỉ thiết bị (mặc định hoặc có thể tùy chỉnh)

## Cách sử dụng

### Mô phỏng một thiết bị

Chạy lệnh sau để khởi động một thiết bị:

```powershell
python iot_device_simulator.py
```

### Tùy chỉnh các thông số

Bạn có thể tùy chỉnh các thông số thiết bị và MQTT:

```powershell
python iot_device_simulator.py --device-id "my-device-001" --mqtt-host "localhost" --publish-interval 2.0
```

### Xem tất cả tùy chọn

```powershell
python iot_device_simulator.py --help
```

### Mô phỏng nhiều thiết bị

Sử dụng script `multi_device_simulator.py` để chạy nhiều thiết bị cùng lúc:

```powershell
python multi_device_simulator.py --device-count 5
```

Mỗi thiết bị sẽ chạy trong một cửa sổ console riêng biệt.

## Các tùy chọn

### iot_device_simulator.py

| Tùy chọn | Mặc định | Mô tả |
|----------|----------|-------|
| --device-id | test-device-001 | ID của thiết bị |
| --battery-max | 100.0 | Mức pin tối đa |
| --battery-min | 5.0 | Mức pin tối thiểu |
| --signal-max | -30.0 | Cường độ tín hiệu tối đa (dBm) |
| --signal-min | -120.0 | Cường độ tín hiệu tối thiểu (dBm) |
| --location-lat | 10.762622 | Vĩ độ ban đầu |
| --location-lon | 106.660172 | Kinh độ ban đầu |
| --address | 123 Đường A, Quận 1, TP.HCM | Địa chỉ thiết bị |
| --mqtt-host | mqtt.duydz.tao | Tên host của MQTT broker |
| --mqtt-port | 1883 | Port của MQTT broker |
| --mqtt-username | johnnaeder | Username MQTT |
| --mqtt-password | Taodeptrai123@ | Password MQTT |
| --mqtt-topic | devices/{device_id}/data | Topic MQTT (format string) |
| --mqtt-qos | 1 | MQTT QoS level (0, 1, 2) |
| --reconnect-delay | 5 | Thời gian chờ kết nối lại (giây) |
| --publish-interval | 1.0 | Khoảng thời gian giữa các lần gửi dữ liệu (giây) |

### multi_device_simulator.py

| Tùy chọn | Mặc định | Mô tả |
|----------|----------|-------|
| --device-count | 3 | Số lượng thiết bị cần mô phỏng |
| --device-prefix | test-device- | Tiền tố cho ID thiết bị |
| --mqtt-host | mqtt.duydz.tao | Tên host của MQTT broker |
| --mqtt-port | 1883 | Port của MQTT broker |
| --mqtt-username | johnnaeder | Username MQTT |
| --mqtt-password | Taodeptrai123@ | Password MQTT |
| --mqtt-topic | devices/{device_id}/data | Topic MQTT (format string) |
| --publish-interval | 1.0 | Khoảng thời gian giữa các lần gửi dữ liệu (giây) |

## Xem dữ liệu trong Grafana

1. Đảm bảo rằng dữ liệu được lưu vào InfluxDB
2. Cấu hình Grafana để kết nối với InfluxDB
3. Tạo dashboard trong Grafana để hiển thị dữ liệu từ thiết bị

## Cấu trúc dữ liệu gửi đi

Dữ liệu gửi đến MQTT có dạng JSON như sau:

```json
{
  "deviceId": "test-device-001",
  "timestamp": "2023-06-10T15:30:45.123456",
  "batteryLevel": 87.5,
  "signalStrength": -65.2,
  "isOnline": true,
  "location": {
    "latitude": 10.762622,
    "longitude": 106.660172
  },
  "address": "123 Đường A, Quận 1, TP.HCM",
  "batteryCharging": false
}
```
