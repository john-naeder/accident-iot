#!/usr/bin/env python3
"""
InfluxDB Data Analyzer
-----------------------------
This script connects to InfluxDB and fetches IoT device data for analysis.
It generates a simple report on device status, message frequency, and other metrics.

Usage:
    python analyze_influxdb_data.py
"""

import argparse
import time
from datetime import datetime, timedelta
import pandas as pd
from influxdb_client import InfluxDBClient
from influxdb_client.client.write_api import SYNCHRONOUS
import matplotlib.pyplot as plt
from tabulate import tabulate

# Default InfluxDB settings
DEFAULT_INFLUX_URL = "http://localhost:8086"
DEFAULT_INFLUX_TOKEN = "TaoDuySieuDZ123@"
DEFAULT_INFLUX_ORG = "accident-monitor-org"
DEFAULT_INFLUX_BUCKET = "iot_status"

def parse_arguments():
    parser = argparse.ArgumentParser(description='InfluxDB Data Analyzer for IoT Devices')
    
    # InfluxDB settings
    parser.add_argument('--influx-url', default=DEFAULT_INFLUX_URL, 
                        help=f'InfluxDB URL (default: {DEFAULT_INFLUX_URL})')
    parser.add_argument('--influx-token', default=DEFAULT_INFLUX_TOKEN, 
                        help=f'InfluxDB Token (default: {DEFAULT_INFLUX_TOKEN})')
    parser.add_argument('--influx-org', default=DEFAULT_INFLUX_ORG, 
                        help=f'InfluxDB Organization (default: {DEFAULT_INFLUX_ORG})')
    parser.add_argument('--influx-bucket', default=DEFAULT_INFLUX_BUCKET, 
                        help=f'InfluxDB Bucket (default: {DEFAULT_INFLUX_BUCKET})')
    
    # Analysis settings
    parser.add_argument('--time-range', default='1h', 
                        help='Time range for analysis (e.g., 1h, 6h, 1d, 7d) (default: 1h)')
    parser.add_argument('--device-id', default=None,
                        help='Specific device ID to analyze (default: analyze all devices)')
    parser.add_argument('--output-file', default=None,
                        help='Save results to file (default: display to console)')
    parser.add_argument('--create-charts', action='store_true',
                        help='Generate charts from the data')
    
    return parser.parse_args()

def fetch_measurements(client, bucket, org, time_range, device_id=None):
    """Fetch available measurements from InfluxDB"""
    query_api = client.query_api()
    
    # Query to get all measurements in the bucket
    flux_query = f'''
    import "influxdata/influxdb/schema"
    schema.measurements(bucket: "{bucket}")
    '''
    
    try:
        result = query_api.query(org=org, query=flux_query)
        measurements = []
        
        for table in result:
            for record in table.records:
                measurements.append(record.get_value())
                
        return measurements
    except Exception as e:
        print(f"Error fetching measurements: {e}")
        return []

def fetch_device_list(client, bucket, org, time_range, measurement):
    """Fetch list of all device IDs in the specified measurement"""
    query_api = client.query_api()
    
    flux_query = f'''
    from(bucket: "{bucket}")
        |> range(start: -{time_range})
        |> filter(fn: (r) => r._measurement == "{measurement}")
        |> group(columns: ["deviceId"])
        |> distinct(column: "deviceId")
    '''
    
    try:
        result = query_api.query(org=org, query=flux_query)
        devices = []
        
        for table in result:
            for record in table.records:
                devices.append(record.get_value())
                
        return devices
    except Exception as e:
        print(f"Error fetching device list: {e}")
        return []

def fetch_device_data(client, bucket, org, time_range, measurement, device_id=None):
    """Fetch device data from InfluxDB"""
    query_api = client.query_api()
    
    device_filter = f'and r.deviceId == "{device_id}"' if device_id else ''
    
    # Build query to get all fields for the specified measurement
    flux_query = f'''
    from(bucket: "{bucket}")
        |> range(start: -{time_range})
        |> filter(fn: (r) => r._measurement == "{measurement}" {device_filter})
    '''
    
    try:
        result = query_api.query_data_frame(query=flux_query, org=org)
        return result
    except Exception as e:
        print(f"Error fetching device data: {e}")
        if isinstance(result, list) and len(result) == 0:
            print("No data found. Check your query parameters.")
        return pd.DataFrame()

