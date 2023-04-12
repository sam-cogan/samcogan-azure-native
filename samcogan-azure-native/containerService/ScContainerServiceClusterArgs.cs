using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pulumi;

namespace samcogan.azurenative.containerService
{
    public class ScContainerServiceClusterArgs: ResourceArgs
    {
        public Input<string> ResourceGroupName { get; set; }
        public Input<string> ClusterName { get; set; }
        public Input<int> NodeCount { get; set; }
        public Input<string> NodeSize { get; set; }

        public Input<string>? AddressSpace { get; set; }
        public Input<string>? SubnetName { get; set; }

        public Input<string> AKSVersion { get; set; }
    }
}
