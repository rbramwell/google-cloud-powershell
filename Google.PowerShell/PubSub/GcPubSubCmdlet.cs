using Google.Apis.Pubsub.v1;
using Google.Apis.Pubsub.v1.Data;
using Google.PowerShell.Common;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Management.Automation;
using System.Collections;
using System.Text;

namespace Google.PowerShell.PubSub
{
    public class GcpsCmdlet : GCloudCmdlet
    {
        public PubsubService Service { get; private set; }

        public GcpsCmdlet()
        {
            Service = new PubsubService(GetBaseClientServiceInitializer());
        }

        /// <summary>
        /// Prefix projects/{project name}/topics/{topic} to topicName if not present.
        /// </summary>
        protected string PrefixProjectToTopic(string topicName, string project)
        {
            if (!string.IsNullOrWhiteSpace(topicName) && !topicName.StartsWith($"projects/{project}/topics"))
            {
                topicName = $"projects/{project}/topics/{topicName}";
            }
            return topicName;
        }

        /// <summary>
        /// Prefix projects/{project name}/subscriptions/{subscriptions} to subscriptionName if not present.
        /// </summary>
        protected string PrefixProjectToSubscription(string subscriptionName, string project)
        {
            if (!string.IsNullOrWhiteSpace(subscriptionName) && !subscriptionName.StartsWith($"projects/{project}/subscriptions"))
            {
                subscriptionName = $"projects/{project}/subscriptions/{subscriptionName}";
            }
            return subscriptionName;
        }

        /// <summary>
        /// Converts a hashtable to a dictionary
        /// </summary>
        protected static Dictionary<K, V> ConvertToDictionary<K, V>(Hashtable hashTable)
        {
            return hashTable.Cast<DictionaryEntry>().ToDictionary(kvp => (K)kvp.Key, kvp => (V)kvp.Value);
        }
    }

    [Cmdlet(VerbsCommon.New, "GcpsTopic")]
    public class NewGcpsTopic : GcpsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to check for log entries. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        [Parameter(Mandatory = true)]
        [Alias("Topic")]
        public string[] Name { get; set; }

