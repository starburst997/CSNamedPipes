using System;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace IpcLib.Demo
{
    public static class DemoApp
    {
        public static void Main(string[] args)
        {
            var server = new IpcServerDemo();
            
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

    public class IpcServerDemo : IpcServerCallback
    {
        private readonly IpcServer _server;
        
        private int _count;

        public IpcServerDemo()
        {
            _server = new IpcServer("ExamplePipeName", this, 1);
        }
        
        public void OnAsyncConnect(PipeStream pipe, out object state)
        {
            var count = Interlocked.Increment(ref _count);
            state = count;
            
            Console.WriteLine($"Connected: {count}");
        }

        public void OnAsyncDisconnect(PipeStream pipe, object state)
        {
            Console.WriteLine($"Disconnected: {(int) state}");
        }

        public void OnAsyncMessage(PipeStream pipe, byte[] data, int bytes, object state)
        {
            var message = Encoding.UTF8.GetString(data, 0, bytes);
            
            Console.WriteLine($"Message Received: {message}");
        }

        public void Stop()
        {
            _server.IpcServerStop();
        }
        
        public void Send(string message)
        {
            _server.Send(message);
            
            Console.WriteLine($"Message Sent: {message}");
        }
    }
}