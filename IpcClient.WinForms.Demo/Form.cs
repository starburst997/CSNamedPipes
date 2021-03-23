using System;
using IpcLib.Client;

namespace IpcClientWinFormsDemo
{
    public partial class Form : System.Windows.Forms.Form
    {
        private IpcClient _client;
        
        private delegate void ConnectedDelegate();
        private delegate void DisconnectedDelegate();
        private delegate void MessageDelegate(string data);

        private ConnectedDelegate _connected;
        private DisconnectedDelegate _disconnected;
        private MessageDelegate _message;
        
        public Form()
        {
            InitializeComponent();
        }

        private void OnLoad(object? sender, EventArgs e)
        {
            _connected = Connected;
            _disconnected = Disconnected;
            _message = Message;
            
            _client = new IpcClient("ipc-winforms");
            _client.Connected += ConnectedAsync;
            _client.Disconnected += DisconnectedAsync;
            _client.Message += MessageAsync;
        }

        private void MessageAsync(string message)
        {
            Invoke(_message, message);
        }

        private void DisconnectedAsync()
        {
            Invoke(_disconnected);
        }

        private void ConnectedAsync()
        {
            Invoke(_connected);
        }

        private void Log(string text)
        {
            textBox1.AppendText($"{text}{Environment.NewLine}");
        }
        
        private void Message(string message)
        {
            Log($"Message: {message}");
        }

        private void Disconnected()
        {
            Log($"Disconnected");
        }

        private void Connected()
        {
            Log($"Connected");
        }

        private void ConnectOnClick(object? sender, EventArgs e)
        {
            _client.Connect();
        }

        private void SendOnClick(object? sender, EventArgs e)
        {
            _client.Send("Hola from Client");
        }
        
        private void StopOnClick(object? sender, EventArgs e)
        {
            _client.Stop();
        }
    }
}