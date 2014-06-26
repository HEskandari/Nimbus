using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;
using Nimbus.Extensions;

namespace Nimbus.Infrastructure.MessageSendersAndReceivers
{
    internal abstract class BatchingMessageSender : INimbusMessageSender
    {
        private readonly ILogger _logger;
        private readonly List<BrokeredMessage> _outboundQueue = new List<BrokeredMessage>();
        private readonly object _sendingMutex = new object();
        private bool _flushing;
        private bool _disposed;

        protected BatchingMessageSender(ILogger logger)
        {
            _logger = logger;
        }

        protected abstract void SendBatch(BrokeredMessage[] messages);

        public Task Send(BrokeredMessage message)
        {
            return Task.Run(() =>
                            {
                                lock (_outboundQueue)
                                {
                                    _outboundQueue.Add(message);
                                }
                                TriggerMessageFlush();
                            });
        }

        private void TriggerMessageFlush()
        {
            Task.Run(() => FlushMessages());
        }

        private void FlushMessages()
        {
            if (_flushing) return;
            if (_disposed) return;

            lock (_sendingMutex)
            {
                try
                {
                    _flushing = true;
                    BrokeredMessage[] toSend;

                    lock (_outboundQueue)
                    {
                        toSend = _outboundQueue.Take(100).ToArray();
                        _outboundQueue.RemoveRange(0, toSend.Length);
                    }

                    if (toSend.None()) return;

                    try
                    {
                        SendBatch(toSend);
                    }
                    catch (Exception ex)
                    {
                        if (ex.IsTransientFault())
                        {
                            _logger.Warn("Going to retry after {0} was thrown sending batch: {1}", ex.GetType().Name, ex.Message);
                            lock (_outboundQueue)
                            {
                                _outboundQueue.AddRange(toSend);
                            }
                        }
                        else
                        {
                            // Until recycling is implemented we have no option but to fail fast
                            _logger.Error(ex, "FATAL: The BatchingMessageSender has failed and cannot be recovered: {0}", ex.Message);
                            Environment.FailFast("The BatchingMessageSender has failed and cannot be recovered: {0}".FormatWith(ex.Message), ex);
                        }
                    }
                }
                finally
                {
                    _flushing = false;
                }

                lock (_outboundQueue)
                {
                    if (_outboundQueue.Any()) TriggerMessageFlush();
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            _disposed = true;
        }
    }
}