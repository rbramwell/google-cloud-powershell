using Google.Apis.Pubsub.v1;
using Google.Apis.Pubsub.v1.Data;
using Google.PowerShell.Common;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace Google.PowerShell.PubSub
{
    [Cmdlet(VerbsCommon.Get, "GcpsSubscription")]
    public class GetGcpsSubscription : GcpsCmdlet
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

        [Parameter(Mandatory = false, Position = 0)]
        [ValidateNotNullOrEmpty]
        [Alias("Name")]
        public string[] Subscription { get; set; }

        [Parameter(Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public string Topic { get; set; }

        protected override void ProcessRecord()
        {
            if (Subscription != null)
            {
                Subscription = Subscription.Select(item => PrefixProjectToSubscription(item, Project)).ToArray();
            }

            // Handles the case where user wants to list all subscriptions in a particular topic.
            // In this case, we will have to make a call to get the name of all the subscription in that topic
            // before calling get request.
            if (Topic != null)
            {
                Topic = PrefixProjectToTopic(Topic, Project);
                ProjectsResource.TopicsResource.SubscriptionsResource.ListRequest listRequest =
                    Service.Projects.Topics.Subscriptions.List(Topic);
                do
                {
                    ListTopicSubscriptionsResponse response = listRequest.Execute();
                    if (response.Subscriptions != null)
                    {
                        if (Subscription != null)
                        {
                            IEnumerable<string> selectedSubscriptions = response.Subscriptions
                                .Where(sub => Subscription.Contains(sub, System.StringComparer.OrdinalIgnoreCase));
                            GetSubscriptions(selectedSubscriptions);
                        }
                        else
                        {
                            GetSubscriptions(response.Subscriptions);
                        }
                    }
                    listRequest.PageToken = response.NextPageToken;
                }
                while (!Stopping && listRequest.PageToken != null);
                return;
            }

            if (Subscription != null && Subscription.Length > 0)
            {
                GetSubscriptions(Subscription.Select(item => PrefixProjectToSubscription(item, Project)));
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

    [Cmdlet(VerbsCommon.New, "GcpsSubscription")]
    public class NewGcpsSubscription : GcpsCmdlet
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

        [Parameter(Mandatory = true, Position = 0)]
        [ValidateNotNullOrEmpty]
        [Alias("Name")]
        public string Subscription { get; set; }

        [Parameter(Mandatory = true, Position = 1)]
        [ValidateNotNullOrEmpty]
        public string Topic { get; set; }

        [Parameter(Mandatory = false)]
        public int? AckDeadLine { get; set; }

        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string PushEndPoint { get; set; }

        [Parameter(Mandatory = false)]
        [ValidateNotNull]
        public Hashtable PushEndPointAttributes { get; set; }

        protected override void ProcessRecord()
        {
            Topic = PrefixProjectToTopic(Topic, Project);
            Subscription = PrefixProjectToSubscription(Subscription, Project);

            Subscription subscriptionBody = new Subscription()
            {
                Name = Subscription,
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
                Service.Projects.Subscriptions.Create(subscriptionBody, Subscription);
            Subscription response = request.Execute();
            WriteObject(response);
        }
    }

    [Cmdlet(VerbsCommon.Set, "GcpsPushConfig")]
    public class SetGcpsPushConfig : GcpsCmdlet
    {
        private class ParameterSetNames
        {
            public const string PushConfig = "PushConfig";
            public const string PullConfig = "PullConfig";
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
        public string Subscription { get; set; }

        [Parameter(Mandatory = true, Position = 1, ParameterSetName = ParameterSetNames.PushConfig)]
        [ValidateNotNullOrEmpty]
        public string PushEndPoint { get; set; }

        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.PushConfig)]
        [ValidateNotNull]
        public Hashtable PushEndPointAttributes { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.PullConfig)]
        public SwitchParameter PullConfig { get; set; }

        protected override void ProcessRecord()
        {
            Subscription = PrefixProjectToSubscription(Subscription, Project);

            ModifyPushConfigRequest requestBody = new ModifyPushConfigRequest();

            if (!PullConfig.IsPresent)
            {
                PushConfig pushConfig = new PushConfig() { PushEndpoint = PushEndPoint };
                if (PushEndPointAttributes != null && PushEndPointAttributes.Count > 0)
                {
                    pushConfig.Attributes = ConvertToDictionary<string, string>(PushEndPointAttributes);
                }
                requestBody.PushConfig = pushConfig;
            }

            ProjectsResource.SubscriptionsResource.ModifyPushConfigRequest request =
                Service.Projects.Subscriptions.ModifyPushConfig(requestBody, Subscription);
            request.Execute();
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

        [Parameter(Mandatory = false, Position = 0, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [Alias("Name")]
        public string Subscription { get; set; }

        protected override void ProcessRecord()
        {
            Subscription = PrefixProjectToSubscription(Subscription, Project);
            ProjectsResource.SubscriptionsResource.DeleteRequest request = Service.Projects.Subscriptions.Delete(Subscription);
            request.Execute();
        }
    }
}

