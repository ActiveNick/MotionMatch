using Microsoft.Azure.Devices.Client;
using Microsoft.Band.Sensors;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MotionMatchWin10
{
    public static class AzureHelper
    {
        // TO DO: Replace this connection string with your own from the IoT Device Explorer.
        // 

        public static void SendToAzure(Motion currentMotion)
        {
            var jsonMessage = JsonConvert.SerializeObject(currentMotion);

            AzureHelper.SendMessage(jsonMessage);
        }

        public static void SendToAzure(Sample currentSample)
        {
            var jsonMessage = JsonConvert.SerializeObject(currentSample);

            AzureHelper.SendMessage(jsonMessage);
        }

        public static async void SendMessage(string message)
        {
            // Send message to an IoT Hub using IoT Hub SDK
            try
            {
                DeviceClient deviceClient = DeviceClient.CreateFromConnectionString("HostName=MotionMatchHub.azure-devices.net;DeviceId=NickLumia;SharedAccessKey=0ZdBDEW+qcRUARNVngW6DeHM9RKnkPcPLYOnWcISOTQ=");

                var content = new Message(Encoding.UTF8.GetBytes(message));
                await deviceClient.SendEventAsync(content);

                Debug.WriteLine("Message Sent: {0}", message, null);
            }
            catch (Exception e)
            {
                Debug.WriteLine("Exception when sending message:" + e.Message);
            }
        }

        public static async void SendBatchToAzure(List<Sample> samples)
        {
            // Send message to an IoT Hub using IoT Hub SDK
            try
            {
                DeviceClient deviceClient = DeviceClient.CreateFromConnectionString("HostName=MotionMatchHub.azure-devices.net;DeviceId=NickLumia;SharedAccessKey=0ZdBDEW+qcRUARNVngW6DeHM9RKnkPcPLYOnWcISOTQ=");

                List<Message> messages = new List<Message>();

                foreach(Sample s in samples)
                {
                    string jsonMessage = JsonConvert.SerializeObject(s);
                    Message content = new Message(Encoding.UTF8.GetBytes(jsonMessage));
                    messages.Add(content);
                }

                await deviceClient.SendEventBatchAsync(messages);

                Debug.WriteLine("Batch messages Sent: {0} messages", messages.Count);
            }
            catch (Exception e)
            {
                Debug.WriteLine("Exception when sending message:" + e.Message);
            }
        }
    }
}
