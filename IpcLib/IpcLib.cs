using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Threading;

// Ipc => Inter-process communications library
namespace IpcLib
{
    // Interface for user code to receive notifications regarding pipe messages
    // ReSharper disable once InconsistentNaming
    public interface IpcServerCallback
    {
        void OnAsyncConnect(PipeStream pipe, out object state);
        void OnAsyncDisconnect(PipeStream pipe, object state);
        void OnAsyncMessage(PipeStream pipe, byte[] data, int bytes, object state);
    }
    
    // ReSharper disable once InconsistentNaming
    public interface IpcClientCallback
    {
        void OnAsyncConnect(PipeStream pipe);
        void OnAsyncDisconnect(PipeStream pipe);
        void OnAsyncMessage(PipeStream pipe, byte[] data, int bytes);
    }

    // Internal data associated with pipes
    internal struct IpcPipeData
    {
        public PipeStream Pipe;
        public object State;
        public byte[] Data;
    }

    public class IpcServer
    {
        // TODO: parameterize so they can be passed by application
        public const int ServerInBufferSize = 4096;
        public const int ServerOutBufferSize = 4096;
        private readonly IpcServerCallback _callback;

        private readonly string _pipename;

        private IAsyncResult _awaitingClientConnection;
        private readonly Dictionary<PipeStream, IpcPipeData> _pipes = new();

        private bool _running;

        public IpcServer(string pipename, IpcServerCallback callback, int instances)
        {
            Debug.Assert(!_running);
            _running = true;

            // Save parameters for next new pipe
            _pipename = pipename;
            _callback = callback;

            // Start accepting connections
            for (var i = 0; i < instances; ++i)
                IpcServerPipeCreate();
        }

        public void IpcServerStop()
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
                ServerInBufferSize,
                ServerOutBufferSize
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
                    Pipe = pipe, 
                    State = null, 
                    Data = new byte[ServerInBufferSize]
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

                    // Alert server that client connection exists
                    _callback.OnAsyncConnect(pipe, out pd.State);

                    // Accept messages
                    BeginRead(pd);
                }
                else
                {
                    pipe.Close();
                }
            }
            catch (Exception ex)
            {
                //Exception reason: NamedPipeServerStream.close() is called when stopped the server. This causes OnClientConnected to be called. It then tries to acces a closed pipe with pipe.EndWaitForConnection
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
            _callback.OnAsyncDisconnect(pd.Pipe, pd.State);
                
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
                _callback.OnAsyncMessage(pd.Pipe, pd.Data, bytesRead, pd.State);
            
            BeginRead(pd);
        }

        public bool Send(string message)
        {
            if (_pipes.Count <= 0) 
                return false;
            
            lock (_pipes)
            {
                var output = Encoding.UTF8.GetBytes(message);
                Debug.Assert(output.Length < IpcServer.ServerInBufferSize);
                
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

    // TODO: Use invoke to makes sure it is called on same thread
    public class IpcClient
    {
        private readonly string _name;
        private readonly int _timeout;
        private readonly IpcClientCallback _callback;
        
        private IpcClientPipe _client;
        private PipeStream _pipe;
        private Thread _thread;

        private bool _connected;
        
        public IpcClient(string pipename, IpcClientCallback callback, int timeout = 10)
        {
            _name = pipename;
            _callback = callback;
            _timeout = timeout;
        }

        public void Connect()
        {
            _client = new IpcClientPipe(".", _name);
            
            try
            {
                _pipe = _client.Connect(_timeout);
            }
            catch (Exception e)
            {
                return;
            }
            
            _connected = true;
            
            // Create client pipe structure
            var pd = new IpcPipeData
            {
                Pipe = _pipe, 
                State = null, 
                Data = new byte[IpcServer.ServerOutBufferSize]
            };
            
            _callback.OnAsyncConnect(pd.Pipe);
            
            // Accept messages
            BeginRead(pd);
        }

        // Not sure if thread is absolutely necessary here, will need more tests
        public void ConnectAsync()
        {
            _thread = new Thread(Connect);
            _thread.Start();
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

            _connected = false;
            
            pd.Pipe.Close();
            _callback.OnAsyncDisconnect(pd.Pipe);
        }
        
        private void OnAsyncMessage(IAsyncResult result)
        {
            // Async read from client completed
            if (result.AsyncState == null) return;
            
            var pd = (IpcPipeData) result.AsyncState;
            var bytesRead = pd.Pipe.EndRead(result);
            if (bytesRead != 0)
                _callback.OnAsyncMessage(pd.Pipe, pd.Data, bytesRead);
            
            // Loop back
            BeginRead(pd);
        }

        public bool Send(string message)
        {
            if (!_connected) return false;
            
            try
            {
                var output = Encoding.UTF8.GetBytes(message);
                Debug.Assert(output.Length < IpcServer.ServerInBufferSize);
                
                _pipe.BeginWrite(output, 0, output.Length, OnAsyncWriteComplete, _pipe);
            }
            catch (Exception)
            {
                _pipe.Close();
            }

            return true;
        }
        
        private void OnAsyncWriteComplete(IAsyncResult result)
        {
            var pipe = (PipeStream) result.AsyncState;
            pipe?.EndWrite(result);
        }
        
        public void Stop()
        {
            if (!_connected) return;

            _connected = false;
            _pipe.Close();
        }
    }

    public class IpcClientPipe
    {
        private readonly NamedPipeClientStream _pipe;

        public IpcClientPipe(string serverName, string pipename)
        {
            _pipe = new NamedPipeClientStream(
                serverName,
                pipename,
                PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough
            );
        }

        public PipeStream Connect(int timeout)
        {
            // NOTE: will throw on failure
            _pipe.Connect(timeout);

            // Must Connect before setting ReadMode
            _pipe.ReadMode = PipeTransmissionMode.Message;

            return _pipe;
        }
    }
}