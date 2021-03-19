using System;
using System.Threading;
using IpcLib.Server;

namespace IpcLib.Demo
{
    public static class DemoApp
    {
        public static void Main(string[] args)
        {
            var server = new IpcServer("ExamplePipeName");
            
            server.Connected += id => Console.WriteLine($"Connected {id}");
            server.Disconnected += id => Console.WriteLine($"Disconnected ({id})");
            server.Message += (id, message) => Console.WriteLine($"Message Received ({id}): {message}");
            
            server.Connect();
            
            Thread.Sleep(5000);

            Console.WriteLine("Sending tests to clients");
            
            Thread.Sleep(1000);

            server.Send("Server 1");
            
            Thread.Sleep(1000);

            server.Send("Server 2");
            
            Thread.Sleep(1000);

            server.Send("Server 3");
            
            Thread.Sleep(1000);

            server.Send("Server 4");
            
            Thread.Sleep(1000);
            
            server.Stop();
            
            Console.WriteLine("End");
        }
    }
}