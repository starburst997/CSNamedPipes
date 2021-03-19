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
            // For testing create multiple clients (synchronously for simplicity, but async works too)
            for (var n = 0; n < 1; n++)
            {
                var t = new Thread(ThreadProc);
                t.Start(n);
            }

            // The call below stops the application from terminating
            // until we see all the responses displayed.
            Console.ReadLine();
        }

        public static void ThreadProc(object n)
        {
            var index = (int) n;
            var cli = new IpcClientPipe(".", "ExamplePipeName");

            PipeStream pipe;
            try
            {
                pipe = cli.Connect(10);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Connection failed: {e}");
                return;
            }
            
            Console.WriteLine($"Connected!");

            // Asynchronously send data to the server
            var message = $"Test request {index}";
            var output = Encoding.UTF8.GetBytes(message);
            Debug.Assert(output.Length < IpcServer.ServerInBufferSize);
            pipe.Write(output, 0, output.Length);

            // Read the result
            var data = new byte[IpcServer.ServerOutBufferSize];
            var bytesRead = pipe.Read(data, 0, data.Length);
            Console.WriteLine($"Server response: {Encoding.UTF8.GetString(data, 0, bytesRead)}");

            // Done with this one
            pipe.Close();
        }
    }
}