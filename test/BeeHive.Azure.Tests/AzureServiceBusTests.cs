﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace BeeHive.Azure.Tests
{
    public class AzureServiceBusTests
    {

        private const string ConnectionString =
            "Endpoint=sb://beehivetest.servicebus.windows.net/;SharedSecretIssuer=owner;SharedSecretValue=FIFK7e6S7TgDX2q2zz+QYpNybFYaaCdZpKfKJLKb42c=";

        [Fact]
        public void TopicCreateAndExists()
        {
            var topicName = Guid.NewGuid().ToString("N");
            var queueName = QueueName.FromTopicName(topicName);
            var serviceBusOperator = new ServiceBusOperator(ConnectionString);
            Assert.False(serviceBusOperator.QueueExistsAsync(queueName).Result);

            serviceBusOperator.CreateQueueAsync(queueName).Wait();
            Assert.True(serviceBusOperator.QueueExistsAsync(queueName).Result);

            serviceBusOperator.DeleteQueueAsync(queueName).Wait();
            Assert.False(serviceBusOperator.QueueExistsAsync(queueName).Result);

        }

        [Fact]
        public void TopicAndSubscriptionCreateAndExists()
        {
            var topicName = Guid.NewGuid().ToString("N");
            var subName = Guid.NewGuid().ToString("N");
            var topicQueueName = QueueName.FromTopicName(topicName);
            var queueName = QueueName.FromTopicAndSubscriptionName(topicName, subName);
            var serviceBusOperator = new ServiceBusOperator(ConnectionString);


            Assert.False(serviceBusOperator.QueueExistsAsync(queueName).Result);

            serviceBusOperator.CreateQueueAsync(topicQueueName).Wait();
            serviceBusOperator.CreateQueueAsync(queueName).Wait();
            Assert.True(serviceBusOperator.QueueExistsAsync(queueName).Result);

            serviceBusOperator.DeleteQueueAsync(queueName).Wait();
            serviceBusOperator.DeleteQueueAsync(topicQueueName).Wait();
            Assert.False(serviceBusOperator.QueueExistsAsync(queueName).Result);


        }

        [Fact]
        public void TopicAndSubscriptionCreateAndSent()
        {
            var topicName = Guid.NewGuid().ToString("N");
            var subName = Guid.NewGuid().ToString("N");
            var topicQueueName = QueueName.FromTopicName(topicName);
            var queueName = QueueName.FromTopicAndSubscriptionName(topicName, subName);
            var serviceBusOperator = new ServiceBusOperator(ConnectionString);


            serviceBusOperator.CreateQueueAsync(topicQueueName).Wait();
            serviceBusOperator.CreateQueueAsync(queueName).Wait();

            serviceBusOperator.PushAsync(new Event("chashm")
            {
               QueueName = topicQueueName.ToString()
            }).Wait();

            serviceBusOperator.DeleteQueueAsync(queueName).Wait();
            serviceBusOperator.DeleteQueueAsync(topicQueueName).Wait();


        }

        [Fact]
        public void TopicAndSubscriptionCreateAndSentAndReceivedForOneMinute()
        {
            var topicName = Guid.NewGuid().ToString("N");
            var subName = Guid.NewGuid().ToString("N");
            var topicQueueName = QueueName.FromTopicName(topicName);
            var queueName = QueueName.FromTopicAndSubscriptionName(topicName, subName);
            var serviceBusOperator = new ServiceBusOperator(ConnectionString);


            serviceBusOperator.CreateQueueAsync(topicQueueName).Wait();
            serviceBusOperator.CreateQueueAsync(queueName).Wait();

            serviceBusOperator.PushAsync(new Event("chashm")
            {
                QueueName = topicQueueName.ToString()
            }).Wait();

            var pollerResult = serviceBusOperator.NextAsync(queueName).Result;
            var cancellationTokenSource = new CancellationTokenSource();
            serviceBusOperator.KeepExtendingLeaseAsync(pollerResult.PollingResult, TimeSpan.FromSeconds(10),
                cancellationTokenSource.Token);

            Thread.Sleep(35000);
            cancellationTokenSource.Cancel();
            serviceBusOperator.CommitAsync(pollerResult.PollingResult);

            serviceBusOperator.DeleteQueueAsync(queueName).Wait();
            serviceBusOperator.DeleteQueueAsync(topicQueueName).Wait();


        }

        [Fact]
        public void TopicAndSubscriptionCreateAndSentBatch()
        {
            var topicName = Guid.NewGuid().ToString("N");
            var subName = Guid.NewGuid().ToString("N");
            var topicQueueName = QueueName.FromTopicName(topicName);
            var queueName = QueueName.FromTopicAndSubscriptionName(topicName, subName);
            var serviceBusOperator = new ServiceBusOperator(ConnectionString);


            serviceBusOperator.CreateQueueAsync(topicQueueName).Wait();
            serviceBusOperator.CreateQueueAsync(queueName).Wait();

            serviceBusOperator.PushBatchAsync(new[]{ new Event("chashm")
            {
                QueueName = topicQueueName.ToString()
            },
            new Event("chashm")
            {
                QueueName = topicQueueName.ToString()
            }
            }).Wait();

            serviceBusOperator.DeleteQueueAsync(queueName).Wait();
            serviceBusOperator.DeleteQueueAsync(topicQueueName).Wait();


        }

        [Fact]
        public void NeverEverCreatesAMessageBiggerThan256KB()
        {
            const int BufferSize = 10*1024;
            const int NumberOfMessages = 1000;
            var random = new Random();
            var list = new List<Event>();
            for (int i = 0; i < NumberOfMessages; i++)
            {
                var fatMessage = new FatMessage() {ALotOfBytes = new byte[BufferSize + random.Next(10000)]};
                random.NextBytes(fatMessage.ALotOfBytes);
                list.Add(new Event(fatMessage));
            }

            var batchUp = ServiceBusOperator.BatchUp(list);
            foreach (var batch in batchUp)
            {
                var sum = batch.Sum(x => x.Size);
                Assert.True(sum < 256 * 1024, "Size BIg!" );
                Console.WriteLine("Size => {0}", sum);
            }

            Assert.Equal(NumberOfMessages, batchUp.Sum(x => x.Count));
        }

        class FatMessage
        {
            public byte[] ALotOfBytes { get; set; }
        }
    }
}
