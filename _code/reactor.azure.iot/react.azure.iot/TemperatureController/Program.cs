// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using CommandLine;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.PlugAndPlay;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.Devices.Client.Samples
{
    public class Program
    {
        // DTDL interface used: https://github.com/Azure/iot-plugandplay-models/blob/main/dtmi/com/example/temperaturecontroller-2.json
        // The TemperatureController model contains 2 Thermostat components that implement different versions of Thermostat models.
        // Both Thermostat models are identical in definition but this is done to allow IoT Central to handle
        // TemperatureController model correctly.
        private const string ModelId = "dtmi:com:example:TemperatureController;2";

        private static ILogger s_logger;

        public static async Task Main(string[] args)
        {
            s_logger = InitializeConsoleDebugLogger();
            

            s_logger.LogInformation("Press Control+C to quit the sample.");
            using var cts = new CancellationTokenSource(Timeout.InfiniteTimeSpan);
            

            s_logger.LogDebug($"Set up the device client.");
            using DeviceClient deviceClient = await SetupDeviceClientAsync(cts.Token);
            var sample = new TemperatureControllerSample(deviceClient, s_logger);
            await sample.PerformOperationsAsync(cts.Token);
            
            await deviceClient.CloseAsync();
        }

        private static ILogger InitializeConsoleDebugLogger()
        {
            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                .AddFilter(level => level >= LogLevel.Debug)
                .AddConsole(options =>
                {
                    options.TimestampFormat = "[MM/dd/yyyy HH:mm:ss]";
                });
            });

            return loggerFactory.CreateLogger<TemperatureControllerSample>();
        }

        private static async Task<DeviceClient> SetupDeviceClientAsync(CancellationToken cancellationToken)
        {
            DeviceClient deviceClient;
            s_logger.LogDebug($"Initializing via IoT Hub connection string");
            deviceClient = InitializeDeviceClient("HostName=reactoriothub.azure-devices.net;DeviceId=temperatureSensor;SharedAccessKey=bEfQ+E/AtW2QFIiUI3S3rdfl47udfx35ZKbiW6Ob0WY=");
            return deviceClient;
        }

        // Initialize the device client instance using connection string based authentication, over Mqtt protocol (TCP, with fallback over Websocket) and
        // setting the ModelId into ClientOptions.This method also sets a connection status change callback, that will get triggered any time the device's
        // connection status changes.
        private static DeviceClient InitializeDeviceClient(string deviceConnectionString)
        {
            var options = new ClientOptions
            {
                ModelId = ModelId,
            };

            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, TransportType.Mqtt, options);
            deviceClient.SetConnectionStatusChangesHandler((status, reason) =>
            {
                s_logger.LogDebug($"Connection status change registered - status={status}, reason={reason}.");
            });

            return deviceClient;
        }
    }
}
