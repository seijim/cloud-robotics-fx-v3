using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Azure.Storage.Queues;

namespace CloudRoboticsCommon
{
    public class RbC2dMessageToQueue
    {
        private JArray ja_messages;
        private string sqlConnString;
        private string storageConnString;

        public RbC2dMessageToQueue(JArray ja_messages, string storageConnString, string sqlConnString)
        {
            this.ja_messages = ja_messages;
            this.sqlConnString = sqlConnString;
            this.storageConnString = storageConnString;
        }

        public async Task SendToDeviceAsync()
        {
            foreach (JObject jo in ja_messages)
            {
                JObject jo_header = (JObject)jo[RbFormatType.RbHeader];
                string jo_TargetType = (string)jo_header["TargetType"];
                string jo_TargetDeviceGroupId = (string)jo_header["TargetDeviceGroupId"];
                string jo_TargetDeviceId = jo_header["TargetDeviceId"].ToString().ToLower();
                string msg = JsonConvert.SerializeObject(jo);
                QueueClient queueClient = new QueueClient(storageConnString, jo_TargetDeviceId);

                if (jo_TargetType == RbTargetType.Device)
                {
                    // Create the queue if it doesn't already exist
                    await queueClient.CreateIfNotExistsAsync();
                    await queueClient.SendMessageAsync(msg);
                }
                else
                {
                    DeviceGroup dg = new DeviceGroup(jo_TargetDeviceGroupId, sqlConnString);
                    List<string> deviceList = dg.GetDeviceGroupList();
                    foreach (string deviceId in deviceList)
                    {
                        // Create the queue if it doesn't already exist
                        await queueClient.CreateIfNotExistsAsync();
                        await queueClient.SendMessageAsync(msg);
                    }
                }
            }
        }

        public async Task SendToEachDeviceAsync(List<string> deviceList, int position)
        {
            foreach (JObject jo in ja_messages)
            {
                JObject jo_header = (JObject)jo[RbFormatType.RbHeader];
                string jo_TargetType = (string)jo_header["TargetType"];
                string jo_TargetDeviceGroupId = (string)jo_header["TargetDeviceGroupId"];
                string jo_TargetDeviceId = (string)jo_header["TargetDeviceId"];
                string msg = JsonConvert.SerializeObject(jo);

                QueueClient queueClient = new QueueClient(storageConnString, jo_TargetDeviceId);
                // Create the queue if it doesn't already exist
                await queueClient.CreateIfNotExistsAsync();
                await queueClient.SendMessageAsync(msg);
            }
        }

    }
}
