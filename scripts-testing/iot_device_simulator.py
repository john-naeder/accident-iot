#!/usr/bin/env python3
"""
IoT Device Simulator for MQTT Testing
------------------------------------
This script simulates an IoT device by sending random device data to an MQTT broker.
Data includes:
- Battery level (random variations)
- Signal strength (random variations)
- Online status (occasionally goes offline)
- Location (slight random variations to simulate movement)
- Device address (static)

Usage:
    python iot_device_simulator.py
"""

import json
import time
import random
import argparse
from datetime import datetime
import paho.mqtt.client as mqtt

# Default device parameters
DEFAULT_DEVICE_ID = "test-device-001"
DEFAULT_BATTERY_MAX = 100.0
DEFAULT_BATTERY_MIN = 5.0
DEFAULT_SIGNAL_MAX = -30.0
DEFAULT_SIGNAL_MIN = -120.0
DEFAULT_LOCATION_LAT = 10.801312
DEFAULT_LOCATION_LON = 106.657618
DEFAULT_ADDRESS = "123 Đường A, Quận 1, TP.HCM"

# Default MQTT settings
DEFAULT_MQTT_HOST = "localhost"
DEFAULT_MQTT_PORT = 1884
DEFAULT_MQTT_USERNAME = "johnnaeder"
DEFAULT_MQTT_PASSWORD = "Taodeptrai123@"
DEFAULT_MQTT_TOPIC = "devices/{device_id}/data"
DEFAULT_MQTT_QOS = 1

# Connection settings
DEFAULT_RECONNECT_DELAY = 5  # seconds
DEFAULT_PUBLISH_INTERVAL = 1.0  # seconds

class IoTDeviceSimulator:
    def __init__(self, args):
        # Device parameters
        self.device_id = args.device_id
        self.battery_level = random.uniform(args.battery_min, args.battery_max)
        self.signal_strength = random.uniform(args.signal_min, args.signal_max)
        self.is_online = True
        self.location = {
            "latitude": args.location_lat,
            "longitude": args.location_lon
        }
        self.address = args.address
        
        # Battery drain simulation
        self.battery_drain_rate = random.uniform(0.01, 0.05)  # % per second
        self.battery_charging = False
        
        # MQTT settings
        self.client = mqtt.Client(client_id=f"simulator-{self.device_id}")
        if args.mqtt_username:
            self.client.username_pw_set(args.mqtt_username, args.mqtt_password)
        
        self.mqtt_host = args.mqtt_host
        self.mqtt_port = args.mqtt_port
        self.mqtt_topic = args.mqtt_topic.format(device_id=self.device_id)
        self.mqtt_qos = args.mqtt_qos
        
        # Connection settings
        self.reconnect_delay = args.reconnect_delay
        self.publish_interval = args.publish_interval
        
        # Set up callbacks
        self.client.on_connect = self.on_connect
        self.client.on_disconnect = self.on_disconnect
        
        # Initialize device ID and start timestamp
        self.start_time = datetime.now()
        self.messages_sent = 0

    def on_connect(self, client, userdata, flags, rc):
        if rc == 0:
            print(f"Connected to MQTT broker at {self.mqtt_host}:{self.mqtt_port}")
        else:
            print(f"Failed to connect to MQTT broker with error code: {rc}")

    def on_disconnect(self, client, userdata, rc):
        if rc != 0:
            print(f"Unexpected disconnection from MQTT broker. Reconnecting...")
            while True:
                try:
                    print(f"Attempting to reconnect in {self.reconnect_delay} seconds...")
                    time.sleep(self.reconnect_delay)
                    self.client.reconnect()
                    break
                except Exception as e:
                    print(f"Reconnection failed: {e}")

    def connect(self):
        try:
            print(f"Connecting to MQTT broker at {self.mqtt_host}:{self.mqtt_port}...")
            self.client.connect(self.mqtt_host, self.mqtt_port)
            self.client.loop_start()
        except Exception as e:
            print(f"Connection error: {e}")
            return False
        return True

    def update_device_state(self):
        # Simulate battery level changes
        if self.battery_charging:
            self.battery_level += random.uniform(0.05, 0.1)
            if self.battery_level >= DEFAULT_BATTERY_MAX:
                self.battery_level = DEFAULT_BATTERY_MAX
                self.battery_charging = False
        else:
            self.battery_level -= self.battery_drain_rate
            if self.battery_level <= 15:
                # Start charging when battery is low
                self.battery_charging = True
        
        # Ensure battery level stays within limits
        self.battery_level = max(DEFAULT_BATTERY_MIN, min(DEFAULT_BATTERY_MAX, self.battery_level))
        
        # Randomly vary signal strength
        self.signal_strength = self.signal_strength + random.uniform(-2.0, 2.0)
        self.signal_strength = max(DEFAULT_SIGNAL_MIN, min(DEFAULT_SIGNAL_MAX, self.signal_strength))
        
        # Occasionally go offline (1% chance per update)
        if random.random() < 0.01:
            self.is_online = not self.is_online
        
        # Add small random movement to location
        self.location["latitude"] += random.uniform(-0.0001, 0.0001)
        self.location["longitude"] += random.uniform(-0.0001, 0.0001)

    def get_device_data(self):
        return {
            "deviceId": self.device_id,
            "timestamp": datetime.now().isoformat(),
            "batteryLevel": round(self.battery_level, 2),
            "signalStrength": round(self.signal_strength, 2),
            "isOnline": self.is_online,
            "location": {
                "latitude": round(self.location["latitude"], 6),
                "longitude": round(self.location["longitude"], 6)
            },
            "address": self.address,
            "batteryCharging": self.battery_charging
        }

    def publish_data(self):
        self.update_device_state()
        data = self.get_device_data()
        
        # Convert to JSON
        payload = json.dumps(data)
        
        # Publish to MQTT
        try:
            result = self.client.publish(self.mqtt_topic, payload, qos=self.mqtt_qos)
            if result.rc == mqtt.MQTT_ERR_SUCCESS:
                self.messages_sent += 1
                print(f"[{data['timestamp']}] Message #{self.messages_sent} published:")
                print(f"  Topic: {self.mqtt_topic}")
                print(f"  Battery: {data['batteryLevel']}% | Signal: {data['signalStrength']} dBm | Online: {data['isOnline']}")
                if self.messages_sent % 10 == 0:
                    # Print statistics every 10 messages
                    runtime = (datetime.now() - self.start_time).total_seconds()
                    print(f"\n=== STATISTICS ===")
                    print(f"Runtime: {int(runtime // 60)}m {int(runtime % 60)}s")
                    print(f"Messages sent: {self.messages_sent}")
                    print(f"Average rate: {self.messages_sent / runtime:.2f} msg/sec")
                    print(f"=================\n")
            else:
                print(f"Failed to publish message: {result}")
        except Exception as e:
            print(f"Error publishing message: {e}")

    def run(self):
        if not self.connect():
            return
        
        print(f"Starting IoT device simulator for device {self.device_id}")
        print(f"Publishing to topic: {self.mqtt_topic}")
        print(f"Press Ctrl+C to stop the simulation\n")
        
        try:
            while True:
                self.publish_data()
                time.sleep(self.publish_interval)
        except KeyboardInterrupt:
            print("\nSimulation stopped by user")
        finally:
            self.client.loop_stop()
            self.client.disconnect()
            print("Disconnected from MQTT broker")
            
            # Print final statistics
            runtime = (datetime.now() - self.start_time).total_seconds()
            print(f"\n=== FINAL STATISTICS ===")
            print(f"Total runtime: {int(runtime // 60)}m {int(runtime % 60)}s")
            print(f"Total messages sent: {self.messages_sent}")
            print(f"Average publishing rate: {self.messages_sent / runtime:.2f} msg/sec")
            print(f"=======================")


