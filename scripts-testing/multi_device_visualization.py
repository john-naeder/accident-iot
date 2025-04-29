#!/usr/bin/env python3
"""
Multi-device simulator for testing visualization
-------------------------------------------
This script launches multiple IoT device simulators with different configurations
to test dashboard visualization capabilities in Grafana.

Usage:
    python multi_device_visualization.py
"""

import subprocess
import time
import random
import os
import signal
import sys
from datetime import datetime

# Base device configurations - these will be slightly modified for each device
BASE_CONFIGS = [
    {
        "device_id": "device-001",
        "battery_min": 10.0,
        "battery_max": 100.0,
        "signal_min": -100.0,
        "signal_max": -40.0,
        "location_lat": 10.762622,  # HCMC
        "location_lon": 106.660172,
        "address": "123 Nguyen Hue, Quận 1, TP.HCM",
        "publish_interval": 1.0
    },
    {
        "device_id": "device-002",
        "battery_min": 5.0,
        "battery_max": 90.0,
        "signal_min": -110.0,
        "signal_max": -50.0,
        "location_lat": 10.772744,  # A bit north
        "location_lon": 106.657967,
        "address": "456 Le Loi, Quận 1, TP.HCM",
        "publish_interval": 2.0
    },
    {
        "device_id": "device-003",
        "battery_min": 20.0,
        "battery_max": 95.0, 
        "signal_min": -90.0,
        "signal_max": -30.0,
        "location_lat": 10.757691,  # A bit south
        "location_lon": 106.662778,
        "address": "789 Ham Nghi, Quận 1, TP.HCM", 
        "publish_interval": 1.5
    },
    {
        "device_id": "device-004",
        "battery_min": 15.0,
        "battery_max": 85.0,
        "signal_min": -95.0,
        "signal_max": -45.0,
        "location_lat": 10.763825,  # A bit east
        "location_lon": 106.682003,
        "address": "101 Vo Van Kiet, Quận 1, TP.HCM",
        "publish_interval": 2.5
    },
    {
        "device_id": "device-005",
        "battery_min": 10.0,
        "battery_max": 90.0,
        "signal_min": -105.0,
        "signal_max": -55.0,
        "location_lat": 10.754969,  # A bit southwest
        "location_lon": 106.642303,
        "address": "202 Ly Tu Trong, Quận 1, TP.HCM",
        "publish_interval": 3.0
    }
]

# Common settings - these stay the same for all devices
COMMON_SETTINGS = {
    "mqtt_host": "mqtt.duydz.tao",
    "mqtt_port": 1883,
    "mqtt_username": "rsu",
    "mqtt_password": "Taodeptrai123@",
    "mqtt_topic": "devices/{device_id}/data"
}

processes = []

def start_device_simulator(config):
    """Start a device simulator with the given configuration"""
    cmd = [
        "python", 
        "iot_device_simulator.py",
        f"--device-id={config['device_id']}",
        f"--battery-min={config['battery_min']}",
        f"--battery-max={config['battery_max']}",
        f"--signal-min={config['signal_min']}",
        f"--signal-max={config['signal_max']}",
        f"--location-lat={config['location_lat']}",
        f"--location-lon={config['location_lon']}",
        f"--address={config['address']}",
        f"--mqtt-host={COMMON_SETTINGS['mqtt_host']}",
        f"--mqtt-port={COMMON_SETTINGS['mqtt_port']}",
        f"--mqtt-username={COMMON_SETTINGS['mqtt_username']}",
        f"--mqtt-password={COMMON_SETTINGS['mqtt_password']}",
        f"--mqtt-topic={COMMON_SETTINGS['mqtt_topic']}",
        f"--publish-interval={config['publish_interval']}"
    ]
    
    print(f"Starting device {config['device_id']}...")
    process = subprocess.Popen(cmd)
    processes.append(process)
    return process

def signal_handler(sig, frame):
    """Handle Ctrl+C to terminate all processes cleanly"""
    print("\nStopping all device simulators...")
    for p in processes:
        p.terminate()
    
    # Wait for processes to terminate
    for p in processes:
        p.wait()
    
    print("All simulators stopped.")
    sys.exit(0)

if __name__ == "__main__":
    # Register signal handler for Ctrl+C
    signal.signal(signal.SIGINT, signal_handler)
    
    print(f"Starting {len(BASE_CONFIGS)} device simulators...")
    
    # Start each device simulator
    for config in BASE_CONFIGS:
        start_device_simulator(config)
        # Add a small delay to prevent all devices syncing up
        time.sleep(1)
    
    print(f"All {len(processes)} device simulators started.")
    print("Press Ctrl+C to stop all simulators.")
    
    # Keep the script running
    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        signal_handler(None, None)
