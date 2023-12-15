using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using NamedPipeWrapper;
using log4net.Appender;
using log4net.Config;
using log4net.Layout;

namespace UnitTests
{
    [TestFixture]
    class DataTests
    {
        private static readonly log4net.ILog Logger =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        static DataTests()
        {
            var layout = new PatternLayout("%-6timestamp %-5level - %message%newline");
            var appender = new ConsoleAppender { Layout = layout };
            layout.ActivateOptions();
            appender.ActivateOptions();
            BasicConfigurator.Configure(appender);
        }

        private const string PipeName = "data_test_pipe";

        private NamedPipeServer _server;
        private NamedPipeClient _client;

        private string _expectedData;
        private string _actualData;
        private bool _clientDisconnected;

        private DateTime _startTime;

        private readonly ManualResetEvent _barrier = new ManualResetEvent(false);

        #region Setup and teardown

        [SetUp]
        public void SetUp()
        {
            Logger.Debug("Setting up test...");

            _barrier.Reset();

            _server = new NamedPipeServer(PipeName);
            _client = new NamedPipeClient(PipeName,".");

            _expectedData = null;
            _actualData = null;
            _clientDisconnected = false;

            _server.ClientDisconnected += ServerOnClientDisconnected;
            _server.ClientMessage += ServerOnClientMessage;

            _server.Error += ServerOnError;
            _client.Error += ClientOnError;

            _server.Start();
            _client.Start();

            // Give the client and server a few seconds to connect before sending data
            Thread.Sleep(TimeSpan.FromSeconds(1));

            Logger.Debug("Client and server started");
            Logger.Debug("---");

            _startTime = DateTime.Now;
        }

        private void ServerOnError(Exception exception)
        {
            string msg = "ServerError:" + exception.ToString();
            Logger.Error(msg);
            throw new Exception(msg);
        }

        private void ClientOnError(Exception exception)
        {
            string msg= "ClientError: " + exception.ToString();
            Logger.Error(msg);
            throw new Exception(msg);
        }

        [TearDown]
        public void TearDown()
        {
            Logger.Debug("---");
            Logger.Debug("Stopping client and server...");

            _server.Stop();
            _client.Stop();

            _server.ClientDisconnected -= ServerOnClientDisconnected;
            _server.ClientMessage -= ServerOnClientMessage;

            _server.Error -= ServerOnError;
            _client.Error -= ClientOnError;

            Logger.Debug("Client and server stopped");
            Logger.DebugFormat("Test took {0}", (DateTime.Now - _startTime));
            Logger.Debug("~~~~~~~~~~~~~~~~~~~~~~~~~~");
        }

        #endregion

        #region Events

        private void ServerOnClientDisconnected(NamedPipeConnection connection)
        {
            Logger.Warn("Client disconnected");
            _clientDisconnected = true;
            _barrier.Set();
        }

        private void ServerOnClientMessage(NamedPipeConnection connection, string message)
        {
            Logger.Debug(string.Format("ServerOnClientMessage:message={0}", message));
            _actualData = message;
            _barrier.Set();
        }

        #endregion

        #region Tests

        [Test]
        public void TestEmptyMessageDoesNotDisconnectClient()
        {
            SendMessageToServer("1");
            _barrier.WaitOne(TimeSpan.FromSeconds(2));
            Assert.NotNull(_actualData, "Server should have received a zero-byte message from the client");
            Assert.IsFalse(_clientDisconnected, "Server should not disconnect the client for explicitly sending zero-length data");
        }

        [Test]
        public void TestMessageUtf8()
        {
            const string testmgs = "äüöß";
            SendMessageToServer("äüöß");
            _barrier.WaitOne(TimeSpan.FromSeconds(20));
            Assert.AreEqual(_actualData, testmgs);
            Assert.IsFalse(_clientDisconnected, "Server should still be connected to the client");
        }

        #endregion

        #region Helper methods

        private void SendMessageToServer(string mydata)
        {

            // Generate some random data and compute its SHA-1 hash
            var data = mydata;
            //new Random().NextBytes(data);

            _expectedData = data;


            _client.PushMessage(data);

            Logger.DebugFormat("Finished sending {0} bytes of data to the client",mydata);
        }

        /// <summary>
        /// Computes the SHA-1 hash (lowercase) of the specified byte array.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        private static string Hash(byte[] bytes)
        {
            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            {
                var hash = sha1.ComputeHash(bytes);
                var sb = new StringBuilder();
                for (var i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        #endregion
    }
}
