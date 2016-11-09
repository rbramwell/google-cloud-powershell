using Google.Apis.Pubsub.v1;
using Google.Apis.Pubsub.v1.Data;
using Google.PowerShell.Common;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Management.Automation;

namespace Google.PowerShell.PubSub
{
    public class GcPubSubCmdlet : GCloudCmdlet
    {
        public PubsubService Service { get; private set; }

        public GcPubSubCmdlet()
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
    }

    [Cmdlet(VerbsCommon.New, "GcPubSubTopic")]
    public class NewGcPubSubTopic : GcPubSubCmdlet
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

    [Cmdlet(VerbsCommon.Get, "GcPubSubTopic")]
    public class GetGcPubSubTopic : GcPubSubCmdlet
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

    [Cmdlet(VerbsCommon.Remove, "GcPubSubTopic")]
    public class RemoveGcPubSubTopic : GcPubSubCmdlet
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

    [Cmdlet(VerbsCommon.Get, "GcPubSubSubscription", DefaultParameterSetName = ParameterSetNames.Default)]
    public class GetGcPubSubSubscription : GcPubSubCmdlet
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
}
