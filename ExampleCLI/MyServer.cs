using System;
using NamedPipeWrapper;

namespace ExampleCLI
{
    class MyServer
    {
        private bool KeepRunning
        {
            get
            {
                var key = Console.ReadKey();
                if (key.Key == ConsoleKey.Q)
                    return false;
                return true;
            }
        }

        public MyServer(string pipeName)
        {
            var server = new NamedPipeServer(pipeName);
            server.ClientConnected += OnClientConnected;
            server.ClientDisconnected += OnClientDisconnected;
            server.ClientMessage += OnClientMessage;
            server.Error += OnError;
            server.Start();
            while (KeepRunning)
            {
                // Do nothing - wait for user to press 'q' key
            }
            server.Stop();
        }

        private void OnClientConnected(NamedPipeConnection connection)
        {
            Console.WriteLine("Client {0} is now connected!", connection.Id);
            connection.PushMessage("Welcome!");
        }

        private void OnClientDisconnected(NamedPipeConnection connection)
        {
            Console.WriteLine("Client {0} disconnected", connection.Id);
        }

        private void OnClientMessage(NamedPipeConnection connection, string message)
        {
            Console.WriteLine("Client {0} says: {1}", connection.Id, message);
        }

        private void OnError(Exception exception)
        {
            Console.Error.WriteLine("ERROR: {0}", exception);
        }
    }
}