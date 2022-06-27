﻿using System;
using System.Threading.Channels;
using System.Threading.Tasks;

using RabbitMQ.Client.Impl;

namespace RabbitMQ.Client.ConsumerDispatching
{
#nullable enable
    internal abstract class ConsumerDispatcherChannelBase : ConsumerDispatcherBase, IConsumerDispatcher
    {
        protected readonly ModelBase _model;
        protected readonly ChannelReader<WorkStruct> _reader;
        private readonly ChannelWriter<WorkStruct> _writer;
        private readonly Task _worker;

        public bool IsShutdown { get; private set; }

        protected ConsumerDispatcherChannelBase(ModelBase model, int concurrency)
        {
            _model = model;
            var channel = Channel.CreateUnbounded<WorkStruct>(new UnboundedChannelOptions
            {
                SingleReader = concurrency == 1,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
            _reader = channel.Reader;
            _writer = channel.Writer;

            Func<Task> loopStart = ProcessChannelAsync;
            if (concurrency == 1)
            {
                _worker = Task.Run(loopStart);
            }
            else
            {
                var tasks = new Task[concurrency];
                for (int i = 0; i < concurrency; i++)
                {
                    tasks[i] = Task.Run(loopStart);
                }
                _worker = Task.WhenAll(tasks);
            }
        }

        public void HandleBasicConsumeOk(IBasicConsumer consumer, string consumerTag)
        {
            if (!IsShutdown)
            {
                AddConsumer(consumer, consumerTag);
                _writer.TryWrite(new WorkStruct(WorkType.ConsumeOk, consumer, consumerTag));
            }
        }

        public void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered,
            in CachedString exchange, in CachedString routingKey, in ReadOnlyBasicProperties basicProperties, ReadOnlyMemory<byte> body, byte[] rentedMethodArray, byte[] rentedArray)
        {
            if (!IsShutdown)
            {
                _writer.TryWrite(new WorkStruct(GetConsumerOrDefault(consumerTag), consumerTag, deliveryTag, redelivered, in exchange, in routingKey, basicProperties, body, rentedMethodArray, rentedArray));
            }
        }

        public void HandleBasicCancelOk(string consumerTag)
        {
            if (!IsShutdown)
            {
                _writer.TryWrite(new WorkStruct(WorkType.CancelOk, GetAndRemoveConsumer(consumerTag), consumerTag));
            }
        }

        public void HandleBasicCancel(string consumerTag)
        {
            if (!IsShutdown)
            {
                _writer.TryWrite(new WorkStruct(WorkType.Cancel, GetAndRemoveConsumer(consumerTag), consumerTag));
            }
        }

        protected sealed override void ShutdownConsumer(IBasicConsumer consumer, ShutdownEventArgs reason)
        {
            _writer.TryWrite(new WorkStruct(consumer, reason));
        }

        public void Quiesce()
        {
            IsShutdown = true;
        }

        protected override Task InternalShutdownAsync()
        {
            _writer.Complete();
            return _worker;
        }

        public Task WaitForShutdownAsync()
        {
            return _worker;
        }

        protected abstract Task ProcessChannelAsync();

        protected readonly struct WorkStruct
        {
            public readonly IBasicConsumer Consumer;
            public IAsyncBasicConsumer AsyncConsumer => (IAsyncBasicConsumer)Consumer;
            public readonly string? ConsumerTag;
            public readonly ulong DeliveryTag;
            public readonly bool Redelivered;
            public readonly CachedString Exchange;
            public readonly CachedString RoutingKey;
            public readonly ReadOnlyBasicProperties BasicProperties;
            public readonly ReadOnlyMemory<byte> Body;
            public readonly byte[]? RentedMethodArray;
            public readonly byte[]? RentedArray;
            public readonly ShutdownEventArgs? Reason;
            public readonly WorkType WorkType;

            public WorkStruct(WorkType type, IBasicConsumer consumer, string consumerTag)
                : this()
            {
                WorkType = type;
                Consumer = consumer;
                ConsumerTag = consumerTag;
            }

            public WorkStruct(IBasicConsumer consumer, ShutdownEventArgs reason)
                : this()
            {
                WorkType = WorkType.Shutdown;
                Consumer = consumer;
                Reason = reason;
            }

            public WorkStruct(IBasicConsumer consumer, string consumerTag, ulong deliveryTag, bool redelivered,
                in CachedString exchange, in CachedString routingKey, in ReadOnlyBasicProperties basicProperties, ReadOnlyMemory<byte> body, byte[] rentedMethodArray, byte[] rentedArray)
            {
                WorkType = WorkType.Deliver;
                Consumer = consumer;
                ConsumerTag = consumerTag;
                DeliveryTag = deliveryTag;
                Redelivered = redelivered;
                Exchange = exchange;
                RoutingKey = routingKey;
                BasicProperties = basicProperties;
                Body = body;
                RentedMethodArray = rentedMethodArray;
                RentedArray = rentedArray;
                Reason = default;
            }
        }

        protected enum WorkType : byte
        {
            Shutdown,
            Cancel,
            CancelOk,
            Deliver,
            ConsumeOk
        }
    }
}
