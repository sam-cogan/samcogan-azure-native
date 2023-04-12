using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pulumi;
using Pulumi.AzureNative.ContainerService;
using Pulumi.AzureNative.ContainerService.Inputs;
using Pulumi.AzureNative.Network;
using Pulumi.AzureNative.Network.Inputs;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Resources.Inputs;

namespace samcogan.azurenative.containerService
{

    public class ScContainerServiceCluster: ComponentResource
    {
        public Output<string> ClusterUrl { get; private set; }
        public Output<string> KubeConfig { get; private set; }
        public ScContainerServiceCluster(string name, ScContainerServiceClusterArgs args,
            ComponentResourceOptions options = null)
            : base("samcogan-azure-native:containerService:ScContainerServiceCluster", name, options)
        {

            // Create the resource group
            var resourceGroup = new ResourceGroup($"rg-{name}", new ResourceGroupArgs
            {
                ResourceGroupName = args.ResourceGroupName,
                Location = "East US"
            }, new CustomResourceOptions { Parent = this });

            // Create the virtual network
            var vnet = new VirtualNetwork($"vnet-{name}", new VirtualNetworkArgs
            {
                ResourceGroupName = resourceGroup.Name,
                Location = resourceGroup.Location,
                AddressSpace = new AddressSpaceArgs
                {
                    AddressPrefixes = new[] { args.AddressSpace ?? "10.0.0.0/16" }
                },
                Subnets = new[]
                {
                new Pulumi.AzureNative.Network.Inputs.SubnetArgs
                {
                    Name = args.SubnetName ?? "aks-subnet",
                    AddressPrefix = "10.0.0.0/24"
                }
            }
            }, new CustomResourceOptions { Parent = resourceGroup });

            // Create the AKS cluster
            var cluster = new ManagedCluster($"cluster-{name}", new ManagedClusterArgs
            {
                ResourceGroupName = resourceGroup.Name,
                Location = resourceGroup.Location,
                EnableRBAC = true,
                AadProfile = new ManagedClusterAADProfileArgs
                {
                    Managed = true,
                    EnableAzureRBAC = true
                },
                AutoUpgradeProfile = new ManagedClusterAutoUpgradeProfileArgs
                {
                    UpgradeChannel = UpgradeChannel.Stable
                },
                AgentPoolProfiles = new[]
                {
                new ManagedClusterAgentPoolProfileArgs
                {
                    Name = "agentpool",
                    Count = args.NodeCount,
                    VmSize = args.NodeSize,
                    VnetSubnetID = vnet.Subnets.Apply(subnets => subnets[0].Id)!,
                    OsType = OSType.Linux
                    
                },
            },
                DnsPrefix = $"{args.ClusterName}-dns",
                KubernetesVersion = "1.25.5",
                NetworkProfile = new ContainerServiceNetworkProfileArgs
                {
                    NetworkPlugin = "azure",
                    ServiceCidr = "172.16.0.0/16",
                    DnsServiceIP = "172.16.0.10",
                    DockerBridgeCidr = "172.17.0.1/16"
                },
                Identity = new ManagedClusterIdentityArgs
                {
                    Type = Pulumi.AzureNative.ContainerService.ResourceIdentityType.SystemAssigned,
                },
            }, new CustomResourceOptions { Parent = resourceGroup });


            ClusterUrl = cluster.Fqdn;
            KubeConfig = Output.CreateSecret( Output.Tuple(resourceGroup.Name, cluster.Name)
                .Apply(items => Output.CreateSecret(GetKubeAdminConfig(items.Item1, items.Item2))));
        }

        private static async Task<string> GetKubeAdminConfig(string resourceGroupName, string clusterName)
        {
            var credentials = await ListManagedClusterAdminCredentials
                .InvokeAsync(new ListManagedClusterAdminCredentialsArgs
                {
                    ResourceGroupName = resourceGroupName,
                    ResourceName = clusterName
                }).ConfigureAwait(false);

            var encoded = credentials.Kubeconfigs[0].Value;
            var data = Convert.FromBase64String(encoded);
            return Encoding.UTF8.GetString(data);
        }
    }
}
