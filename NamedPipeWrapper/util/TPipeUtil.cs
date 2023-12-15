using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NamedPipeWrapper.util
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static class TPipeUtil
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        public static string readMessageFromNamedPipe(PipeStream namedPipe, int messageBufferSize=(5000))
        {
            StringBuilder messageBuilder = new StringBuilder();
            string messageChunk = string.Empty;
            byte[] messageBuffer = new byte[messageBufferSize];
            do
            {
                var bytesread=namedPipe.Read(messageBuffer, 0, messageBuffer.Length);
                messageChunk = Encoding.UTF8.GetString(messageBuffer,0,bytesread);
                messageBuilder.Append(messageChunk);
                messageBuffer = new byte[messageBuffer.Length];
            }
            while (!namedPipe.IsMessageComplete);
            return messageBuilder.ToString();
        }

        public static void writeMessageToNamedPipe(PipeStream namedPipe, string message,int messageBufferSize=5000)
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            namedPipe.Write(messageBytes, 0, messageBytes.Length);
        }



    }
}
