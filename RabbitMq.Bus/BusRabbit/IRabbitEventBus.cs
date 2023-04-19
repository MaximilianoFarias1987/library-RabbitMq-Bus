using RabbitMq.Bus.Commands;
using RabbitMq.Bus.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMq.Bus.BusRabbit
{
    public interface IRabbitEventBus
    {
        Task SendCommand<T>(T command) where T : Command;
        void Publish<T>(T evento) where T : Event;
        void Suscribe<T, TH>() where T : Event
                                where TH : IEventHandler<T>;
    }
}
