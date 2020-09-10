using System.Threading.Tasks;
using Newtonsoft.Json;
using Azure.Storage.Queues;

namespace CloudRoboticsCommon
{
    public class RbAppMessageToQueue
    {
        private RbMessage message;
        private string storageConnString;
        private string appName;
        private QueueClient queueClient;

        public RbAppMessageToQueue(RbMessage message, string storageAccount, string storageKey, string appName)
        {
            this.message = message;
            this.appName = appName.ToLower();
            storageConnString = string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}",
                                                            storageAccount, storageKey);
            queueClient = new QueueClient(storageConnString, this.appName);
        }

        public async Task SendAsync()
        {
            string msg = JsonConvert.SerializeObject(message);

            // Create the queue if it doesn't already exist
            await queueClient.CreateIfNotExistsAsync();
            await queueClient.SendMessageAsync(msg);
        }
    }
}
