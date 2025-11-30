using System;
using System.Threading;
using Apache.NMS;
namespace NotifServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Notif service ");
            IConnectionFactory factory = new NMSConnectionFactory("stomp:tcp://localhost:61613");
            try
            {
                using (IConnection connection = factory.CreateConnection())
                {
                    connection.Start();
                    using (ISession session = connection.CreateSession())
                    {
                        IDestination destination = session.GetTopic("BikeNotif");
                        using (IMessageProducer producer = session.CreateProducer(destination))
                        {
                            producer.DeliveryMode = MsgDeliveryMode.NonPersistent;
                            var random = new Random();
                            string[] types = { "Information Météo", "Information Pollution", "Information Trafic" };
                            string[] levels = { "Excellent", "Bon", "Mauvais" };
                            while (true)
                            {
                                string type = types[random.Next(types.Length)];
                                string level = levels[random.Next(levels.Length)];
                                string messageText = $"{type}: Niveau {level} actuellement.";
                                string json = $"{{\"type\": \"{type}\", \"level\": \"{level}\", \"message\": \"{messageText}\"}}";
                                ITextMessage message = session.CreateTextMessage(json);
                                producer.Send(message);
                                Console.WriteLine($"[STOMP] Envoyé : {json}");
                                Thread.Sleep(10000);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERREUR : {ex.Message}");
            }
        }
    }
}