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
            // Run as a Windows console application
            var server = new DemoIpcServer();
            server.Start();

            // Since all the requests are issued asynchronously, the constructors are likely to return
            // before all the requests are complete. The call below stops the application from terminating
            // until we see all the responses displayed.
            Thread.Sleep(1000);
            Console.WriteLine("\nPress return to shutdown server");
            Console.ReadLine();

            server.Stop();

            Console.WriteLine("\nComplete! Press return to exit program");
            Console.ReadLine();
        }
    }

    public class DemoIpcServer : IpcCallback
    {
        private int _count;
        private IpcServer _srv;

        public void OnAsyncConnect(PipeStream pipe, out object state)
        {
            var count = Interlocked.Increment(ref _count);
            Console.WriteLine($"Connected: {count}");
            
            state = count;
        }

        public void OnAsyncDisconnect(PipeStream pipe, object state)
        {
            Console.WriteLine($"Disconnected: {(int) state}");
        }

        public void OnAsyncMessage(PipeStream pipe, byte[] data, int bytes, object state)
        {
            Console.WriteLine($"Message: {(int) state} bytes: {bytes}");
            
            data = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(data, 0, bytes).ToUpper().ToCharArray());

            // Write results
            try
            {
                pipe.BeginWrite(data, 0, bytes, OnAsyncWriteComplete, pipe);
            }
            catch (Exception)
            {
                pipe.Close();
            }
        }

        public void Start()
        {
            _srv = new IpcServer("ExamplePipeName", this, 1);
        }

        public void Stop()
        {
            _srv.IpcServerStop();
        }

        private void OnAsyncWriteComplete(IAsyncResult result)
        {
            var pipe = (PipeStream) result.AsyncState;
            pipe?.EndWrite(result);
        }
    }
}