        protected override void ProcessRecord()
        {
            foreach (string topicName in Name)
            {
                string formattedTopicname = PrefixProjectToTopic(topicName, Project);
                Topic topic = new Topic() { Name = formattedTopicname };
                ProjectsResource.TopicsResource.CreateRequest request = Service.Projects.Topics.Create(topic, formattedTopicname);
                Topic response = request.Execute();
                WriteObject(response);
            }
        }
    }

    [Cmdlet(VerbsCommon.Get, "GcpsTopic")]
    public class GetGcpsTopic : GcpsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to check for log entries. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        [Alias("Topic")]
        public string[] Name { get; set; }

        protected override void ProcessRecord()
        {
            if (Name != null && Name.Length > 0)
            {
                foreach (string topicName in Name)
                {
                    string formattedTopicName = PrefixProjectToTopic(topicName, Project);
                    ProjectsResource.TopicsResource.GetRequest getRequest = Service.Projects.Topics.Get(formattedTopicName);
                    WriteObject(getRequest.Execute());
                }
            }
            else
            {
                ProjectsResource.TopicsResource.ListRequest listRequest = Service.Projects.Topics.List($"projects/{Project}");
                do
                {
                    ListTopicsResponse response = listRequest.Execute();

                    if (response.Topics != null)
                    {
                        WriteObject(response.Topics, true);
                    }
                    listRequest.PageToken = response.NextPageToken;
                }
                while (!Stopping && listRequest.PageToken != null);
            }
        }
    }

    [Cmdlet(VerbsCommon.Remove, "GcpsTopic")]
    public class RemoveGcpsTopic : GcpsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to check for log entries. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
        [Alias("Topic")]
        public string[] Name { get; set; }

        protected override void ProcessRecord()
        {
            foreach (string topicName in Name)
            {
                string formattedTopicName = PrefixProjectToTopic(topicName, Project);
                ProjectsResource.TopicsResource.DeleteRequest request = Service.Projects.Topics.Delete(formattedTopicName);
                request.Execute();
            }
        }
    }

    [Cmdlet(VerbsCommon.New, "GcpsMessage")]
    public class NewGcpsMessage : GcpsCmdlet
    {
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string Data { get; set; }

        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public Hashtable Attributes { get; set; }

        protected override void ProcessRecord()
        {
            PubsubMessage psMessage = ConstructPubSubMessage(Data, Attributes);
            WriteObject(psMessage);
        }

        internal static PubsubMessage ConstructPubSubMessage(string data, Hashtable attributes)
        {
            Dictionary<string, string> attributesDict = null;
            string base64EncodedMessage = null;
            if (attributes != null && attributes.Count > 0)
            {
                attributesDict = ConvertToDictionary<string, string>(attributes);
            }
            if (!string.IsNullOrWhiteSpace(data))
            {
                base64EncodedMessage = Convert.ToBase64String(Encoding.UTF8.GetBytes(data));
            }
            // A valid PubSub message must have either a non-empty message or a non-empty attributes.
            if (attributesDict == null && base64EncodedMessage == null)
            {
                throw new ArgumentException("Cannot construct a PubSub message because both the message and the attributes are empty.");
            }
            PubsubMessage psMessage = new PubsubMessage() { Data = base64EncodedMessage, Attributes = attributesDict };
            return psMessage;
        }
    }

    [Cmdlet(VerbsData.Publish, "GcpsMessage", DefaultParameterSetName = ParameterSetNames.DataAndAttributes)]
    public class PublishGcpsMessage : GcpsCmdlet
    {
        private class ParameterSetNames
        {
            public const string DataAndAttributes = "DataAndAttributes";
            public const string Message = "Message";
        }

        /// <summary>
        /// <para type="description">
        /// The project to check for log entries. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        public string Topic { get; set; }

        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DataAndAttributes)]
        [ValidateNotNullOrEmpty]
        public string Data { get; set; }

        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.DataAndAttributes)]
        [ValidateNotNullOrEmpty]
        public Hashtable Attributes { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.Message)]
        [ValidateNotNullOrEmpty]
        public PubsubMessage[] Message { get; set; }

        protected override void ProcessRecord()
        {
            Topic = PrefixProjectToTopic(Topic, Project);
            PublishRequest requestBody = null;
            if (ParameterSetName == ParameterSetNames.DataAndAttributes)
            {
                PubsubMessage psMessage = NewGcpsMessage.ConstructPubSubMessage(Data, Attributes);
                requestBody = new PublishRequest() { Messages = new List<PubsubMessage>() { psMessage } };
            }
            else
            {
                requestBody = new PublishRequest() { Messages = Message };
            }
            ProjectsResource.TopicsResource.PublishRequest publishRequest = Service.Projects.Topics.Publish(requestBody, Topic);
            PublishResponse response = publishRequest.Execute();

            WriteObject(response.MessageIds, true);
        }
    }

    [Cmdlet(VerbsCommon.Get, "GcpsMessage")]
    public class GetGcpsMessage : GcpsCmdlet
    {
        private class PubSubMessageWithAckId : PubsubMessage
        {
            public PubSubMessageWithAckId() : base() { }

            public PubSubMessageWithAckId(ReceivedMessage receivedMessage)
            {
                AckId = receivedMessage.AckId;
                if (receivedMessage.Message != null)
                {
                    Attributes = receivedMessage.Message.Attributes;
                    Data = receivedMessage.Message.Data;
                    ETag = receivedMessage.Message.ETag;
                    MessageId = receivedMessage.Message.MessageId;
                    PublishTime = receivedMessage.Message.PublishTime;
                }
            }

            public string AckId { get; set; }
        }

        /// <summary>
        /// <para type="description">
        /// The project to check for log entries. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("Subscription")]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        [Parameter(Mandatory = false)]
        public int? MaxMessages { get; set; }

        [Parameter(Mandatory = false)]
        public SwitchParameter AutoAck { get; set; }

        protected override void ProcessRecord()
        {
            Name = PrefixProjectToSubscription(Name, Project);
            PullRequest requestBody = new PullRequest() { ReturnImmediately = true, MaxMessages = 100 };
            if (MaxMessages.HasValue)
            {
                requestBody.MaxMessages = MaxMessages;
            }

            ProjectsResource.SubscriptionsResource.PullRequest request = Service.Projects.Subscriptions.Pull(requestBody, Name);
            PullResponse response = request.Execute();
            IList<ReceivedMessage> receivedMessages = response.ReceivedMessages;

            if (receivedMessages == null || receivedMessages.Count == 0)
            {
                return;
            }

            if (AutoAck.IsPresent)
            {
                AcknowledgeRequest ackRequestBody = new AcknowledgeRequest()
                {
                    AckIds = receivedMessages.Select(message => message.AckId).ToList()
                };
                ProjectsResource.SubscriptionsResource.AcknowledgeRequest ackRequest =
                    Service.Projects.Subscriptions.Acknowledge(ackRequestBody, Name);
                ackRequest.Execute();
            }

            foreach (ReceivedMessage receivedMessage in receivedMessages)
            {
                PubSubMessageWithAckId messageWithAck = new PubSubMessageWithAckId(receivedMessage);
                byte[] base64Bytes = Convert.FromBase64String(messageWithAck.Data);
                messageWithAck.Data = Encoding.UTF8.GetString(base64Bytes);
                if (AutoAck.IsPresent)
                {
                    // Remove the AckId 
                    messageWithAck.AckId = null;
                }
                WriteObject(messageWithAck);
            }
        }
    }

    [Cmdlet(VerbsCommon.Get, "GcpsSubscription", DefaultParameterSetName = ParameterSetNames.Default)]
    public class GetGcpsSubscription : GcpsCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByTopic = "ByTopic";
            public const string Default = "Default";
        }

        /// <summary>
        /// <para type="description">
        /// The project to check for log entries. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.Default)]
        [ValidateNotNullOrEmpty]
        [Alias("Subscription")]
        public string[] Name { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.ByTopic)]
        [ValidateNotNullOrEmpty]
        public string Topic { get; set; }

        protected override void ProcessRecord()
        {
            // Handles the case where user wants to list all subscriptions in a particular topic.
            // In this case, we will have to make a call to get the name of all the subscription in that topic
            // before calling get request.
            if (ParameterSetName == ParameterSetNames.ByTopic)
            {
                Topic = PrefixProjectToTopic(Topic, Project);
                ProjectsResource.TopicsResource.SubscriptionsResource.ListRequest listRequest =
                    Service.Projects.Topics.Subscriptions.List(Topic);
                do
                {
                    ListTopicSubscriptionsResponse response = listRequest.Execute();
                    if (response.Subscriptions != null)
                    {
                        GetSubscriptions(response.Subscriptions);
                    }
                    listRequest.PageToken = response.NextPageToken;
                }
                while (!Stopping && listRequest.PageToken != null);
                return;
            }

            if (Name != null && Name.Length > 0)
            {
                GetSubscriptions(Name.Select(item => PrefixProjectToSubscription(item, Project)));
            }
            else
            {
                ProjectsResource.SubscriptionsResource.ListRequest listRequest =
                    Service.Projects.Subscriptions.List($"projects/{Project}");
                do
                {
                    ListSubscriptionsResponse response = listRequest.Execute();

                    if (response.Subscriptions != null)
                    {
                        WriteObject(response.Subscriptions, true);
                    }
                    listRequest.PageToken = response.NextPageToken;
                }
                while (!Stopping && listRequest.PageToken != null);
            }
        }

        /// <summary>
        /// Given a list of subscription names, returns the corresponding subscriptions.
        /// </summary>
        private void GetSubscriptions(IEnumerable<string> subscriptionNames)
        {
            foreach (string subscriptionName in subscriptionNames)
            {
                ProjectsResource.SubscriptionsResource.GetRequest getRequest = Service.Projects.Subscriptions.Get(subscriptionName);
                Subscription subscription = getRequest.Execute();
                WriteObject(subscription);
            }
        }
    }

    [Cmdlet(VerbsCommon.New, "GcpsSubscription", DefaultParameterSetName = ParameterSetNames.Default)]
    public class NewGcpsSubscription : GcpsCmdlet
    {
        private class ParameterSetNames
        {
            public const string DeletedTopic = "DeletedTopic";
            public const string Default = "Default";
        }

        /// <summary>
        /// <para type="description">
        /// The project to check for log entries. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        [Alias("Subscription")]
        public string Name { get; set; }

        [Parameter(Mandatory = false)]
        public int? AckDeadLine { get; set; }

        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string PushEndPoint { get; set; }

        [Parameter(Mandatory = false)]
        [ValidateNotNull]
        public Hashtable PushEndPointAttributes { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.Default)]
        [ValidateNotNullOrEmpty]
        public string Topic { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.DeletedTopic)]
        public SwitchParameter DeletedTopic { get; set; }

        protected override void ProcessRecord()
        {
            if (ParameterSetName == ParameterSetNames.DeletedTopic)
            {
                // To get subscription from deleted topic, set the value of Topic to _deleted-topic_.
                Topic = "_deleted-topic_";
            }
            if (ParameterSetName == ParameterSetNames.Default)
            {
                Topic = PrefixProjectToTopic(Topic, Project);
            }
            Name = PrefixProjectToSubscription(Name, Project);

            Subscription subscriptionBody = new Subscription()
            {
                Name = Name,
                Topic = Topic
            };

            if (AckDeadLine.HasValue)
            {
                subscriptionBody.AckDeadlineSeconds = AckDeadLine.Value;
            }
            if (PushEndPoint != null)
            {
                PushConfig pushConfig = new PushConfig() { PushEndpoint = PushEndPoint };
                if (PushEndPointAttributes != null && PushEndPointAttributes.Count > 0)
                {
                    pushConfig.Attributes = ConvertToDictionary<string, string>(PushEndPointAttributes);
                }
            }

            ProjectsResource.SubscriptionsResource.CreateRequest request =
                Service.Projects.Subscriptions.Create(subscriptionBody, Name);
            Subscription response = request.Execute();
            WriteObject(response);
        }
    }

    [Cmdlet(VerbsCommon.Remove, "GcpsSubscription")]
    public class RemoveGcpsSubscription : GcpsCmdlet
    {
        /// <summary>
        /// <para type="description">
        /// The project to check for log entries. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        [Parameter(Mandatory = false, ValueFromPipelineByPropertyName = true, ValueFromPipeline = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Subscription")]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            string subscriptionName = PrefixProjectToSubscription(Name, Project);
            ProjectsResource.SubscriptionsResource.DeleteRequest request = Service.Projects.Subscriptions.Delete(subscriptionName);
            request.Execute();
        }
    }
}
