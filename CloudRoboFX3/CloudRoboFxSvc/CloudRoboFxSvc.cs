using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
// adding 
using System.Text;
using System.Data.SqlClient;
using Microsoft.Azure.EventHubs;
using Microsoft.ServiceFabric.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using CloudRoboticsCommon;

//*******************************************************************************************************************************************
// V3 の変更点
// ・.NET Framework から .NET Core 3.1 ランタイムへの変更
// ・パッケージやライブラリの最新化
// ・App Routing 機能：DLL の Blob Storage 配置と DLL Load 機能のは廃止
// ・App Routing 機能：CALL では HTTP URI / HTTP トリガー Function App を呼び出し、返り値をデバイスに送信
// ・App Routing 機能：CALL_ASYNC を追加し、App 名の Queue Storage にメッセージを出力 (キュートリガー Function App, Logic App で処理可能)
//*******************************************************************************************************************************************
namespace CloudRoboFxSvc
{
    /// <summary>
    /// Service Fabric rutime generages an instance of this class in each service replica.
    /// </summary>
    internal sealed class CloudRoboFxSvc : StatefulService
    {
        /// <summary>
        /// Names of the dictionaries that hold the current offset value and partition epoch.
        /// </summary>
        private const string OffsetDictionaryName = "OffsetDictionary";
        private const string EpochDictionaryName = "EpochDictionary";

        /// <summary>
        /// Lock Object for multi-thread
        /// </summary>
        private readonly static string thisLock = "{ThisObjectLock1}";

        /// <summary>
        /// Initial Setting (Environment variables)
        /// </summary>
        private string iotHubConnectionString = string.Empty;
        private string compatibleEventHubConnString = string.Empty;
        private string iotHubConsumerGroup = string.Empty;
        private string storageQueueSendEnabled = string.Empty;
        private string storageQueueConnString = string.Empty;
        private string sqlConnectionString = string.Empty;
        private string EncPassPhrase = string.Empty;
        private string rbEncPassPhrase = string.Empty;
        private int rbCacheExpiredTimeSec = 30;
        private string rbTraceStorageConnString = string.Empty;
        private string rbTraceStorageTableName = string.Empty;
        private string rbTraceLevel = string.Empty;

        /// <summary>
        /// Error & Info message trace
        /// </summary>
        private RbTraceLog rbTraceLog = null;

        /// <summary>
        /// IoT Hub message processing retry
        /// </summary>
        private readonly int maxEventProcessingRetryCount = 3;

        /// <summary>
        /// SQL Database Table Cache
        /// </summary>
        private Dictionary<string, object> rbCustomerRescCacheDic = new Dictionary<string, object>();
        private Dictionary<string, object> rbAppMasterCacheDic = new Dictionary<string, object>();
        private Dictionary<string, object> rbAppRouterCacheDic = new Dictionary<string, object>();
        private Dictionary<string, object> rbAppDllCacheInfoDic = new Dictionary<string, object>();

        /// <summary>
        /// Reconfiguration may occur when the below error number returns from SQL Database.
        /// It is necessary to retry when we face the below error number.
        /// https://social.technet.microsoft.com/wiki/contents/articles/4235.retry-logic-for-transient-failures-in-windows-azure-sql-database.aspx
        /// </summary>
        // 20	    The instance of SQL Server does not support encryption.
        // 64	    An error occurred during login. 
        // 233	    Connection initialization error. 
        // 10053	A transport-level error occurred when receiving results from the server. 
        // 10054	A transport-level error occurred when sending the request to the server. 
        // 10060	Network or instance-specific error. 
        // 40143	Connection could not be initialized. 
        // 40197	The service encountered an error processing your request.
        // 40501	The server is busy. 
        // 40613	The database is currently unavailable.
        private readonly List<int> sqlErrorListForRetry
            = new List<int> { 20, 64, 233, 10053, 10054, 10060, 40143, 40197, 40501, 40613 };
        private readonly int maxLoopCounter = 10;
        private readonly int sleepInterval = 1000; //millisecond

        /// <summary>
        /// StorageQueueSendEnabled property type in RbHeader
        /// </summary>
        private readonly string typeStorageQueueSendEnabled = "StorageQueueSendEnabled";
        private string prevStorageQueueSendEnabled = string.Empty;


