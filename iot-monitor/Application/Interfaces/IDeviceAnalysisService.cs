using iot_monitor.Application.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace iot_monitor.Application.Interfaces
{
    public interface IDeviceAnalysisService
    {
        /// <summary>
        /// Analyzes device data and returns analysis results with status and issues
        /// </summary>
        /// <param name="deviceId">Unique identifier of the device</param>
        /// <param name="deviceData">Raw device data to analyze</param>
        /// <returns>Analysis results including status, metrics, and any issues found</returns>
        Task<DeviceAnalysisResult> AnalyzeDeviceDataAsync(string deviceId, Dictionary<string, object> deviceData);

        /// <summary>
        /// Saves analysis results to history and sends notifications if needed
        /// </summary>
        /// <param name="result">Device analysis result to save</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task SaveAnalysisHistoryAsync(DeviceAnalysisResult result);
    }
}
