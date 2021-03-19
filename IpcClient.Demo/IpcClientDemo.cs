using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace IpcLib.Demo
{
    public static class DemoApp
    {
        public static void Main()
        {
            var client = new IpcClientDemo();
            
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

    public class IpcClientDemo : IpcClientCallback
    {
        private readonly IpcClient _client;

        public IpcClientDemo()
        {
            _client = new IpcClient("ExamplePipeName", this, 100);
            _client.Connect();
        }
        
        public void OnAsyncConnect(PipeStream pipe)
        {
            Console.WriteLine($"Connected");
        }

        public void OnAsyncDisconnect(PipeStream pipe)
        {
            Console.WriteLine($"Disconnected");
        }

        public void OnAsyncMessage(PipeStream pipe, byte[] data, int bytes)
        {
            var message = Encoding.UTF8.GetString(data, 0, bytes);
            
            Console.WriteLine($"Message Received: {message}");
        }

        public void Send(string message)
        {
            _client.Send(message);
            
            Console.WriteLine($"Message Sent: {message}");
        }
        
        public void Stop()
        {
            _client.Stop();
        }
    }
}