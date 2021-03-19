using System.IO.Pipes;

namespace IpcLib.Server
{
    // ReSharper disable once InconsistentNaming
    public interface IpcServerCallback
    {
        void OnAsyncConnect(PipeStream pipe, out object state);
        void OnAsyncDisconnect(PipeStream pipe, object state);
        void OnAsyncMessage(PipeStream pipe, byte[] data, int bytes, object state);
    }
}