using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace IpcLib
{
    // ReSharper disable once InconsistentNaming
    public interface IpcClientCallback
    {
        void OnAsyncConnect(PipeStream pipe);
        void OnAsyncDisconnect(PipeStream pipe);
        void OnAsyncMessage(PipeStream pipe, byte[] data, int bytes);
    }
    
    public class IpcClient
    {
        private struct IpcPipeData
        {
            public PipeStream Pipe;
            public object State;
            public byte[] Data;
        }
        
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
            catch (Exception)
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