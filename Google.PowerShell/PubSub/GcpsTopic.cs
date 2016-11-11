using Google.Apis.Pubsub.v1;
using Google.Apis.Pubsub.v1.Data;
using Google.PowerShell.Common;
using System.Management.Automation;

namespace Google.PowerShell.PubSub
{
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

        [Parameter(Mandatory = true, Position = 0)]
        [Alias("Name")]
        public string[] Topic { get; set; }

        protected override void ProcessRecord()
        {
            foreach (string topicName in Topic)
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
        [Alias("Name")]
        public string[] Topic { get; set; }

        protected override void ProcessRecord()
        {
            if (Topic != null && Topic.Length > 0)
            {
                foreach (string topicName in Topic)
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

        [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
        [Alias("Name")]
        public string[] Topic { get; set; }

        protected override void ProcessRecord()
        {
            foreach (string topicName in Topic)
            {
                string formattedTopicName = PrefixProjectToTopic(topicName, Project);
                ProjectsResource.TopicsResource.DeleteRequest request = Service.Projects.Topics.Delete(formattedTopicName);
                request.Execute();
            }
        }
    }
}
