using System.IO.Pipes;

namespace IpcLib.Client
{
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