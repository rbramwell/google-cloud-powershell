using Google.Apis.Pubsub.v1;
using Google.Apis.Pubsub.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;

namespace Google.PowerShell.PubSub
{

    public class PubSubMessageWithAckId : PubsubMessage
    {
        public PubSubMessageWithAckId() : base() { }

        public PubSubMessageWithAckId(ReceivedMessage receivedMessage, string subscription)
        {
            Subscription = subscription;
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
        public string Subscription { get; set; }
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
                PubSubMessageWithAckId messageWithAck = new PubSubMessageWithAckId(receivedMessage, Name);
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

    [Cmdlet(VerbsCommon.Set, "GcpsAckDeadline")]
    public class SetGcpsAckDeadline : GcpsCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByObject = "ByObject";
            public const string ByName = "ByName";
        }

        /// <summary>
        /// <para type="description">
        /// The project to check for log entries. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.ByName)]
        [Alias("Subscription")]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true, ParameterSetName = ParameterSetNames.ByName)]
        [ValidateNotNullOrEmpty]
        public string[] AckIds { get; set; }

        [Parameter(Mandatory = true)]
        public int AckDeadline { get; set; }

        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.ByObject)]
        [ValidateNotNullOrEmpty]
        public PubSubMessageWithAckId[] InputObject { get; set; }

        protected override void ProcessRecord()
        {
            if (ParameterSetName == ParameterSetNames.ByName)
            {
                Name = PrefixProjectToSubscription(Name, Project);
                ModifyAckDeadlineRequest requestBody = new ModifyAckDeadlineRequest()
                {
                    AckDeadlineSeconds = AckDeadline,
                    AckIds = AckIds.ToList()
                };
                ProjectsResource.SubscriptionsResource.ModifyAckDeadlineRequest request =
                    Service.Projects.Subscriptions.ModifyAckDeadline(requestBody, Name);
                request.Execute();
            }

            if (ParameterSetName == ParameterSetNames.ByObject)
            {
                // We group the message with subscription as key and Ack IDs as values and send 1 request per subscription.
                IEnumerable<IGrouping<string, string>> messageGroups =
                    InputObject.GroupBy(message => message.Subscription, message => message.AckId);
                foreach (IGrouping<string, string> messageGroup in messageGroups)
                {
                    ModifyAckDeadlineRequest requestBody = new ModifyAckDeadlineRequest()
                    {
                        AckDeadlineSeconds = AckDeadline,
                        AckIds = messageGroup.ToList()
                    };
                    ProjectsResource.SubscriptionsResource.ModifyAckDeadlineRequest request =
                        Service.Projects.Subscriptions.ModifyAckDeadline(requestBody, messageGroup.Key);
                    request.Execute();
                }
            }
        }
    }

    [Cmdlet(VerbsCommunications.Send, "GcpsAck")]
    public class SendGcpsAck : GcpsCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByObject = "ByObject";
            public const string ByName = "ByName";
        }

        /// <summary>
        /// <para type="description">
        /// The project to check for log entries. If not set via PowerShell parameter processing, will
        /// default to the Cloud SDK's DefaultProject property.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public string Project { get; set; }

        [Parameter(Mandatory = true, Position = 0, ParameterSetName = ParameterSetNames.ByName)]
        [Alias("Subscription")]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true, ParameterSetName = ParameterSetNames.ByName)]
        [ValidateNotNullOrEmpty]
        public string[] AckIds { get; set; }

        [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ParameterSetNames.ByObject)]
        [ValidateNotNullOrEmpty]
        public PubSubMessageWithAckId[] InputObject { get; set; }

        protected override void ProcessRecord()
        {
            if (ParameterSetName == ParameterSetNames.ByName)
            {
                Name = PrefixProjectToSubscription(Name, Project);
                AcknowledgeRequest requestBody = new AcknowledgeRequest() { AckIds = AckIds.ToList() };
                ProjectsResource.SubscriptionsResource.AcknowledgeRequest request =
                    Service.Projects.Subscriptions.Acknowledge(requestBody, Name);
                request.Execute();
            }

            if (ParameterSetName == ParameterSetNames.ByObject)
            {
                // We group the message with subscription as key and Ack IDs as values and send 1 request per subscription.
                IEnumerable<IGrouping<string, string>> messageGroups =
                    InputObject.GroupBy(message => message.Subscription, message => message.AckId);
                foreach (IGrouping<string, string> messageGroup in messageGroups)
                {
                    AcknowledgeRequest requestBody = new AcknowledgeRequest() { AckIds = messageGroup.ToList() };
                    ProjectsResource.SubscriptionsResource.AcknowledgeRequest request =
                        Service.Projects.Subscriptions.Acknowledge(requestBody, messageGroup.Key);
                    request.Execute();
                }
            }
        }
    }
}