        public CloudRoboFxSvc(StatefulServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// このサービス レプリカがクライアント要求やユーザー要求を処理するためのリスナー (HTTP、Service Remoting、WCF など) を作成する、省略可能なオーバーライド。
        /// </summary>
        /// <remarks>
        ///サービスの通信の詳細については、https://aka.ms/servicefabricservicecommunication を参照してください
        /// </remarks>
        /// <returns>リスナーのコレクション。</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new ServiceReplicaListener[0];
        }

        /// <summary>
        /// This is a main entry point of Service replica.
        /// This method can be executable when the replica is primary.
        /// </summary>
        /// <param name="cancellationToken">When Service Fabric needs to shutdown this Service replica, it is cancelled.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // Environment variables 
            InitializeSetting();

            // Initialize RbTraceLog
            rbTraceLog = new RbTraceLog(rbTraceStorageConnString, rbTraceStorageTableName,
                                            "CloudRoboticsFx3", this.Context.TraceId.ToString());

            rbTraceLog.CreateLogTableIfNotExists();

            // These Reliable Dictionaries are used to keep track of our position in IoT Hub.
            // If this service fails over, this will allow it to pick up where it left off in the event stream.
            IReliableDictionary<string, string> offsetDictionary =
                await this.StateManager.GetOrAddAsync<IReliableDictionary<string, string>>(OffsetDictionaryName);

            IReliableDictionary<string, long> epochDictionary =
                await this.StateManager.GetOrAddAsync<IReliableDictionary<string, long>>(EpochDictionaryName);

            // Each partition of this service corresponds to a partition in IoT Hub.
            // IoT Hub partitions are numbered 0..n-1, up to n = 32.
            // This service needs to use an identical partitioning scheme. 
            // The low key of every partition corresponds to an IoT Hub partition.
            Int64RangePartitionInformation partitionInfo = (Int64RangePartitionInformation)this.Partition.PartitionInfo;
            long servicePartitionKey = partitionInfo.LowKey;

            EventHubClient client = null;
            PartitionReceiver partitionReceiver = null;
            int retryCount = 0;