def analyze_device_status(data):
    """Analyze device status data"""
    if data.empty:
        return {
            "status": "No data available",
            "device_count": 0,
            "measurements": []
        }
    
    # Get unique devices
    devices = data['deviceId'].unique()
    
    # Get measurements
    measurements = data['_measurement'].unique()
    
    # Group by device
    device_data = {}
    for device in devices:
        device_df = data[data['deviceId'] == device]
        
        # Get latest status for each device
        latest = device_df.sort_values('_time').groupby('_field').last()
        
        # Check if any essential fields exist
        if 'batteryLevel' in latest.index or 'signalStrength' in latest.index or 'isOnline' in latest.index:
            # Extract relevant fields
            battery = latest.loc['batteryLevel', '_value'] if 'batteryLevel' in latest.index else None
            signal = latest.loc['signalStrength', '_value'] if 'signalStrength' in latest.index else None
            is_online = latest.loc['isOnline', '_value'] if 'isOnline' in latest.index else None
            
            # Calculate the latest timestamp
            latest_time = device_df['_time'].max()
            time_diff = (datetime.now() - latest_time).total_seconds()
            
            device_data[device] = {
                "battery_level": battery,
                "signal_strength": signal,
                "is_online": is_online,
                "latest_update": latest_time,
                "time_since_update": time_diff,
                "status": "Unknown"
            }
            
            # Determine device status
            if is_online is not None and not is_online:
                device_data[device]["status"] = "Offline"
            elif time_diff > 300:  # No updates in 5 minutes
                device_data[device]["status"] = "Potentially Offline"
            elif battery is not None and battery < 15:
                device_data[device]["status"] = "Low Battery"
            elif signal is not None and signal < -90:
                device_data[device]["status"] = "Weak Signal"
            else:
                device_data[device]["status"] = "OK"
    
    return {
        "status": "Data analyzed",
        "device_count": len(device_data),
        "devices": device_data,
        "measurements": measurements.tolist()
    }

