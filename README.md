# Accident IoT Monitoring System

This repository contains a comprehensive IoT-based accident monitoring and alert system built with multiple components including device simulations, data processing, visualization, and web services.

## Project Overview

The Accident IoT Monitoring System is designed to detect, monitor, and respond to vehicle accidents through IoT devices. The system collects data from IoT sensors (real or simulated), processes the information, analyzes it for accident detection, and provides visualization and alerts.

## Repository Structure

### Core Components

#### 1. IoT Device Sources (`iot-source/`)

- Arduino and ESP8266 code for IoT devices that collect and transmit accident data
- Files include:
  - `ardunio_warningcar_code.ino`: Arduino code for the warning car system
  - `car_senddata.ino`: Car data transmission code
  - `esp8266_rf.ino`: ESP8266 RF transmission code

#### 2. IoT Monitor Service (`iot-monitor/`)

- .NET-based background service that processes IoT data
- Features:
  - MQTT client for receiving IoT device data
  - InfluxDB integration for time-series data storage
  - Real-time data analysis for accident detection
  - Notification system for alerts
  - 
#### 3. Accident Monitor Application (`accident-monitor/`)

- .NET Clean Architecture application for monitoring and management
- Components:
  - `AccidentMonitor.WebApi`: REST API for accessing accident data
  - `AccidentMonitor.Domain`: Domain models and business rules
  - `AccidentMonitor.Application`: Application services and use cases
  - `AccidentMonitor.Infrastructure`: External systems integrations
- Deployment:
  - Azure-ready with `azure.yaml` configuration
  - Dockerized with `Dockerfile`
  - Infrastructure as Code (IaC) with Bicep (`infra/` folder)

#### 4. Testing Scripts (`scripts-testing/`)

- Python scripts for testing and simulating IoT devices
- Features:
  - Device simulation with realistic data patterns
  - Multi-device testing capabilities
  - Data visualization tools
- Key files:
  - `iot_device_simulator.py`: Single device simulator
  - `multi_device_simulator.py`: Multiple device simulator
  - `analyze_influxdb_data.py`: Data analysis tools
  - `test_mqtt_influxdb.py`: End-to-end testing

### Infrastructure Components (Docker)

#### 1. Database (`accident-monitor-db/`)

- SQL initialization scripts for the application database

#### 2. DNS Server (`bind9-docker/`)

- DNS configuration for local domain resolution
- Used for inter-service communication

#### 3. Monitoring Stack

- `grafana-docker/`: Grafana dashboards for visualization
- `influxdb-docker/`: InfluxDB time-series database for IoT data
- `mosquitto-docker/`: MQTT broker for IoT device communication

#### 4. Routing and Mapping

- `ors-docker/`: OpenRouteService for mapping and routing
- `traefik-docker/`: Traefik reverse proxy for service routing

## Setup and Deployment

### Local Development

1. **Prerequisites**
   - Docker and Docker Compose
   - .NET 8.0 SDK
   - Python 3.7 or newer
   - Arduino IDE (for IoT device programming)

2. **Run the Infrastructure**

   ```bash
   docker-compose up -d
   ```

3. **Run the Accident Monitor Application**

   ```bash
   cd accident-monitor
   dotnet run --project src/AccidentMonitor.WebApi
   ```

4. **Run the IoT Monitor Service**

   ```bash
   cd iot-monitor
   dotnet run
   ```

5. **Simulate IoT Devices**

   ```bash
   cd scripts-testing
   python multi_device_simulator.py --device-count 5
   ```

### Production Deployment

1. **Build and Deploy the Accident Monitor Application**

   ```bash
   cd accident-monitor
   azd up
   ```

2. **Deploy the Docker Infrastructure**

   ```bash
   docker-compose -f docker-compose.prod.yml up -d
   ```

## IoT Device Simulation

The system includes tools for simulating IoT devices sending accident data:

**Note**: The IoT device sources are already included for real world testing, checkout the `./iot-source` for details.

### Features

- Simulate one or multiple IoT devices
- Data sent continuously at configurable intervals (default: 1 second)
- Simulated parameters:
  - Device ID
  - Battery level (random changes with auto-recharge)
  - Signal strength (random fluctuation)
  - Online/offline status (occasional disconnections to simulate real-world conditions)
  - Device location (minor changes to simulate movement)
  - Device address (configurable)

### Usage

1. **Simulate a Single Device**

   ```bash
   python iot_device_simulator.py
   ```

2. **Customize Parameters**

   ```bash
   python iot_device_simulator.py --device-id "my-device-001" --mqtt-host "localhost" --publish-interval 2.0
   ```

3. **Simulate Multiple Devices**

   ```bash
   python multi_device_simulator.py --device-count 5
   ```

## Data Visualization

1. Access Grafana at `https://grafana.duydz.tao` (or locally at `http://localhost:3000`)
2. Default credentials: Username: `johnnaeder`, Password: `Taodeptrai123@`
3. Explore the pre-configured dashboards for:
   - Device status monitoring
   - Accident detection and visualization
   - System performance metrics

## Architecture

The system follows a microservices architecture:

1. **Data Collection Layer**
   - IoT devices/simulators collect sensor data
   - MQTT broker receives and routes device messages

2. **Processing Layer**
   - IoT Monitor processes real-time data
   - InfluxDB stores time-series IoT data
   - Accident Monitor processes and stores business data

3. **Presentation Layer**
   - Grafana provides data visualization
   - Web API provides programmatic access
   - Notification services for alerts

## Technologies Used

- **IoT Devices**: Arduino, ESP8266
- **Backend**: .NET 8, Clean Architecture, ASP.NET Core
- **Data Storage**: InfluxDB (time-series), SQL Serve (relational)
- **Messaging**: MQTT (Mosquitto)
- **Visualization**: Grafana
- **Deployment**: Docker, Azure (with AZD)
- **Testing**: Python (device simulation)
- **Networking**: Traefik, Bind9 DNS