def parse_arguments():
    parser = argparse.ArgumentParser(description='IoT Device Simulator for MQTT Testing')
    
    # Device parameters
    parser.add_argument('--device-id', default=DEFAULT_DEVICE_ID, 
                        help=f'Device ID (default: {DEFAULT_DEVICE_ID})')
    parser.add_argument('--battery-max', type=float, default=DEFAULT_BATTERY_MAX, 
                        help=f'Maximum battery level (default: {DEFAULT_BATTERY_MAX})')
    parser.add_argument('--battery-min', type=float, default=DEFAULT_BATTERY_MIN, 
                        help=f'Minimum battery level (default: {DEFAULT_BATTERY_MIN})')
    parser.add_argument('--signal-max', type=float, default=DEFAULT_SIGNAL_MAX, 
                        help=f'Maximum signal strength in dBm (default: {DEFAULT_SIGNAL_MAX})')
    parser.add_argument('--signal-min', type=float, default=DEFAULT_SIGNAL_MIN, 
                        help=f'Minimum signal strength in dBm (default: {DEFAULT_SIGNAL_MIN})')
    parser.add_argument('--location-lat', type=float, default=DEFAULT_LOCATION_LAT, 
                        help=f'Initial latitude (default: {DEFAULT_LOCATION_LAT})')
    parser.add_argument('--location-lon', type=float, default=DEFAULT_LOCATION_LON, 
                        help=f'Initial longitude (default: {DEFAULT_LOCATION_LON})')
    parser.add_argument('--address', default=DEFAULT_ADDRESS, 
                        help=f'Device address (default: {DEFAULT_ADDRESS})')
    
    # MQTT settings
    parser.add_argument('--mqtt-host', default=DEFAULT_MQTT_HOST, 
                        help=f'MQTT broker hostname (default: {DEFAULT_MQTT_HOST})')
    parser.add_argument('--mqtt-port', type=int, default=DEFAULT_MQTT_PORT, 
                        help=f'MQTT broker port (default: {DEFAULT_MQTT_PORT})')
    parser.add_argument('--mqtt-username', default=DEFAULT_MQTT_USERNAME, 
                        help=f'MQTT username (default: {DEFAULT_MQTT_USERNAME})')
    parser.add_argument('--mqtt-password', default=DEFAULT_MQTT_PASSWORD, 
                        help=f'MQTT password (default: {DEFAULT_MQTT_PASSWORD})')
    parser.add_argument('--mqtt-topic', default=DEFAULT_MQTT_TOPIC, 
                        help=f'MQTT topic template (default: {DEFAULT_MQTT_TOPIC})')
    parser.add_argument('--mqtt-qos', type=int, default=DEFAULT_MQTT_QOS, 
                        help=f'MQTT QoS level (default: {DEFAULT_MQTT_QOS})')
    
    # Connection settings
    parser.add_argument('--reconnect-delay', type=int, default=DEFAULT_RECONNECT_DELAY, 
                        help=f'Reconnection delay in seconds (default: {DEFAULT_RECONNECT_DELAY})')
    parser.add_argument('--publish-interval', type=float, default=DEFAULT_PUBLISH_INTERVAL, 
                        help=f'Publishing interval in seconds (default: {DEFAULT_PUBLISH_INTERVAL})')
    
    return parser.parse_args()


if __name__ == "__main__":
    args = parse_arguments()
    simulator = IoTDeviceSimulator(args)
    simulator.run()
