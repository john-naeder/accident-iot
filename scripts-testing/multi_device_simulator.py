#!/usr/bin/env python3
"""
Multi-Device IoT Simulator
--------------------------
This script runs multiple IoT device simulators concurrently to simulate a fleet of devices.
Each simulator runs in its own thread and publishes data to the MQTT broker.

Usage:
    python multi_device_simulator.py --device-count 5
"""

import argparse
import threading
import time
import random
import subprocess
import sys
import os
from datetime import datetime

def run_simulator(device_id, mqtt_host, mqtt_port, mqtt_username, mqtt_password, 
                  mqtt_topic, publish_interval):
    """
    Run a single IoT device simulator as a subprocess
    """
    # Generate random variations for device parameters
    battery_level = random.uniform(50, 100)
    signal_strength = random.uniform(-90, -45)
    lat_base = 10.762
    lon_base = 106.660
    
    # Random location within Ho Chi Minh City
    lat = lat_base + random.uniform(0, 0.05)
    lon = lon_base + random.uniform(0, 0.05)
    
    # Generate different addresses for different devices
    district = random.choice(["Quận 1", "Quận 2", "Quận 3", "Quận 4", "Quận 5", "Quận 7", "Thủ Đức"])
    street = random.choice(["Đường A", "Đường B", "Đường C", "Đại lộ Đông Tây", "Nguyễn Huệ", "Lê Lợi", "Võ Văn Kiệt"])
    address = f"{random.randint(1, 200)} {street}, {district}, TP.HCM"
    
    cmd = [
        sys.executable,
        os.path.join(os.path.dirname(os.path.abspath(__file__)), "iot_device_simulator.py"),
        "--device-id", f"{device_id}",
        "--battery-max", f"{100}",
        "--battery-min", f"{5}",
        "--signal-max", f"{-30}",
        "--signal-min", f"{-120}",
        "--location-lat", f"{lat}",
        "--location-lon", f"{lon}",
        "--address", f"{address}",
        "--mqtt-host", f"{mqtt_host}",
        "--mqtt-port", f"{mqtt_port}",
        "--mqtt-username", f"{mqtt_username}",
        "--mqtt-password", f"{mqtt_password}",
        "--mqtt-topic", f"{mqtt_topic}",
        "--publish-interval", f"{publish_interval}"
    ]
    
    # Open a new console window for each simulator (on Windows)
    if os.name == 'nt':
        subprocess.Popen(cmd, creationflags=subprocess.CREATE_NEW_CONSOLE)
    else:
        # For non-Windows platforms, you might want to use a different approach
        # This will run in the background without a separate window
        subprocess.Popen(cmd)
    
    print(f"Started simulator for device {device_id} with address: {address}")


def main():
    # Parse command line arguments
    parser = argparse.ArgumentParser(description='Multi-Device IoT Simulator for MQTT Testing')
    parser.add_argument('--device-count', type=int, default=3,
                      help='Number of device simulators to run (default: 3)')
    parser.add_argument('--device-prefix', default='test-device-',
                      help='Prefix for device IDs (default: test-device-)')
    parser.add_argument('--mqtt-host', default='mqtt.duydz.tao',
                      help='MQTT broker hostname (default: mqtt.duydz.tao)')
    parser.add_argument('--mqtt-port', type=int, default=1883,
                      help='MQTT broker port (default: 1883)')
    parser.add_argument('--mqtt-username', default='johnnaeder',
                      help='MQTT username (default: johnnaeder)')
    parser.add_argument('--mqtt-password', default='Taodeptrai123@',
                      help='MQTT password (default: Taodeptrai123@)')
    parser.add_argument('--mqtt-topic', default='devices/{device_id}/data',
                      help='MQTT topic template (default: devices/{device_id}/data)')
    parser.add_argument('--publish-interval', type=float, default=1.0,
                      help='Publishing interval in seconds (default: 1.0)')
    
    args = parser.parse_args()
    
    device_count = args.device_count
    
    print(f"=== Multi-Device IoT Simulator ===")
    print(f"Starting {device_count} device simulators...")
    print(f"MQTT Broker: {args.mqtt_host}:{args.mqtt_port}")
    print(f"Press Ctrl+C to stop all simulators\n")
    
    # Start device simulators
    for i in range(1, device_count + 1):
        device_id = f"{args.device_prefix}{i:03d}"
        
        # Randomize the publish interval slightly to avoid synchronization
        interval = args.publish_interval + random.uniform(-0.2, 0.2)
        interval = max(0.5, interval)  # Ensure minimum interval of 0.5 seconds
        
        run_simulator(
            device_id=device_id,
            mqtt_host=args.mqtt_host,
            mqtt_port=args.mqtt_port,
            mqtt_username=args.mqtt_username,
            mqtt_password=args.mqtt_password,
            mqtt_topic=args.mqtt_topic,
            publish_interval=interval
        )
        
        # Small delay between starting each simulator
        time.sleep(0.5)
    
    print(f"\nAll {device_count} device simulators started!")
    print(f"Each simulator is running in its own window.")
    print(f"To stop all simulators, close their respective console windows.")


if __name__ == "__main__":
    main()