            try
            {
                // ETW Trace
                ServiceEventSource.Current.ServiceMessage(this.Context, $"CloudRoboFxSvc has started. Partition({servicePartitionKey})");
                // RbTrace (Table Storage)
                rbTraceLog.WriteLog($"CloudRoboFxSvc has started. Partition({servicePartitionKey})");

                // Get a partitionReceiver with EventHubClient.
                // The partitionReceiver is used to get events from IoT Hub.
                client = EventHubClient.CreateFromConnectionString(compatibleEventHubConnString);
                partitionReceiver = await this.ConnectToIoTHubAsync(client, iotHubConsumerGroup,
                                                    servicePartitionKey, epochDictionary, offsetDictionary);

                string previousOffset = string.Empty;

                // Loop of Processing EventData
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        int maxReceivedBatchSize = 50;
                        int maxWaitTimeMilliSec = 5000;
                        IEnumerable<EventData> receivedEvents = await partitionReceiver.ReceiveAsync(maxReceivedBatchSize,
                                                                        TimeSpan.FromMilliseconds(maxWaitTimeMilliSec));
                        if (receivedEvents == null)
                            continue;

                        // Process event data
                        foreach (var eventData in receivedEvents)
                        {
                            await ProcessMessage(eventData, servicePartitionKey, rbTraceLog);

                            // Save the current Iot Hub data stream offset.
                            // This will allow the service to pick up from its current location if it fails over.
                            // Duplicate device messages may still be sent to the the tenant service 
                            // if this service fails over after the message is sent but before the offset is saved.
                            ServiceEventSource.Current.ServiceMessage(this.Context, "Saving offset {0}",
                                    eventData.SystemProperties.Offset);

                            using (ITransaction tx = this.StateManager.CreateTransaction())
                            {
                                await offsetDictionary.SetAsync(tx, "offset", eventData.SystemProperties.Offset);
                                await tx.CommitAsync();
                            }
                        }
                    }
                    catch (TimeoutException te)
                    {
                        // transient error. Retry.
                        ServiceEventSource.Current.ServiceMessage(this.Context, $"TimeoutException in RunAsync: {te.ToString()}");
                        rbTraceLog.WriteError("E004", $"** TimeoutException in RunAsync ** {te.ToString()}");
                    }
                    catch (FabricTransientException fte)
                    {
                        // transient error. Retry.
                        ServiceEventSource.Current.ServiceMessage(this.Context, $"FabricTransientException in RunAsync: {fte.ToString()}");
                        rbTraceLog.WriteError("E005", $"** FabricTransientException in RunAsync ** {fte.ToString()}");
                    }
                    catch (FabricNotPrimaryException)
                    {
                        // not primary any more, time to quit.

                        rbTraceLog.WriteError("E006", $"** FabricNotPrimaryException in RunAsync **");
                        return;
                    }
                    catch(Exception ex)
                    {
                        if (ex is ServerBusyException || ex is TimeoutException)
                        {
                            ++retryCount;
                            if (retryCount > maxEventProcessingRetryCount)
                                throw;
                            rbTraceLog.WriteLog("** Retrying to open IoT Hub connection... **");
                            if (partitionReceiver != null)
                            {
                                try { await partitionReceiver.CloseAsync(); } catch { /* None */ }
                                partitionReceiver = null;
                            }
                            await Task.Delay(new TimeSpan(0, 0, 5));
                            partitionReceiver = await this.ConnectToIoTHubAsync(client, iotHubConsumerGroup,
                                                    servicePartitionKey, epochDictionary, offsetDictionary);
                            previousOffset = string.Empty;

                            continue;
                        }
                        else
                        {
                            ServiceEventSource.Current.ServiceMessage(this.Context, ex.ToString());
                            rbTraceLog.WriteError("E007", $"** Critical error occured ** {ex.ToString()}");

                            throw;
                        }
                    }
                }
            }
            finally
            {
                if (partitionReceiver != null)
                {
                    await partitionReceiver.CloseAsync();
                }
            }
        }

        private async Task ProcessMessage(EventData eventData, long partitionKey, RbTraceLog rbTraceLog)
        {
            // Receive a message and system properties
            string iothub_deviceId = (string)eventData.SystemProperties["iothub-connection-device-id"];
            DateTime iothub_enqueuedTimeUtc = (DateTime)eventData.SystemProperties.EnqueuedTimeUtc;
            string text_message = Encoding.UTF8.GetString(eventData.Body.Array);

            // RbTrace (Table Storage)
            if (rbTraceLevel == RbTraceType.Detail)
                rbTraceLog.WriteLog($"Received a message. Partition({partitionKey}), DeviceId({iothub_deviceId}), Message:{text_message}");

            // Loop of retry for reconfiguration on SQL Database
            int loopCounter = 0;
            while (true)
            {
                JObject jo_message = null;

                // Routing switch
                bool devRouting = false;
                bool appRouting = false;

                try
                {
                    // Check RbHeader
                    if (text_message.IndexOf(RbFormatType.RbHeader) < 0)
                    {
                        rbTraceLog.WriteLog(RbExceptionMessage.RbHeaderNotFound
                            + $" Partition({partitionKey}), DeviceId({iothub_deviceId}), Message:{text_message}");
                        return;
                    }

                    // Check RbHeader simplly
                    jo_message = JsonConvert.DeserializeObject<JObject>(text_message);
                    var jo_rbh = (JObject)jo_message[RbFormatType.RbHeader];

                    var v_rbhRoutingType = jo_rbh[RbHeaderElement.RoutingType];
                    if (v_rbhRoutingType == null)
                    {
                        rbTraceLog.WriteError("W001", "** Message skipped because RoutingType is null **", jo_message);
                        return;
                    }
                    string s_rbhRoutingType = (string)v_rbhRoutingType;
                    if (s_rbhRoutingType == RbRoutingType.LOG || s_rbhRoutingType == string.Empty)
                    {
                        // RoutingType == LOG -> only using IoT Hub with Stream Analytics  
                        return;
                    }

                    // Check RbHeader in detail
                    RbHeaderBuilder hdBuilder = new RbHeaderBuilder(jo_message, iothub_deviceId);
                    RbHeader rbh = null;
                    try
                    {
                        rbh = hdBuilder.ValidateJsonSchema();
                    }
                    catch (Exception ex)
                    {
                        rbTraceLog.WriteError("W002", "** Message skipped because of bad RbHeader **", ex);
                        return;

                    }

                    // Check StorageQueueSendEnabled property in RbHeader 
                    prevStorageQueueSendEnabled = storageQueueSendEnabled;
                    string messageStorageQueueSendEnabled = null;
                    if (storageQueueSendEnabled != "true")
                    {
                        try
                        {
                            messageStorageQueueSendEnabled = (string)jo_rbh[typeStorageQueueSendEnabled];
                        }
                        catch
                        {
                            messageStorageQueueSendEnabled = null;
                        }
                        if (messageStorageQueueSendEnabled == "true")
                            storageQueueSendEnabled = messageStorageQueueSendEnabled;
                    }

                    // Check RoutingType (CALL, D2D, CONTROL)
                    if (rbh.RoutingType == RbRoutingType.CALL || rbh.RoutingType == RbRoutingType.CALL_ASYNC)
                    {
                        appRouting = true;
                    }
                    else if (rbh.RoutingType == RbRoutingType.D2D)
                    {
                        devRouting = true;
                        if (rbh.AppProcessingId != string.Empty)
                        {
                            appRouting = true;
                        }
                    }
                    else if (rbh.RoutingType == RbRoutingType.CONTROL)
                    {
                        devRouting = false;
                        appRouting = false;
                    }
                    else
                    {
                        rbTraceLog.WriteError("W003", "** Message skipped because of bad RoutingType **", jo_message);
                        return;
                    }

                    // Device Router builds RbHeader
                    DeviceRouter dr = null;
                    if (devRouting)
                    {
                        dr = new DeviceRouter(rbh, sqlConnectionString);
                        rbh = dr.GetDeviceRouting();
                        string new_header = JsonConvert.SerializeObject(rbh);
                        jo_message[RbFormatType.RbHeader] = JsonConvert.DeserializeObject<JObject>(new_header);
                    }
                    else
                    {
                        rbh.TargetDeviceId = rbh.SourceDeviceId;
                        rbh.TargetType = RbTargetType.Device;
                    }

                    // Application Routing
                    JArray ja_messages = null;
                    if (appRouting)
                    {
                        // Application Call Logic
                        JObject jo_temp;
                        string rbBodyString;
                        try
                        {
                            jo_temp = (JObject)jo_message[RbFormatType.RbBody];
                            rbBodyString = JsonConvert.SerializeObject(jo_temp);
                        }
                        catch (Exception ex)
                        {
                            rbTraceLog.WriteError("E001", $"** RbBody is not regular JSON format ** {ex.ToString()}", jo_message);
                            return;
                        }

                        try
                        {
                            if (rbh.RoutingType == RbRoutingType.CALL_ASYNC)
                                await CallAppsWithQueue(rbh, rbBodyString, partitionKey.ToString());
                            else
                                ja_messages = await CallAppsWithHttp(rbh, rbBodyString, partitionKey.ToString());
                        }
                        catch (Exception ex)
                        {
                            rbTraceLog.WriteError("E002", $"** Error occured in CallApps ** {ex.ToString()}", jo_message);
                            return;
                        }
                    }
                    else
                    {
                        ja_messages = new JArray();
                        ja_messages.Add(jo_message);
                    }

                    // Send C2D Message
                    if (rbh.RoutingType == RbRoutingType.CALL
                        || rbh.RoutingType == RbRoutingType.D2D
                        || rbh.RoutingType == RbRoutingType.CONTROL)
                    {
                        if (storageQueueSendEnabled == "true")
                        {
                            // Send C2D message to Queue storage
                            RbC2dMessageToQueue c2dsender = null;
                            c2dsender = new RbC2dMessageToQueue(ja_messages, storageQueueConnString, sqlConnectionString);
                            await c2dsender.SendToDeviceAsync();
                        }
                        else
                        {
                            // Send C2D message to IoT Hub
                            RbC2dMessageSender c2dsender = null;
                            c2dsender = new RbC2dMessageSender(ja_messages, iotHubConnectionString, sqlConnectionString);
                            await c2dsender.SendToDeviceAsync();
                        }
                        // StorageQueueSendEnabled property in RbHeader
                        storageQueueSendEnabled = prevStorageQueueSendEnabled;
                    }

                    // Get out of retry loop because of normal completion
                    break;

                }
                catch (Exception ex)
                {
                    rbTraceLog.WriteError("E003", $"** Critical error occured ** {ex.ToString()}", jo_message);

                    bool continueLoop = false;

                    if (ex != null && ex is SqlException)
                    {
                        foreach (SqlError error in (ex as SqlException).Errors)
                        {
                            if (sqlErrorListForRetry.Contains(error.Number))
                            {
                                continueLoop = true;
                                break;  // Exit foreach loop
                            }
                        }

                        if (continueLoop)
                        {
                            ++loopCounter;
                            rbTraceLog.WriteLog($"Transaction retry has started. Count({loopCounter})");

                            if (loopCounter > maxLoopCounter)
                            {
                                break;  // Get out of retry loop because counter reached max number
                            }
                            else
                            {
                                Thread.Sleep(sleepInterval);
                            }
                        }
                        else
                        {
                            break;  // Get out of retry loop because of another sql error
                        }
                    }
                    else
                    {
                        throw;
                    }
                }
            }

        }

        /// <summary>
        /// Creates an PartitionReceiver from the given connection sting and partition key.
        /// The Reliable Dictionaries are used to create a receiver from wherever the service last left off,
        /// or from the current date/time if it's the first time the service is coming up.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="consumerGroup"></param>
        /// <param name="servicePartitionKey"></param>
        /// <param name="epochDictionary"></param>
        /// <param name="offsetDictionary"></param>
        /// <returns>PartitionReceiver</returns>
        private async Task<PartitionReceiver> ConnectToIoTHubAsync(
            EventHubClient client,
            string consumerGroup,
            long servicePartitionKey,
            IReliableDictionary<string, long> epochDictionary,
            IReliableDictionary<string, string> offsetDictionary)
        {
            // This gives each partition its own dedicated TCP connection to IoT Hub.
            var eventHubRuntimeInfo = await client.GetRuntimeInformationAsync();
            PartitionReceiver partitionReceiver;

            // Get an IoT Hub partition ID that corresponds to this partition's low key.
            // This assumes that this service has a partition count 'n' that is equal to the IoT Hub partition count and a partition range of 0..n-1.
            // For example, given an IoT Hub with 32 partitions, this service should be created with:
            // partition count = 32
            // partition range = 0..31
            string eventHubPartitionId = eventHubRuntimeInfo.PartitionIds[servicePartitionKey];

            using (var tx = this.StateManager.CreateTransaction())
            {
                ConditionalValue<string> offsetResult = await offsetDictionary.TryGetValueAsync(tx, "offset", LockMode.Default);
                ConditionalValue<long> epochResult = await epochDictionary.TryGetValueAsync(tx, "epoch", LockMode.Update);

                long newEpoch = epochResult.HasValue
                    ? epochResult.Value + 1
                    : 0;

                if (offsetResult.HasValue)
                {
                    // continue where the service left off before the last failover or restart.
                    ServiceEventSource.Current.ServiceMessage(
                        this.Context,
                        "Creating EventHub listener on partition {0} with offset {1}",
                        eventHubPartitionId,
                        offsetResult.Value);

                    partitionReceiver = client.CreateEpochReceiver(consumerGroup, eventHubPartitionId, EventPosition.FromOffset(offsetResult.Value), newEpoch);
                }
                else
                {
                    // first time this service is running so there is no offset value yet.
                    // start with the current time.
                    ServiceEventSource.Current.ServiceMessage(
                        this.Context,
                        "Creating EventHub listener on partition {0} with offset {1}",
                        eventHubPartitionId,
                        DateTime.UtcNow);

                    partitionReceiver = client.CreateEpochReceiver(consumerGroup, eventHubPartitionId, EventPosition.FromEnqueuedTime(DateTime.UtcNow), newEpoch);
                }

                // epoch is recorded each time the service fails over or restarts.
                await epochDictionary.SetAsync(tx, "epoch", newEpoch);
                await tx.CommitAsync();
            }

            return partitionReceiver;
        }

        /// <summary>
        /// Call Apps (Azure Functions or Custom App) with HTTP
        /// </summary>
        /// <param name="rbh"></param>
        /// <param name="rbBodyString"></param>
        /// <param name="partitionId"></param>
        /// <returns>Task<JArray></returns>
        private async Task<JArray> CallAppsWithHttp(RbHeader rbh, string rbBodyString, string partitionId)
        {
            // Get App Master Info
            RbAppMasterCache rbappmc = GetAppMasterInfo(rbh);
            // Get App Routing Info
            RbAppRouterCache rbapprc = GetAppRoutingInfo(rbh);

            JArray ja_messages = new JArray();
            RbMessage message = new RbMessage();
            message.RbHeader = rbh;
            message.RbBody = (JObject)JsonConvert.DeserializeObject(rbBodyString);
            var strMessage = (string)JsonConvert.SerializeObject(message);

            // HTTP Client
            var client = new HttpClient();

            // Create request body
            byte[] byteData = Encoding.UTF8.GetBytes(strMessage);
            var content = new ByteArrayContent(byteData);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            // Call REST API
            var response = await client.PostAsync(rbapprc.HttpUri, content);
            if (response.IsSuccessStatusCode)
            {
                var responseText = await response.Content.ReadAsStringAsync();
                try
                {
                    message.RbBody = (JObject)JsonConvert.DeserializeObject(responseText);
                }
                catch
                {
                    var badResponse = new BadResponse();
                    badResponse.StatusCode = "Parse Error";
                    badResponse.ReasonPhrase = "HTTP response is not regular JSON format";
                    message.RbBody = badResponse;
                }
            }
            else
            {
                var badResponse = new BadResponse();
                badResponse.StatusCode = response.StatusCode.ToString();
                badResponse.ReasonPhrase = response.ReasonPhrase.ToString();
                message.RbBody = badResponse;
            }
            message.RbHeader = rbh;

            string json_message = JsonConvert.SerializeObject(message);
            JObject jo = (JObject)JsonConvert.DeserializeObject(json_message);
            ja_messages.Add(jo);

            return ja_messages;
        }

        /// <summary>
        /// Call Apps Async (Azure Functions) with Queue Storage
        /// </summary>
        /// <param name="rbh"></param>
        /// <param name="rbBodyString"></param>
        /// <param name="partitionId"></param>
        /// <returns>Task</returns>
        private async Task CallAppsWithQueue(RbHeader rbh, string rbBodyString, string partitionId)
        {
            // Get App Master Info
            RbAppMasterCache rbappmc = GetAppMasterInfo(rbh);
            // Get App Routing Info
            RbAppRouterCache rbapprc = GetAppRoutingInfo(rbh);

            RbMessage message = new RbMessage();
            message.RbHeader = rbh;
            message.RbBody = (JObject)JsonConvert.DeserializeObject(rbBodyString);
            string queueName = rbapprc.AppId + "-" + rbapprc.AppProcessingId;

            var sender = new RbAppMessageToQueue(message, rbappmc.QueueStorageAccount, rbappmc.QueueStorageKey, queueName);
            await sender.SendAsync();
        }

        /// <summary>
        /// Get application master information.
        /// </summary>
        /// <returns>RbAppMasterCache</returns>
        private RbAppMasterCache GetAppMasterInfo(RbHeader rbh)
        {
            AppMaster am = null;
            bool am_action = true;
            RbAppMasterCache rbappmc = null;
            if (rbAppMasterCacheDic.ContainsKey(rbh.AppId))
            {
                rbappmc = (RbAppMasterCache)rbAppMasterCacheDic[rbh.AppId];
                if (rbappmc.CacheExpiredDatetime >= DateTime.Now)
                    am_action = false;
            }
            if (am_action)
            {
                am = new AppMaster(rbh.AppId, rbEncPassPhrase, sqlConnectionString, rbCacheExpiredTimeSec);
                rbappmc = am.GetAppMaster();
                if (rbappmc != null)
                {
                    lock (thisLock)
                    {
                        rbAppMasterCacheDic[rbh.AppId] = rbappmc;
                    }
                }
                else
                {
                    throw new ApplicationException("Error ** GetAppMaster() returns Null Object");
                }
            }

            return rbappmc;
        }

        /// <summary>
        /// Get application routing information.
        /// </summary>
        /// <returns>RbAppRouterCache</returns>
        private RbAppRouterCache GetAppRoutingInfo(RbHeader rbh)
        {
            AppRouter ar = null;
            bool ar_action = true;
            string cachekey = rbh.AppId + "_" + rbh.AppProcessingId;
            RbAppRouterCache rbapprc = null;
            if (rbAppRouterCacheDic.ContainsKey(cachekey))
            {
                rbapprc = (RbAppRouterCache)rbAppRouterCacheDic[cachekey];
                if (rbapprc.CacheExpiredDatetime >= DateTime.Now)
                    ar_action = false;
            }
            if (ar_action)
            {
                ar = new AppRouter(rbh.AppId, rbh.AppProcessingId, sqlConnectionString, rbCacheExpiredTimeSec);
                rbapprc = ar.GetAppRouting();
                if (rbapprc != null)
                {
                    lock (thisLock)
                    {
                        rbAppRouterCacheDic[cachekey] = rbapprc;
                    }
                }
                else
                {
                    throw new ApplicationException("Error ** GetAppRouting() returns Null Object");
                }
            }

            return rbapprc;
        }

        /// <summary>
        /// Set environment variables.
        /// </summary>
        /// <returns></returns>
        private void InitializeSetting()
        {
            // IoT Hub Connection Info in Setting.xml
            iotHubConnectionString =
                this.Context.CodePackageActivationContext
                    .GetConfigurationPackageObject("Config")
                    .Settings
                    .Sections["IoTHub.ConnectionInfo"]
                    .Parameters["ConnectionString"]
                    .Value;
            compatibleEventHubConnString =
                this.Context.CodePackageActivationContext
                    .GetConfigurationPackageObject("Config")
                    .Settings
                    .Sections["IoTHub.ConnectionInfo"]
                    .Parameters["CompatibleEventHubConnString"]
                    .Value;
            iotHubConsumerGroup =
                this.Context.CodePackageActivationContext
                    .GetConfigurationPackageObject("Config")
                    .Settings
                    .Sections["IoTHub.ConnectionInfo"]
                    .Parameters["ConsumerGroup"]
                    .Value;

            // Storage Queue Connection Info in Setting.xml
            storageQueueSendEnabled =
                this.Context.CodePackageActivationContext
                    .GetConfigurationPackageObject("Config")
                    .Settings
                    .Sections["StorageQueue.ConnectionInfo"]
                    .Parameters["SendEnabled"]
                    .Value;
            string _storageQueueAccount =
                this.Context.CodePackageActivationContext
                    .GetConfigurationPackageObject("Config")
                    .Settings
                    .Sections["StorageQueue.ConnectionInfo"]
                    .Parameters["StorageAccount"]
                    .Value;
            string _storageQueueKey =
                this.Context.CodePackageActivationContext
                    .GetConfigurationPackageObject("Config")
                    .Settings
                    .Sections["StorageQueue.ConnectionInfo"]
                    .Parameters["StorageKey"]
                    .Value;
            storageQueueConnString = string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}",
                                                            _storageQueueAccount, _storageQueueKey);

            // SQL Database Connection Info in Setting.xml
            sqlConnectionString =
                this.Context.CodePackageActivationContext
                    .GetConfigurationPackageObject("Config")
                    .Settings
                    .Sections["Sql.ConnectionInfo"]
                    .Parameters["ConnectionString"]
                    .Value;

            // Pass phrase & Cache expired time in Setting.xml
            rbEncPassPhrase =
                this.Context.CodePackageActivationContext
                    .GetConfigurationPackageObject("Config")
                    .Settings
                    .Sections["RbSetting.Info"]
                    .Parameters["EncPassPhrase"]
                    .Value;
            string _cacheExpiredTimeSec =
                this.Context.CodePackageActivationContext
                    .GetConfigurationPackageObject("Config")
                    .Settings
                    .Sections["RbSetting.Info"]
                    .Parameters["CacheExpiredTimeSec"]
                    .Value;
            rbCacheExpiredTimeSec = int.Parse(_cacheExpiredTimeSec);

            // RbTraceLog Info in Setting.xml
            string _rbTraceStorageAccount =
                this.Context.CodePackageActivationContext
                    .GetConfigurationPackageObject("Config")
                    .Settings
                    .Sections["RbTrace.Info"]
                    .Parameters["StorageAccount"]
                    .Value;
            string _rbTraceStorageKey =
                this.Context.CodePackageActivationContext
                    .GetConfigurationPackageObject("Config")
                    .Settings
                    .Sections["RbTrace.Info"]
                    .Parameters["StorageKey"]
                    .Value;
            rbTraceStorageConnString = string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}",
                                                            _rbTraceStorageAccount, _rbTraceStorageKey);
            rbTraceStorageTableName =
                this.Context.CodePackageActivationContext
                    .GetConfigurationPackageObject("Config")
                    .Settings
                    .Sections["RbTrace.Info"]
                    .Parameters["StorageTableName"]
                    .Value;
            rbTraceLevel =
                this.Context.CodePackageActivationContext
                    .GetConfigurationPackageObject("Config")
                    .Settings
                    .Sections["RbTrace.Info"]
                    .Parameters["TraceLevel"]
                    .Value;
        }
    }
}
