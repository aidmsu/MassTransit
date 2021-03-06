﻿// Copyright 2007-2017 Chris Patterson, Dru Sellers, Travis Smith, et. al.
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
namespace MassTransit.RabbitMqTransport.Topology.Topologies
{
    using System;
    using System.Collections.Generic;
    using Builders;
    using Configuration;
    using Entities;
    using MassTransit.Topology;
    using MassTransit.Topology.Topologies;
    using Util;


    public class RabbitMqMessagePublishTopology<TMessage> :
        MessagePublishTopology<TMessage>,
        IRabbitMqMessagePublishTopologyConfigurator<TMessage>
        where TMessage : class
    {
        readonly IList<IRabbitMqMessagePublishTopology> _implementedMessageTypes;
        readonly IMessageTopology<TMessage> _messageTopology;

        public RabbitMqMessagePublishTopology(IMessageTopology<TMessage> messageTopology, IMessageExchangeTypeSelector<TMessage> exchangeTypeSelector)
        {
            _messageTopology = messageTopology;
            ExchangeTypeSelector = exchangeTypeSelector;

            _implementedMessageTypes = new List<IRabbitMqMessagePublishTopology>();
        }

        IMessageExchangeTypeSelector<TMessage> ExchangeTypeSelector { get; }

        public void Apply(IRabbitMqPublishTopologyBuilder builder)
        {
            var exchangeHandle = ExchangeDeclare(builder);

            if (builder.Exchange != null)
                builder.ExchangeBind(builder.Exchange, exchangeHandle, "", new Dictionary<string, object>());
            else
                builder.Exchange = exchangeHandle;

            foreach (IRabbitMqMessagePublishTopology configurator in _implementedMessageTypes)
                configurator.Apply(builder);
        }

        public override bool TryGetPublishAddress(Uri baseAddress, out Uri publishAddress)
        {
            var exchangeName = _messageTopology.EntityName;
            var exchangeType = ExchangeTypeSelector.GetExchangeType(exchangeName);

            var sendSettings = GetSendSettings(exchangeName, exchangeType);

            publishAddress = sendSettings.GetSendAddress(baseAddress);
            return true;
        }

        public SendSettings GetSendSettings()
        {
            var exchangeName = _messageTopology.EntityName;
            var exchangeType = ExchangeTypeSelector.GetExchangeType(exchangeName);

            return GetSendSettings(exchangeName, exchangeType);
        }

        ExchangeHandle ExchangeDeclare(IRabbitMqTopologyBuilder builder)
        {
            var exchangeName = _messageTopology.EntityName;
            var exchangeType = ExchangeTypeSelector.GetExchangeType(exchangeName);

            var temporary = TypeMetadataCache<TMessage>.IsTemporaryMessageType;

            var durable = !temporary;
            var autoDelete = temporary;

            var arguments = new Dictionary<string, object>();

            return builder.ExchangeDeclare(exchangeName, exchangeType, durable, autoDelete, arguments);
        }

        public SendSettings GetSendSettings(string exchangeName, string exchangeType, Action<IExchangeConfigurator> configure = null)
        {
            var isTemporary = TypeMetadataCache<TMessage>.IsTemporaryMessageType;

            var durable = !isTemporary;
            var autoDelete = isTemporary;

            var settings = new RabbitMqSendSettings(exchangeName, exchangeType, durable, autoDelete);

            configure?.Invoke(settings);

            return settings;
        }

        public void AddImplementedMessageConfigurator<T>(IRabbitMqMessagePublishTopologyConfigurator<T> configurator, bool direct)
            where T : class
        {
            var adapter = new ImplementedTypeAdapter<T>(configurator, direct);

            _implementedMessageTypes.Add(adapter);
        }


        class ImplementedTypeAdapter<T> :
            IRabbitMqMessagePublishTopology
            where T : class
        {
            readonly IRabbitMqMessagePublishTopologyConfigurator<T> _configurator;
            readonly bool _direct;

            public ImplementedTypeAdapter(IRabbitMqMessagePublishTopologyConfigurator<T> configurator, bool direct)
            {
                _configurator = configurator;
                _direct = direct;
            }

            public void Apply(IRabbitMqPublishTopologyBuilder builder)
            {
                if (_direct)
                {
                    var implementedBuilder = builder.CreateImplementedBuilder();

                    _configurator.Apply(implementedBuilder);
                }
            }
        }
    }
}