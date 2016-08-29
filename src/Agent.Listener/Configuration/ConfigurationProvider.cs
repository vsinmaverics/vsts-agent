using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.VisualStudio.Services.Agent.Listener.Configuration
{
    public interface IConfigurationProvider : IExtension
    {
        string ConfigurationProviderType { get; }

        void InitConnection(IAgentServer agentServer);

        void InitConnectionWithCollection(CommandSettings command, string tfsUrl, VssCredentials creds);

        Task<int> GetPoolId(CommandSettings command);

        Task<TaskAgent> UpdateAgentAsync(int poolId, TaskAgent agent);

        Task<TaskAgent> AddAgentAsync(int poolId, TaskAgent agent);

        Task DeleteAgentAsync(int agentPoolId, int agentId);

        void UpdateAgentSetting(AgentSettings settings);
    }

    public abstract class ConfigurationProvider : AgentService
    {
        public Type ExtensionType => typeof(IConfigurationProvider);
        protected ITerminal _term;
        protected IAgentServer _agentServer;

        public override void Initialize(IHostContext hostContext)
        {
            base.Initialize(hostContext);
            _term = hostContext.GetService<ITerminal>();
        }

        protected void InitializeServerConnection(IAgentServer agentServer)
        {
            _agentServer = agentServer;
        }
        
        protected Task<TaskAgent> UpdateAgent(int poolId, TaskAgent agent)
        {
           return _agentServer.UpdateAgentAsync(poolId, agent);
        }

        protected Task<TaskAgent> AddAgent(int poolId, TaskAgent agent)
        {
            return _agentServer.AddAgentAsync(poolId, agent);
        }

        protected Task DeleteAgent(int poolId, int agentId)
        {
            return _agentServer.DeleteAgentAsync(poolId, agentId);
        }
    }

    public sealed class AutomationAgentConfiguration : ConfigurationProvider, IConfigurationProvider
    {
        public string ConfigurationProviderType
            => Constants.Agent.AgentConfigurationProvider.BuildReleasesAgentConfiguration;

        public void InitConnection(IAgentServer agentServer)
        {
            InitializeServerConnection(agentServer);
        }

        public void InitConnectionWithCollection(CommandSettings command, string tfsUrl, VssCredentials creds)
        {
            // No implementation required
        }

        public void UpdateAgentSetting(AgentSettings settings)
        {
            // No implementation required
        }

        public async Task<int> GetPoolId(CommandSettings command)
        {
            int poolId = 0;
            string poolName;
            while (true)
            {
                poolName = command.GetPool();
                try
                {
                    poolId = await GetPoolIdAsync(poolName);
                }
                catch (Exception e) when (!command.Unattended)
                {
                    _term.WriteError(e);
                }

                if (poolId > 0)
                {
                    break;
                }

                _term.WriteError(StringUtil.Loc("FailedToFindPool"));
            }
            return poolId;            
        }

        public Task<TaskAgent> UpdateAgentAsync(int poolId, TaskAgent agent)
        {
            return UpdateAgent(poolId, agent);
        }

        public Task<TaskAgent> AddAgentAsync(int poolId, TaskAgent agent)
        {
            return AddAgent(poolId, agent);
        }

        public Task DeleteAgentAsync(int agentPoolId, int agentId)
        {
            return DeleteAgent(agentPoolId,agentId);
        }

        private async Task<int> GetPoolIdAsync(string poolName)
        {
            int poolId = 0;
            List<TaskAgentPool> pools = await _agentServer.GetAgentPoolsAsync(poolName);
            Trace.Verbose("Returned {0} pools", pools.Count);

            if (pools.Count == 1)
            {
                poolId = pools[0].Id;
                Trace.Info("Found pool {0} with id {1}", poolName, poolId);
            }

            return poolId;
        }

    }

    public sealed class DeploymentAgentConfiguration : ConfigurationProvider, IConfigurationProvider
    {
        private IAgentServer _collectionAgentServer;
        private string _projectName;
        private string _machineGroupName;

        public string ConfigurationProviderType
            => Constants.Agent.AgentConfigurationProvider.DeploymentAgentConfiguration;

        public void InitConnection(IAgentServer agentServer)
        {
            Trace.Info("Agent is a DeploymentAgent");
            InitializeServerConnection(agentServer);

            // Init it with default agent server, if collection flow is required, InitConnectionWithCollection() will take care!
            _collectionAgentServer = agentServer;
        }

        public async void InitConnectionWithCollection(CommandSettings command, string tfsUrl, VssCredentials creds)
        {
            string collectionName;
            Trace.Info("Get the Collection name for tfs and validate the connection");
            // No need to loop for cread, as creds are already validated with ConfigManager!
            while (true)
            {
                // Get the Collection Name
                collectionName = command.GetCollectionName("DefaultCollection");

                UriBuilder uriBuilder = new UriBuilder(new Uri(tfsUrl));
                uriBuilder.Path = uriBuilder.Path + "/" + collectionName;
                Trace.Info("Tfs Ulr to connect - {0}", uriBuilder.Uri.AbsoluteUri);
                try
                {
                    // Validate can connect.
                    await TestConnectAsync(uriBuilder.Uri.AbsoluteUri, creds);
                    Trace.Info("Connect complete.");
                    break;
                }
                catch (Exception e) when (!command.Unattended)
                {
                    _term.WriteError(e);
                    _term.WriteError(StringUtil.Loc("FailedToConnect"));
                }
            }
        }

        public async Task<int> GetPoolId(CommandSettings command)
        {
            int poolId = 0;
            while (true)
            {
                _projectName = command.GetProjectName();
                _machineGroupName = command.GetMachineGroupName();
                try
                {
                    poolId =  await GetPoolIdAsync(_projectName, _machineGroupName);
                }
                catch (Exception e) when (!command.Unattended)
                {
                    _term.WriteError(e);
                }

                if (poolId > 0)
                {
                    break;
                }

                _term.WriteError(StringUtil.Loc("FailedToFindPool"));
            }
            
            return poolId;
        }

        public Task<TaskAgent> UpdateAgentAsync(int poolId, TaskAgent agent)
        {
            return UpdateAgent(poolId, agent);
            // this may have additional calls related to Machine Group
        }

        public Task<TaskAgent> AddAgentAsync(int poolId, TaskAgent agent)
        {
            return AddAgent(poolId, agent);
            // this may have additional calls related to Machine Group
        }

        public Task DeleteAgentAsync(int agentPoolId, int agentId)
        {
            return DeleteAgent(agentPoolId, agentId);
        }

        public void UpdateAgentSetting(AgentSettings settings)
        {
            settings.MachineGroupName = _machineGroupName;
            settings.ProjectName = _projectName;
        }

        private async Task<int> GetPoolIdAsync(string projectName, string machineGroupName)
        {
            int poolId = 0;
            List<TaskAgentQueue> machineGroup = await _collectionAgentServer.GetAgentQueuesAsync(projectName, machineGroupName);
            Trace.Verbose("Returned {0} machineGroup", machineGroup.Count);

            if (machineGroup.Count == 1)
            {
                int queueId = machineGroup[0].Id;
                Trace.Info("Found queue {0} with id {1}", machineGroupName, queueId);
                poolId = machineGroup[0].Pool.Id;
                Trace.Info("Found poolId {0} with queueName {1}", poolId, machineGroupName);
            }

            return poolId;
        }

        private async Task TestConnectAsync(string url, VssCredentials creds)
        {
            _term.WriteLine(StringUtil.Loc("ConnectingToServer"));
            VssConnection connection = ApiUtil.CreateConnection(new Uri(url), creds);

            _collectionAgentServer = HostContext.CreateService<IAgentServer>();
            await _collectionAgentServer.ConnectAsync(connection);
        }

    }

}
