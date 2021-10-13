// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PnpHelpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.Devices.Client.Samples
{
    internal enum StatusCode
    {
        Completed = 200,
        InProgress = 202,
        NotFound = 404,
        BadRequest = 400
    }

    public class TemperatureController
    {
        private class DeviceData
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }
        }

        private const string Thermostat1 = "thermostat1";        
        private const string SerialNumber = "SR-123456";

        private static readonly Random s_random = new Random();

        private readonly DeviceClient _deviceClient;
        private readonly ILogger _logger;

        // Dictionary to hold the temperature updates sent over each "Thermostat" component.
        // NOTE: Memory constrained devices should leverage storage capabilities of an external service to store this
        // information and perform computation.
        // See https://docs.microsoft.com/en-us/azure/event-grid/compare-messaging-services for more details.
        private readonly Dictionary<string, Dictionary<DateTimeOffset, double>> _temperatureReadingsDateTimeOffset =
            new Dictionary<string, Dictionary<DateTimeOffset, double>>();

        // A dictionary to hold all desired property change callbacks that this pnp device should be able to handle.
        // The key for this dictionary is the componentName.
        private readonly IDictionary<string, DesiredPropertyUpdateCallback> _desiredPropertyUpdateCallbacks =
            new Dictionary<string, DesiredPropertyUpdateCallback>();

        // Dictionary to hold the current temperature for each "Thermostat" component.
        private readonly Dictionary<string, double> _temperature = new Dictionary<string, double>();

        // Dictionary to hold the max temperature since last reboot, for each "Thermostat" component.
        private readonly Dictionary<string, double> _maxTemp = new Dictionary<string, double>();

        public TemperatureController(DeviceClient deviceClient, ILogger logger)
        {
            _deviceClient = deviceClient ?? throw new ArgumentNullException($"{nameof(deviceClient)} cannot be null.");
            _logger = logger ?? LoggerFactory.Create(builer => builer.AddConsole()).CreateLogger<TemperatureController>();
        }

        public async Task PerformOperationsAsync(CancellationToken cancellationToken)
        {

            _logger.LogDebug("Set handler for 'reboot' command.");
            await _deviceClient.SetMethodHandlerAsync("talktome", HandletalktomeCommandAsync, "TalktoMeRequest", cancellationToken);

            bool temperatureReset = true;
            _maxTemp[Thermostat1] = 0d;

            while (!cancellationToken.IsCancellationRequested)
            {
                await SendTemperatureTelemetryAsync(Thermostat1, cancellationToken);        
                await SendDeviceMemoryAsync(cancellationToken);
              
                await Task.Delay(5 * 1000);
            }
        }

        // The callback to handle "talktome" command. This method will send a temperature update (of 0°C) over telemetry for both associated components.
        private Task<MethodResponse> HandletalktomeCommandAsync(MethodRequest request, object userContext)
        {
            MethodResponse retValue = null;
            try
            {
                _logger.LogDebug($"Command: Received - talktome ");
                _logger.LogDebug("\talktome...");
                
                DeviceData incomingData = JsonConvert.DeserializeObject<DeviceData>(request.DataAsJson);

                incomingData.Name = $"Howdy from Pi to {incomingData.Name} !!!";
                string result = System.Text.Json.JsonSerializer.Serialize(incomingData);
                retValue = new MethodResponse(Encoding.UTF8.GetBytes(result), 200);
            }
            catch (JsonReaderException ex)
            {
                _logger.LogDebug($"Command input is invalid: {ex.Message}.");
            }

            return Task.FromResult(retValue);
        }

        // Send working set of device memory over telemetry.
        private async Task SendDeviceMemoryAsync(CancellationToken cancellationToken)
        {
            const string workingSetName = "WorkingSet_DeviceMemory";

            long workingSet = Process.GetCurrentProcess().PrivateMemorySize64 / 1024;

            var telemetry = new Dictionary<string, object>
            {
                { workingSetName, workingSet },
            };

            using Message msg = PnpConvention.CreateMessage(telemetry);

            await _deviceClient.SendEventAsync(msg, cancellationToken);
            _logger.LogDebug($"Telemetry: Sent - {JsonConvert.SerializeObject(telemetry)} in KB.");
        }

        private async Task SendTemperatureTelemetryAsync(string componentName, CancellationToken cancellationToken)
        {
            const string telemetryName = "temperature";
            double currentTemperature = s_random.Next(500);
            using Message msg = PnpConvention.CreateMessage(telemetryName, currentTemperature, componentName);

            await _deviceClient.SendEventAsync(msg, cancellationToken);
            _logger.LogDebug($"Telemetry: Sent - component=\"{componentName}\", {{ \"{telemetryName}\": {currentTemperature} }} in °C.");

            if (_temperatureReadingsDateTimeOffset.ContainsKey(componentName))
            {
                _temperatureReadingsDateTimeOffset[componentName].TryAdd(DateTimeOffset.UtcNow, currentTemperature);
            }
            else
            {
                _temperatureReadingsDateTimeOffset.TryAdd(
                    componentName,
                    new Dictionary<DateTimeOffset, double>
                    {
                        { DateTimeOffset.UtcNow, currentTemperature },
                    });
            }
        }
    }
}
