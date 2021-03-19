using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace IpcLib.Server
{
    public class IpcServer
    {
        private readonly string _pipename;

        private IAsyncResult _awaitingClientConnection;
        private readonly Dictionary<PipeStream, IpcPipeData> _pipes = new();

        private bool _running;

        private int _id;
        private readonly int _instances;
        
        // Events
        public Action<int, string> Message;
        public Action<int> Connected;
        public Action<int> Disconnected;

        public IpcServer(string pipename, int instances = 1)
        {
            _running = true;

            // Save parameters for next new pipe
            _pipename = pipename;
            _instances = instances;
        }

        public void Connect()
        {
            // Start accepting connections
            for (var i = 0; i < _instances; ++i)
                IpcServerPipeCreate();
        }
        
        public void Stop()
        {
            // Close all pipes asynchronously
            lock (_pipes)
            {
                _running = false;
                foreach (var pipe in _pipes.Keys)
                    pipe.Close();
            }

            // Wait for all pipes to close
            while (true)
            {
                int count;
                lock (_pipes)
                {
                    count = _pipes.Count;
                }

                if (count == 0)
                    break;

                Thread.Sleep(5);
            }

            if (!_awaitingClientConnection.IsCompleted)
            {
                ((NamedPipeServerStream) _awaitingClientConnection.AsyncState)?.Close();
                //((NamedPipeServerStream)AwaitingClientConnection.AsyncState).EndWaitForConnection(AwaitingClientConnection); THIS WILL WAIT FOREVER. Don't use
            }
        }

        private void IpcServerPipeCreate()
        {
            // Create message-mode pipe to simplify message transition
            // Assume all messages will be smaller than the pipe buffer sizes
            var pipe = new NamedPipeServerStream(
                _pipename,
                PipeDirection.InOut,
                -1,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough,
                IpcLib.ServerInBufferSize,
                IpcLib.ServerOutBufferSize
            );

            // Asynchronously accept a client connection
            _awaitingClientConnection = pipe.BeginWaitForConnection(OnClientConnected, pipe);
        }

        private void OnClientConnected(IAsyncResult result)
        {
            try
            {
                // Complete the client connection
                var pipe = (NamedPipeServerStream) result.AsyncState;
                if (pipe == null) return;
                pipe.EndWaitForConnection(result);

                // Create client pipe structure
                var pd = new IpcPipeData
                {
                    Id = ++_id,
                    Pipe = pipe,
                    State = null,
                    Data = new byte[IpcLib.ServerInBufferSize]
                };

                // Add connection to connection list
                bool running;
                lock (_pipes)
                {
                    running = _running;
                    if (running)
                        _pipes.Add(pd.Pipe, pd);
                }

                // If server is still running
                if (running)
                {
                    // Prepare for next connection
                    IpcServerPipeCreate();

                    Connected?.Invoke(pd.Id);

                    // Accept messages
                    BeginRead(pd);
                }
                else
                {
                    pipe.Close();
                }
            }
            catch (Exception)
            {
                //Exception reason: NamedPipeServerStream.close() is called when stopped the server. This causes OnClientConnected to be called. It then tries to access a closed pipe with pipe.EndWaitForConnection
            }
        }

        private void BeginRead(IpcPipeData pd)
        {
            // Asynchronously read a request from the client
            var isConnected = pd.Pipe.IsConnected;
            if (isConnected)
            {   
                try
                {
                    pd.Pipe.BeginRead(pd.Data, 0, pd.Data.Length, OnAsyncMessage, pd);
                }
                catch (Exception)
                {
                    isConnected = false;
                }
            }

            if (isConnected) return;
            
            pd.Pipe.Close();
            
            Disconnected?.Invoke(pd.Id);
                
            lock (_pipes)
            {
                var removed = _pipes.Remove(pd.Pipe);
                Debug.Assert(removed);
            }
        }

        private void OnAsyncMessage(IAsyncResult result)
        {
            // Async read from client completed
            if (result.AsyncState == null) return;
            
            var pd = (IpcPipeData) result.AsyncState;
            var bytesRead = pd.Pipe.EndRead(result);
            if (bytesRead != 0)
            {
                var message = Encoding.UTF8.GetString(pd.Data, 0, bytesRead);
                Message?.Invoke(pd.Id, message);
            }
            
            BeginRead(pd);
        }

        public bool Send(string message)
        {
            if (_pipes.Count <= 0) 
                return false;
            
            lock (_pipes)
            {
                var output = Encoding.UTF8.GetBytes(message);
                Debug.Assert(output.Length < IpcLib.ServerInBufferSize);
                
                foreach (var pipe in _pipes)
                {
                    try
                    {
                        pipe.Value.Pipe.BeginWrite(output, 0, output.Length, OnAsyncWriteComplete, pipe.Value.Pipe);
                    }
                    catch (Exception)
                    {
                        pipe.Value.Pipe.Close();
                    }
                }
            }
            
            return true;
        }
        
        private void OnAsyncWriteComplete(IAsyncResult result)
        {
            var pipe = (PipeStream) result.AsyncState;
            pipe?.EndWrite(result);
        }
    }
}