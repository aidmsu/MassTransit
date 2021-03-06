// Copyright 2007-2017 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.AzureServiceBusTransport.Topology.Topologies
{
    using System;
    using System.Collections.Generic;
    using Builders;
    using Configurators;
    using MassTransit.Topology;
    using MassTransit.Topology.Topologies;
    using Newtonsoft.Json.Linq;
    using Specifications;
    using Util;


    public class ServiceBusMessageConsumeTopology<TMessage> :
        MessageConsumeTopology<TMessage>,
        IServiceBusMessageConsumeTopologyConfigurator<TMessage>,
        IServiceBusMessageConsumeTopologyConfigurator
        where TMessage : class
    {
        readonly IMessageTopology<TMessage> _messageTopology;
        readonly IList<IServiceBusConsumeTopologySpecification> _specifications;

        public ServiceBusMessageConsumeTopology(IMessageTopology<TMessage> messageTopology)
        {
            _messageTopology = messageTopology;
            _specifications = new List<IServiceBusConsumeTopologySpecification>();
        }

        static bool IsBindableMessageType => typeof(JToken) != typeof(TMessage);

        public void Apply(IReceiveEndpointConsumeTopologyBuilder builder)
        {
            foreach (var specification in _specifications)
                specification.Apply(builder);
        }

        public void Subscribe(string subscriptionName, Action<ISubscriptionConfigurator> configure = null)
        {
            if (!IsBindableMessageType)
            {
                _specifications.Add(new InvalidServiceBusConsumeTopologySpecification(TypeMetadataCache<TMessage>.ShortName, "Is not a bindable message type"));
                return;
            }

            var topicPath = _messageTopology.EntityName;

            var topicConfigurator = new TopicConfigurator(topicPath, TypeMetadataCache<TMessage>.IsTemporaryMessageType);

            var subscriptionConfigurator = new SubscriptionConfigurator(topicPath, subscriptionName);

            configure?.Invoke(subscriptionConfigurator);

            var specification = new SubscriptionConsumeTopologySpecification(topicConfigurator.GetTopicDescription(),
                subscriptionConfigurator.GetSubscriptionDescription());

            _specifications.Add(specification);
        }
    }
}