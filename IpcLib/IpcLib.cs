using System.IO.Pipes;

namespace IpcLib
{
    internal struct IpcPipeData
    {
        public PipeStream Pipe;
        public object State;
        public byte[] Data;
    }
    
    public static class IpcLib
    {
        public const int ServerInBufferSize = 4096;
        public const int ServerOutBufferSize = 4096;
    }
}