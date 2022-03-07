using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json.Linq;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceClientSample
{
    class Program
    {
        private static int _sendInterval = 10;
        private static bool _stopped = false;
        private static bool _isRunning = false;

        static async Task Main(string[] args)
        {
            var deviceClient = DeviceClient.CreateFromConnectionString(Environment.GetEnvironmentVariable("ConnectionString", EnvironmentVariableTarget.Process));
            var twin = await deviceClient.GetTwinAsync();
            _sendInterval = twin.Properties.Desired["sendInterval"];
            await deviceClient.SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateHandler, deviceClient);
            await deviceClient.SetMethodHandlerAsync(methodName: "GetStatus", methodHandler: GetStatusHandler, userContext: deviceClient);
            Console.WriteLine("*** Sample device is Starting ***");
            new Thread(() => SendTelemetry(deviceClient)).Start();
            Console.WriteLine("*** Sample device is Running. Press any key to stop. ***");
            Console.Read();
            Console.WriteLine("*** Sample device is Stoppping ***");
            _stopped = true;
            while (_isRunning)
            {
                Thread.Yield();
            }
            await deviceClient.CloseAsync();
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

        private async static Task DesiredPropertyUpdateHandler(Microsoft.Azure.Devices.Shared.TwinCollection desiredProperties, object userContext)
        {
            int sendInterval = desiredProperties["sendInterval"];
            if (sendInterval != _sendInterval)
            {
                _sendInterval = sendInterval;
                var deviceClient = (DeviceClient)userContext;
                var twin = await deviceClient.GetTwinAsync();
                twin.Properties.Reported["sendInterval"] = _sendInterval;
                await deviceClient.UpdateReportedPropertiesAsync(twin.Properties.Reported);
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
                Thread.Sleep(_sendInterval * 1000);
            }

            _isRunning = false;
        }
    }
}
