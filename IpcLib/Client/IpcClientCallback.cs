using System.IO.Pipes;

namespace IpcLib.Client
{
    // ReSharper disable once InconsistentNaming
    public interface IpcClientCallback
    {
        void OnAsyncConnect(PipeStream pipe);
        void OnAsyncDisconnect(PipeStream pipe);
        void OnAsyncMessage(PipeStream pipe, byte[] data, int bytes);
    }
}