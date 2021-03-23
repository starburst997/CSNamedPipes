using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace IpcServerWinFormsDemo
{
    public partial class Form : System.Windows.Forms.Form
    {
        private IpcLib.Server.IpcServer _server;
        
        private delegate void ConnectedDelegate(int id);
        private delegate void DisconnectedDelegate(int id);
        private delegate void MessageDelegate(int id, string data);

        private ConnectedDelegate _connected;
        private DisconnectedDelegate _disconnected;
        private MessageDelegate _message;
        
        public Form()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            _connected = Connected;
            _disconnected = Disconnected;
            _message = Message;
            
            _server = new IpcLib.Server.IpcServer("ipc-winforms");
            _server.Connected += ConnectedAsync;
            _server.Disconnected += DisconnectedAsync;
            _server.Message += MessageAsync;
        }

        private void MessageAsync(int id, string message)
        {
            Invoke(_message, id, message);
        }

        private void DisconnectedAsync(int id)
        {
            Invoke(_disconnected, id);
        }

        private void ConnectedAsync(int id)
        {
            Invoke(_connected, id);
            _server.Send("ready");
        }
        
        private void Log(string text)
        {
            textBox1.AppendText($"{text}{Environment.NewLine}");
        }
        
        private void Message(int id, string message)
        {
            Log($"Message ({id}): {message}");
        }

        private void Disconnected(int id)
        {
            Log($"Client Disconnected ({id})");
        }

        private void Connected(int id)
        {
            Log($"Client Connected ({id})");
        }

        private void SendOnClick(object? sender, EventArgs e)
        {
            _server.Send("Hello from Server");
        }

        private void ConnectOnClick(object? sender, EventArgs e)
        {
            _server.Connect();
        }
        
        private void StopOnClick(object? sender, EventArgs e)
        {
            _server.Stop();
        }
    }
}