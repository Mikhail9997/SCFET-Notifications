namespace Application.Common.Interfaces;

public interface IKafkaProducer
{
    Task ProduceAsync(string topic, string message);
}