def generate_report(analysis_results, time_range):
    """Generate a text report from analysis results"""
    report = []
    report.append(f"=== IoT Device Status Report ===")
    report.append(f"Time Range: {time_range}")
    report.append(f"Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    report.append(f"Number of Devices: {analysis_results['device_count']}")
    report.append("")
    
    if analysis_results['device_count'] > 0:
        # Prepare table data
        table_data = []
        for device_id, device_info in analysis_results['devices'].items():
            time_diff = device_info['time_since_update']
            if time_diff < 60:
                time_str = f"{int(time_diff)} seconds ago"
            elif time_diff < 3600:
                time_str = f"{int(time_diff / 60)} minutes ago"
            else:
                time_str = f"{int(time_diff / 3600)} hours ago"
                
            table_data.append([
                device_id,
                device_info['status'],
                f"{device_info['battery_level']:.1f}%" if device_info['battery_level'] is not None else "N/A",
                f"{device_info['signal_strength']:.1f} dBm" if device_info['signal_strength'] is not None else "N/A",
                "Yes" if device_info['is_online'] else "No" if device_info['is_online'] is not None else "Unknown",
                time_str
            ])
        
        # Generate table
        headers = ["Device ID", "Status", "Battery", "Signal", "Online", "Last Update"]
        report.append(tabulate(table_data, headers=headers, tablefmt="grid"))
    else:
        report.append("No device data available for the specified time range.")
    
    return "\n".join(report)

def create_charts(data, device_id=None):
    """Create charts from device data"""
    if data.empty:
        print("No data available for charting")
        return
    
    # Filter by device if specified
    if device_id:
        data = data[data['deviceId'] == device_id]
        title_suffix = f" - Device: {device_id}"
    else:
        title_suffix = " - All Devices"
    
    # Create figure
    plt.figure(figsize=(12, 10))
    
    # Battery levels over time
    try:
        battery_data = data[data['_field'] == 'batteryLevel'].copy()
        if not battery_data.empty:
            plt.subplot(2, 1, 1)
            
            # Convert to numeric if needed
            battery_data['_value'] = pd.to_numeric(battery_data['_value'], errors='coerce')
            
            for device in battery_data['deviceId'].unique():
                device_df = battery_data[battery_data['deviceId'] == device]
                plt.plot(device_df['_time'], device_df['_value'], label=device)
            
            plt.title(f"Battery Levels Over Time{title_suffix}")
            plt.xlabel("Time")
            plt.ylabel("Battery Level (%)")
            plt.legend()
            plt.grid(True)
    except Exception as e:
        print(f"Error creating battery chart: {e}")
    
    # Signal strength over time
    try:
        signal_data = data[data['_field'] == 'signalStrength'].copy()
        if not signal_data.empty:
            plt.subplot(2, 1, 2)
            
            # Convert to numeric if needed
            signal_data['_value'] = pd.to_numeric(signal_data['_value'], errors='coerce')
            
            for device in signal_data['deviceId'].unique():
                device_df = signal_data[signal_data['deviceId'] == device]
                plt.plot(device_df['_time'], device_df['_value'], label=device)
            
            plt.title(f"Signal Strength Over Time{title_suffix}")
            plt.xlabel("Time")
            plt.ylabel("Signal Strength (dBm)")
            plt.legend()
            plt.grid(True)
    except Exception as e:
        print(f"Error creating signal strength chart: {e}")
    
    plt.tight_layout()
    
    # Save figure
    filename = f"device_charts_{datetime.now().strftime('%Y%m%d_%H%M%S')}.png"
    if device_id:
        filename = f"{device_id}_{filename}"
    
    plt.savefig(filename)
    print(f"Chart saved as {filename}")
    
    # Show figure
    plt.show()

def main():
    args = parse_arguments()
    
    print(f"Connecting to InfluxDB at {args.influx_url}...")
    
    # Connect to InfluxDB
    client = InfluxDBClient(
        url=args.influx_url,
        token=args.influx_token,
        org=args.influx_org
    )
    
    # Fetch measurements
    print(f"Fetching measurements from bucket '{args.influx_bucket}'...")
    measurements = fetch_measurements(client, args.influx_bucket, args.influx_org, args.time_range)
    
    if not measurements:
        print("No measurements found in the specified bucket.")
        return
    
    print(f"Found measurements: {', '.join(measurements)}")
    
    # Find device_status or related measurement
    status_measurement = None
    for m in measurements:
        if 'device' in m.lower() and ('status' in m.lower() or 'data' in m.lower()):
            status_measurement = m
            break
    
    if not status_measurement:
        # Just use the first measurement
        status_measurement = measurements[0]
    
    print(f"Using measurement: {status_measurement}")
    
    # Fetch device list if no specific device is specified
    if not args.device_id:
        print(f"Fetching list of devices...")
        devices = fetch_device_list(client, args.influx_bucket, args.influx_org, args.time_range, status_measurement)
        if devices:
            print(f"Found {len(devices)} devices: {', '.join(devices)}")
        else:
            print("No devices found.")
            return
    
    # Fetch device data
    print(f"Fetching device data for the last {args.time_range}...")
    data = fetch_device_data(
        client, 
        args.influx_bucket, 
        args.influx_org, 
        args.time_range, 
        status_measurement, 
        args.device_id
    )
    
    if isinstance(data, pd.DataFrame) and data.empty:
        print("No data found. Try increasing the time range or check device ID.")
        return
    
    # Analyze device data
    print("Analyzing device data...")
    analysis_results = analyze_device_status(data)
    
    # Generate report
    report = generate_report(analysis_results, args.time_range)
    
    # Output report
    if args.output_file:
        with open(args.output_file, 'w') as f:
            f.write(report)
        print(f"Report saved to {args.output_file}")
    else:
        print("\n" + report)
    
    # Create charts if requested
    if args.create_charts:
        try:
            import matplotlib.pyplot as plt
            create_charts(data, args.device_id)
        except ImportError:
            print("Matplotlib is required for chart creation. Install with: pip install matplotlib")
    
    # Close client
    client.close()

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\nAnalysis interrupted by user")
    except Exception as e:
        print(f"\nError during analysis: {e}")
