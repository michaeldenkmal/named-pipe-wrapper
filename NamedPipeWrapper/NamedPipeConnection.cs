using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using NamedPipeWrapper.IO;
using NamedPipeWrapper.Threading;
using System.Collections.Concurrent;

namespace NamedPipeWrapper
{
    /// <summary>
    /// Represents a connection between a named pipe client and server.
    /// </summary>
    public class NamedPipeConnection
    {
        /// <summary>
        /// Gets the connection's unique identifier.
        /// </summary>
        public readonly int Id;

        /// <summary>
        /// Gets the connection's name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets a value indicating whether the pipe is connected or not.
        /// </summary>
        public bool IsConnected { get { return _streamWrapper.IsConnected; } }

        /// <summary>
        /// Invoked when the named pipe connection terminates.
        /// </summary>
        public event ConnectionEventHandler Disconnected;

        /// <summary>
        /// Invoked whenever a message is received from the other end of the pipe.
        /// </summary>
        public event ConnectionMessageEventHandler ReceiveMessage;

        /// <summary>
        /// Invoked when an exception is thrown during any read/write operation over the named pipe.
        /// </summary>
        public event ConnectionExceptionEventHandler Error;

        private readonly PipeStreamWrapper _streamWrapper;

        private readonly AutoResetEvent _writeSignal = new AutoResetEvent(false);
        /// <summary>
        /// To support Multithread, we should use BlockingCollection.
        /// </summary>
        private readonly BlockingCollection<string> _writeQueue = new BlockingCollection<string>();

        private bool _notifiedSucceeded;

        internal NamedPipeConnection(int id, string name, PipeStream serverStream)
        {
            Id = id;
            Name = name;
            _streamWrapper = new PipeStreamWrapper(serverStream);
        }

        /// <summary>
        /// Begins reading from and writing to the named pipe on a background thread.
        /// This method returns immediately.
        /// </summary>
        public void Open()
        {
            var readWorker = new Worker();
            readWorker.Succeeded += OnSucceeded;
            readWorker.Error += OnError;
            readWorker.DoWork(ReadPipe);

            var writeWorker = new Worker();
            writeWorker.Succeeded += OnSucceeded;
            writeWorker.Error += OnError;
            writeWorker.DoWork(WritePipe);
        }

        /// <summary>
        /// Adds the specified <paramref name="message"/> to the write queue.
        /// The message will be written to the named pipe by the background thread
        /// at the next available opportunity.
        /// </summary>
        /// <param name="message"></param>
        public void PushMessage(string message)
        {
            _writeQueue.Add(message);
            _writeSignal.Set();
        }

        /// <summary>
        /// Closes the named pipe connection and underlying <c>PipeStream</c>.
        /// </summary>
        public void Close()
        {
            CloseImpl();
        }

        /// <summary>
        ///     Invoked on the background thread.
        /// </summary>
        private void CloseImpl()
        {
            _streamWrapper.Close();
            _writeSignal.Set();
        }

        /// <summary>
        ///     Invoked on the UI thread.
        /// </summary>
        private void OnSucceeded()
        {
            // Only notify observers once
            if (_notifiedSucceeded)
                return;

            _notifiedSucceeded = true;

            if (Disconnected != null)
                Disconnected(this);
        }

        /// <summary>
        ///     Invoked on the UI thread.
        /// </summary>
        /// <param name="exception"></param>
        private void OnError(Exception exception)
        {
            if (Error != null)
                Error(this, exception);
        }

        /// <summary>
        ///     Invoked on the background thread.
        /// </summary>
        /// <exception cref="SerializationException">An object in the graph of type parameter <typeparamref name="TRead"/> is not marked as serializable.</exception>
        private void ReadPipe()
        {

            while (IsConnected && _streamWrapper.CanRead)
            {
                try
                {
                    var sz = _streamWrapper.ReadString();
                    if (sz == null)
                    {
                        CloseImpl();
                        return;
                    }
                    if (ReceiveMessage != null)
                        ReceiveMessage(this, sz);
                }
                catch
                {
                    //we must igonre exception, otherwise, the namepipe wrapper will stop work.
                }
            }
            
        }

        /// <summary>
        ///     Invoked on the background thread.
        /// </summary>
        /// <exception cref="SerializationException">An object in the graph of type parameter <typeparamref name="TWrite"/> is not marked as serializable.</exception>
        private void WritePipe()
        {
            
                while (IsConnected && _streamWrapper.CanWrite)
                {
                    try
                    {
                        //using blockcollection, we needn't use singal to wait for result.
                        //_writeSignal.WaitOne();
                        //while (_writeQueue.Count > 0)
                        {
                            _streamWrapper.WriteString(_writeQueue.Take());
                            _streamWrapper.WaitForPipeDrain();
                        }
                    }
                    catch
                    {
                    //we must igonre exception, otherwise, the namepipe wrapper will stop work.
                }
            }
          
        }
    }

    static class ConnectionFactory
    {
        private static int _lastId;

        public static NamedPipeConnection CreateConnection(PipeStream pipeStream)
        {
            return new NamedPipeConnection(++_lastId, "Client " + _lastId, pipeStream);
        }
    }

    /// <summary>
    /// Handles new connections.
    /// </summary>
    /// <param name="connection">The newly established connection</param>
    public delegate void ConnectionEventHandler(NamedPipeConnection connection);

    /// <summary>
    /// Handles messages received from a named pipe.
    /// </summary>
    /// <param name="connection">Connection that received the message</param>
    /// <param name="message">Message sent by the other end of the pipe</param>
    public delegate void ConnectionMessageEventHandler(NamedPipeConnection connection, string message);

    /// <summary>
    /// Handles exceptions thrown during read/write operations.
    /// </summary>
    /// <param name="connection">Connection that threw the exception</param>
    /// <param name="exception">The exception that was thrown</param>
    public delegate void ConnectionExceptionEventHandler(NamedPipeConnection connection, Exception exception);
}
