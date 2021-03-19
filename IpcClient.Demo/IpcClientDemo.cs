using System;
using System.Threading;
using IpcLib.Client;

namespace IpcLib.Demo
{
    public static class DemoApp
    {
        public static void Main()
        {
            var client = new IpcClient("ExamplePipeName");
            
            client.Connected += () => Console.WriteLine("Connected");
            client.Disconnected += () => Console.WriteLine("Disconnected");
            client.Message += message => Console.WriteLine($"Message Received: {message}");
            
            client.Connect();
            
            Thread.Sleep(1000);

            client.Send("Client 1");
            
            Thread.Sleep(1000);
            
            client.Send("Client 2");
            
            Thread.Sleep(1000);
            
            client.Send("Client 3");
            
            Thread.Sleep(10000);
            
            client.Stop();
            
            Console.WriteLine("End");
        }
    }
}