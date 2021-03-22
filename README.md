# CSNamedPipes

CSNamedPipes is a demo application that implements interprocess communication (IPC) using Named Pipes in C#.

## JD's Fork

A fork of [Patrick Wyatt](https://www.codeofhonor.com/blog/) - [CSNamedPipes](https://github.com/webcoyote/CSNamedPipes), the original code didn't include an obvious way of doing bi-directional communication between two processes. 

Both the Client and Server will wait for messages and can send message to each other asynchronously until `Stop()` is called.

Not sure if I would've been better with Socket instead, I failed using `WM_COPYDATA` at first and stumbled upon this which seems pretty lightweight (I only need to send a few messages between a [Unity App](https://github.com/starburst997/Unity.IPC) and a Windows App for [Notessimo](https://notessimo.net)).

## Server

```c#
var server = new IpcServer("ExamplePipeName");
            
server.Connected += id => Console.WriteLine($"Connected {id}");
server.Disconnected += id => Console.WriteLine($"Disconnected ({id})");
server.Message += (id, message) => Console.WriteLine($"Message Received ({id}): {message}");

server.Connect();

Thread.Sleep(5000);

server.Send("Server 1");

Thread.Sleep(1000);

server.Stop();

Console.WriteLine("End");
```

## Client

```c#
var client = new IpcClient("ExamplePipeName");
            
client.Connected += () => Console.WriteLine("Connected");
client.Disconnected += () => Console.WriteLine("Disconnected");
client.Message += message => Console.WriteLine($"Message Received: {message}");

client.Connect();

Thread.Sleep(1000);

client.Send("Client 1");

Thread.Sleep(10000);

client.Stop();

Console.WriteLine("End");
```

## Why create this? What problems does it solve?

I needed a library to implement interprocess communication so that I could write a desktop application communicate with a Windows Service application. I thought I'd find some simple code on the Internet, but two things were missing:

1. Most of the code samples I ran across used synchronous (blocking) communication, which requires one thread per named pipe. My background is writing massive-scale Internet services like battle.net, and online games like Starcraft and Guild Wars, which would totally fall over using synchronous sockets/pipes. So async it is!

2. Google for "How to detect a client disconnect using a named pipe" and you'll get 430000 hits. I wanted to make sure my program solved this problem.

For more details about the solutions to these problems, you can read the code or check out my blog article [Detect client disconnects using named-pipes in C#](http://www.codeofhonor.com/blog/detect-client-disconnects-using-named-pipes-in-csharp).

## Comments

I am *glad* to answer questions about this project.

## License

MIT License, which basically means you can do whatever you want with the code (even use it commercially with no fee) but don't blame me if something bad happens.
