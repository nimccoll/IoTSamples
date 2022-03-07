using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json.Linq;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DPSDeviceClientSample
{
    class Program
    {
        private static SecurityProviderSymmetricKey _securityProvider;
        private static string _deviceId;
        private static string _iotHub;
        private static int _sendInterval = 10;
        private static bool _stopped = false;
        private static bool _isRunning = false;
        private static bool _reprovision = false;

        // TODO: set your DPS info here:
        private static string _dpsGlobalDeviceEndpoint = "TODO.azure-devices-provisioning.net";
        private static string _dpsIdScope = "TODO";

        // TODO: set the keys for the symmetric key enrollment group here:
        private static string _enrollmentGroupPrimaryKey = "TODO";
        private static string _enrollmentGroupSecondaryKey = "TODO";

        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("*** Sample DPS provisioned device is Starting ***");

            _deviceId = Environment.GetEnvironmentVariable("deviceId", EnvironmentVariableTarget.Process);
            _dpsGlobalDeviceEndpoint = Environment.GetEnvironmentVariable("GlobalDeviceEndpoint", EnvironmentVariableTarget.Process);
            _dpsIdScope = Environment.GetEnvironmentVariable("IdScope", EnvironmentVariableTarget.Process);
            _enrollmentGroupPrimaryKey = Environment.GetEnvironmentVariable("enrollmentGroupPrimaryKey", EnvironmentVariableTarget.Process);
            _enrollmentGroupSecondaryKey = Environment.GetEnvironmentVariable("enrollmentGroupSecondaryKey", EnvironmentVariableTarget.Process);

            if (string.IsNullOrEmpty(_deviceId)
                || string.IsNullOrEmpty(_dpsGlobalDeviceEndpoint)
                || string.IsNullOrEmpty(_dpsIdScope)
                || string.IsNullOrEmpty(_enrollmentGroupPrimaryKey)
                || string.IsNullOrEmpty(_enrollmentGroupSecondaryKey))
            {
                Console.WriteLine("*** Sample DPS provisioned device could not find the deviceId, GlobalDeviceEndpoint, IdScope, enrollmentGroupPrimaryKey, or enrollmentGroupSecondaryKey. Please check the environment configuration on the device. ***");
                return -1;
            }
            else
            {
                // Using symmetric keys for authentication
                _securityProvider = new SecurityProviderSymmetricKey(
                  registrationId: _deviceId,
                  primaryKey: ComputeKeyHash(_enrollmentGroupPrimaryKey, _deviceId),
                  secondaryKey: ComputeKeyHash(_enrollmentGroupSecondaryKey, _deviceId));

                // Provision the device on startup
                DeviceRegistrationResult deviceRegistrationResult = await RegisterDevice(_deviceId);
                if (deviceRegistrationResult.Status == ProvisioningRegistrationStatusType.Assigned)
                {
                    _iotHub = deviceRegistrationResult.AssignedHub;

                    Console.WriteLine($"   Assigned to hub '{deviceRegistrationResult.AssignedHub}'");
                    DeviceClient deviceClient = await CreateDeviceClient(deviceRegistrationResult.DeviceId, deviceRegistrationResult.AssignedHub);

                    // Attempt to connect to the IoT Hub and retrieve the device twin
                    Twin twin = null;
                    try
                    {
                        twin = await deviceClient.GetTwinAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"*** Sample DPS provisioned device failed to retrieve its device twin with the following error {ex.Message}");
                        return -1;
                    }

                    if (twin != null)
                    {
                        Console.WriteLine("*** Sample DPS provisioned device is connected to IoT Hub {0} ***", deviceRegistrationResult.AssignedHub);
                    }

                    new Thread(() => SendTelemetry(deviceClient)).Start();

                    Console.WriteLine("*** Sample DPS provisioned device is Running. Press any key to stop. ***");
                    Console.Read();
                    Console.WriteLine("*** Sample DPS provisioned device is Stopping ***");
                    _stopped = true;
                    while (_isRunning)
                    {
                        Thread.Yield();
                    }
                    await deviceClient.CloseAsync();
                    return 0;
                }
                else
                {
                    Console.WriteLine($"*** Sample DPS provisioned device failed to register with the following status {deviceRegistrationResult.Status}. Error code is {deviceRegistrationResult.ErrorCode} and error message is {deviceRegistrationResult.ErrorMessage} ***");
                    return -1;
                }
            }
        }

        private async static Task SendTelemetry(DeviceClient deviceClient)
        {
            _isRunning = true;

            Random rndI2CPressure = new Random(10);
            Random rndI2CTemperature = new Random(20);
            Random rndConductivity1 = new Random(30);
            Random rndConductivity2 = new Random(40);
            Random rndFlow = new Random(50);
            Random rndPressure1 = new Random(60);
            Random rndPressure2 = new Random(70);

            while (!_stopped)
            {
                try
                {
                    JObject deviceReading = new JObject();
                    deviceReading.Add("CollectorType", "VesselAdapter");
                    deviceReading.Add("Time", DateTime.Now.ToString());
                    deviceReading.Add("I2CPressure", rndI2CPressure.Next(100));
                    deviceReading.Add("I2CTemperature", rndI2CTemperature.Next(100));
                    deviceReading.Add("Conductivity1", rndConductivity1.Next(100));
                    deviceReading.Add("Conductivity2", rndConductivity2.Next(100));
                    deviceReading.Add("Flow", rndFlow.Next(100));
                    deviceReading.Add("Pressure1", rndPressure1.Next(100));
                    deviceReading.Add("Pressure2", rndPressure2.Next(100));

                    deviceClient.SendEventAsync(new Message(Encoding.UTF8.GetBytes(deviceReading.ToString())));

                    Console.WriteLine("*** Sample device: Device data transmitted to IoT hub. ***");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("*** Sample device failed with the following exception. {0} ***", ex.Message);
                }
                if (_reprovision)
                {
                    deviceClient = await CreateDeviceClient(_deviceId, _iotHub);
                    _reprovision = false;
                    Console.WriteLine($"   Assigned to hub '{_iotHub}'");
                }
                Thread.Sleep(_sendInterval * 1000);
            }

            _isRunning = false;
        }

        private static async Task<DeviceRegistrationResult> RegisterDevice(string deviceRegistrationId)
        {
            // Amqp transport
            using var transportHandler = new ProvisioningTransportHandlerAmqp(TransportFallbackType.TcpOnly);

            // Set up a provisioning client for the given device
            var provisioningDeviceClient = ProvisioningDeviceClient.Create(
              globalDeviceEndpoint: _dpsGlobalDeviceEndpoint,
              idScope: _dpsIdScope,
              securityProvider: _securityProvider,
              transport: transportHandler);

            // Register the device
            var deviceRegistrationResult = await provisioningDeviceClient.RegisterAsync();

            Console.WriteLine($"   Device registration result: {deviceRegistrationResult.Status}");
            Console.WriteLine();

            return deviceRegistrationResult;
        }

        private static async Task<DeviceClient> CreateDeviceClient(string deviceId, string iotHub)
        {
            IAuthenticationMethod auth = new DeviceAuthenticationWithRegistrySymmetricKey(
                deviceId,
                _securityProvider.GetPrimaryKey());
            DeviceClient deviceClient = DeviceClient.Create(iotHub, auth);
            await deviceClient.SetMethodHandlerAsync(methodName: "GetStatus", methodHandler: GetStatusHandler, userContext: deviceClient);
            await deviceClient.SetMethodHandlerAsync(methodName: "ReprovisionDevice", methodHandler: ReprovisionDeviceHandler, userContext: deviceClient);

            return deviceClient;
        }

        private static string ComputeKeyHash(string key, string payload)
        {
            // Compute symmetric key based on the provided provisioning key and device ID
            using var hmac = new HMACSHA256(Convert.FromBase64String(key));

            return Convert.ToBase64String(
              hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload)));
        }

        private static async Task<MethodResponse> ReprovisionDeviceHandler(MethodRequest method, object userContext)
        {
            var deviceRegistrationResult = await RegisterDevice(_deviceId);
            if (deviceRegistrationResult.Status == ProvisioningRegistrationStatusType.Assigned)
            {
                _reprovision = true;
                _iotHub = deviceRegistrationResult.AssignedHub;
                return new MethodResponse(Encoding.UTF8.GetBytes(deviceRegistrationResult.AssignedHub), 200);
            }
            else
            {
                return new MethodResponse(Encoding.UTF8.GetBytes($"Device provisioning failed with the following error code {deviceRegistrationResult.ErrorCode} and error message {deviceRegistrationResult.ErrorMessage}"), 500);
            }
        }

        private static async Task<MethodResponse> GetStatusHandler(MethodRequest method, object userContext)
        {
            string state = "stopped";
            if (_isRunning)
            {
                state = "running";
            }
            JObject deviceStatus = new JObject();
            deviceStatus.Add("Status", state);

            return new MethodResponse(Encoding.UTF8.GetBytes(deviceStatus.ToString()), 200);
        }

    }
}
