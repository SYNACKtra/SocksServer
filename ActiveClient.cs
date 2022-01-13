using System;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Text;
using System.Buffers;
using System.Linq;

namespace SocksServer
{
    public class ActiveClient
    {
        protected SocketAsyncEventArgs m_read;
        protected SocketAsyncEventArgs m_write;
        Server l_server;
        int l_totalBytesRead;

        public ActiveClient(Server server, SocketAsyncEventArgs read)
        {
            Console.WriteLine("Creating client");
        	l_server = server;
            m_read = read;
            //m_write = write;
            ((AsyncUserToken)m_read.UserToken).Client = this;
            //((AsyncUserToken)m_write.UserToken).Client = this;
        }

        public void Begin(SocketAsyncEventArgs write)
        {
        	this.m_write = write;
            Console.WriteLine("Taking accessing socket");
            ((AsyncUserToken)this.m_read.UserToken).Socket = this.m_write.AcceptSocket;

            this.Receive();
        }

        public void Receive()
        {
            // As soon as the client is connected, post a receive to the connection
            Console.WriteLine("Taking receiveasync");
            bool willRaiseEvent = m_write.AcceptSocket.ReceiveAsync(m_read);
            if(!willRaiseEvent){
                Console.WriteLine("processing");
                ProcessReceive(m_read);
            }
        }

        public void ProcessReceive(SocketAsyncEventArgs e)
        {
            AsyncUserToken token = (AsyncUserToken)e.UserToken;
            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                //increment the count of the total bytes receive by the server
                Interlocked.Add(ref l_totalBytesRead, e.BytesTransferred);
                Console.WriteLine("The server has read a total of {0} bytes", l_totalBytesRead);

                //echo the data received back to the client
                e.SetBuffer("Hello, World".ToCharArray().Select(c => (byte)c).ToArray(), 0, 12);

                bool willRaiseEvent = token.Socket.SendAsync(e);
                if (!willRaiseEvent)
                {
                    ProcessSend(e);
                }
            }
            else
            {
            	l_server.CloseClientSocket(e);
            }
        }

        public void ProcessSend(SocketAsyncEventArgs e)
        {
        	Console.WriteLine("ProcessSend");
        }
    }
}