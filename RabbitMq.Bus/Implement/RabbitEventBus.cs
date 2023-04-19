using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using RabbitMq.Bus.BusRabbit;
using RabbitMq.Bus.Commands;
using RabbitMq.Bus.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMq.Bus.Implement
{
    public class RabbitEventBus : IRabbitEventBus
    {
        private readonly IMediator _mediator;
        private readonly Dictionary<string, List<Type>> _handlers;
        private readonly List<Type> _eventTypes;

        private readonly IServiceScopeFactory _serviceScopeFactory;

        public RabbitEventBus(IMediator mediator, IServiceScopeFactory serviceScopeFactory)
        {
            _mediator = mediator;
            _handlers = new Dictionary<string, List<Type>>();
            _eventTypes = new List<Type>();
            _serviceScopeFactory = serviceScopeFactory;
        }


        public void Publish<T>(T evento) where T : Event
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                var eventName = evento.GetType().Name;

                channel.QueueDeclare(eventName, false, false, false, null);

                var message = JsonConvert.SerializeObject(evento);
                var body = Encoding.UTF8.GetBytes(message);
                channel.BasicPublish("", eventName, null, body);
            }
        }

        public Task SendCommand<T>(T command) where T : Command
        {
            return _mediator.Send(command);
        }

        public void Suscribe<T, TH>()
            where T : Event
            where TH : IEventHandler<T>
        {
            var eventName = typeof(T).Name;
            var handlerEventType = typeof(TH);

            if (!_eventTypes.Contains(typeof(T)))
            {
                _eventTypes.Add(typeof(T));
            }

            if (!_handlers.ContainsKey(eventName))
            {
                _handlers.Add(eventName, new List<Type>());
            }

            if (_handlers[eventName].Any(x => x.GetType() == handlerEventType))
            {
                throw new ArgumentException($"El manejador {handlerEventType.Name} fue registrado anteriormente por {eventName}");
            }

            _handlers[eventName].Add(handlerEventType);

            var factory = new ConnectionFactory()
            {
                HostName = "localhost",
                DispatchConsumersAsync = true,
            };

            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();

            channel.QueueDeclare(eventName, false, false, false, null);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += Consumer_Delegate;

            channel.BasicConsume(eventName, true, consumer);
        }

        private async Task Consumer_Delegate(object sender, BasicDeliverEventArgs e)
        {
            var eventName = e.RoutingKey;
            var message = Encoding.UTF8.GetString(e.Body.ToArray());

            try
            {
                if (_handlers.ContainsKey(eventName))
                {
                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var subscriptions = _handlers[eventName];
                        foreach (var sb in subscriptions)
                        {
                            var handler = scope.ServiceProvider.GetService(sb);
                            if (handler == null) continue;

                            var eventType = _eventTypes.SingleOrDefault(x => x.Name == eventName);

                            var eventDS = JsonConvert.DeserializeObject(message, eventType);

                            var concreteType = typeof(IEventHandler<>).MakeGenericType(eventType);

                            await (Task)concreteType.GetMethod("Handle").Invoke(handler, new object[] { eventDS });
                        }
                    }
                    
                }
            }
            catch (Exception ex)
            {

                throw;
            }
        }
    }
}
