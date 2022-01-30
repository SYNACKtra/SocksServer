using System.Net.Sockets;
using System.Net;
using System;
using System.Threading;
using MS;
using Mono;

namespace SocksServer
{
    public struct Client {
        public ActiveClient ConnectedClient { get; set; }
        public bool SkipToConnect { get; set; }
    }

    // Implements the connection logic for the socket server.
    // After accepting a connection, all data read from the client
    // is sent back to the client. The read and echo back to the client pattern
    // is continued until the client disconnects.
    public class Server
    {

        private int m_numConnections;   // the maximum number of connections the sample is designed to handle simultaneously
        private int m_receiveBufferSize;// buffer size to use for each socket I/O operation
        public BufferManager m_bufferManager;  // represents a large reusable set of buffers for all socket operations
        //const int opsToPreAlloc = 2;    // read, write (don't alloc buffer space for accepts)
        Socket listenSocket;            // the socket used to listen for incoming connection requests
        // pool of reusable SocketAsyncEventArgs objects for write, read and accept socket operations
        public QueuedSocketAsyncEventArgsPool m_readWritePool;
        public QueuedSocketAsyncEventArgsPool m_acceptPool;
        public EventHandler<SocketAsyncEventArgs> m_CompletedIO;
        public static int SOCK_ARGS_COUNT = 50000;
        public Client?[] m_acceptedConnections;
        int m_totalBytesRead;           // counter of the total # bytes received by the server
        int m_numConnectedSockets;      // the total number of clients connected to the server
        Semaphore m_maxNumberAcceptedClients;
        public SocketAsyncEventArgs acceptSocks;
        public static int AUTH_METHODS_COUNT = 1;

        // Create an uninitialized server instance.
        // To start the server listening for connection requests
        // call the Init method followed by Start method
        //
        // <param name="numConnections">the maximum number of connections the sample is designed to handle simultaneously</param>
        // <param name="receiveBufferSize">buffer size to use for each socket I/O operation</param>
        public Server(int numConnections, int receiveBufferSize)
        {
            m_totalBytesRead = 0;
            m_numConnectedSockets = 0;
            m_numConnections = numConnections;
            m_receiveBufferSize = receiveBufferSize;
            // allocate buffers such that the maximum number of sockets can have one outstanding read and
            //write posted to the socket simultaneously
            m_bufferManager = BufferManager.CreateBufferManager(numConnections * 10 * receiveBufferSize, receiveBufferSize);
            m_acceptPool = new QueuedSocketAsyncEventArgsPool(numConnections, m_bufferManager);
            m_readWritePool = new QueuedSocketAsyncEventArgsPool(numConnections * SOCK_ARGS_COUNT, m_bufferManager);
            m_maxNumberAcceptedClients = new Semaphore(numConnections, numConnections);
            m_CompletedIO = new EventHandler<SocketAsyncEventArgs>(IO_Completed);
        }

        // Initializes the server by preallocating reusable buffers and
        // context objects.  These objects do not need to be preallocated
        // or reused, but it is done this way to illustrate how the API can
        // easily be used to create reusable objects to increase server performance.
        //
        public void Init()
        {
            // Allocates one large byte buffer which all I/O operations use a piece of.  This gaurds
            // against memory fragmentation
            //m_bufferManager.InitBuffer();

            // preallocate pool of SocketAsyncEventArgs objects
            SocketAsyncEventArgs eventArgs;

            for (int i = 0; i < m_numConnections; i++)
            {
                //Pre-allocate a set of reusable SocketAsyncEventArgs
                eventArgs = m_acceptPool.Take();
                eventArgs.UserToken = null;
                eventArgs.Completed += m_CompletedIO;
                m_acceptPool.Return(eventArgs);
            }
        }

        // Starts the server such that it is listening for
        // incoming connection requests.
        //
        // <param name="localEndPoint">The endpoint which the server will listening
        // for connection requests on</param>
        public void Start(IPEndPoint localEndPoint)
        {
            // create the socket which listens for incoming connections
            listenSocket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(localEndPoint);
            // start the server with a listen backlog of 100 connections
            listenSocket.Listen(100);

            // post accepts on the listening socket
            StartAccept();

            //Console.WriteLine("{0} connected sockets with one outstanding receive posted to each....press any key", m_outstandingReadCount);
            Console.WriteLine("Press any key to terminate the server process....");
            Console.ReadKey();
        }

        // Begins an operation to accept a connection request from the client
        //
        // <param name="acceptEventArg">The context object to use when issuing
        // the accept operation on the server's listening socket</param>
        public void StartAccept()
        {
            Console.WriteLine("Starting to listen");

            //m_maxNumberAcceptedClients.WaitOne();
            //Console.WriteLine("LISTENING: accepting a connection");
        startAccepting:
            Socket s = listenSocket.Accept();

            // c.Initialize(acceptEventArg);

            if (s != null)
            {
                Console.WriteLine("Socket accepted");
                ActiveClient l_connectedClient = new ActiveClient(this);
                l_connectedClient.SetSocket(s);
                l_connectedClient.StartAccept();
                goto startAccepting;    
            }
            else
            {
                Console.WriteLine("ACCEPTING: event not fired");
            }
        }

        // This method is the callback method associated with Socket.AcceptAsync
        // operations and is invoked when an accept operation is complete
        //
        // void AcceptEventArg_Completed(object sender, SocketAsyncEventArgs e)
        // {
        //     Console.WriteLine("Client connection accepted. There are {0} clients connected to the server", m_numConnectedSockets);

        //     Console.WriteLine(e.UserToken);
        //     // for(int i = 0; i < this.m_numConnections; i++) {
        //     //     if(this.m_acceptedConnections[i] == null) {
        //     //         this.m_acceptedConnections[i] = ((Client)e.UserToken);
        //     //         break;
        //     //     }
        //     // }

        //     StartAccept(e);
        // }

        // This method is called whenever a receive or send operation is completed on a socket
        //
        // <param name="e">SocketAsyncEventArg associated with the completed receive operation</param>
        public void IO_Completed(object sender, SocketAsyncEventArgs e)
        {
            Client token;

            if(e.UserToken != null && e.UserToken is Client) {
                token = (Client) e.UserToken;
            } else {
                token = new Client();
                e.UserToken = (object) token;
            }

            if (token.ConnectedClient == null) {
                token.ConnectedClient = new ActiveClient(this);
            }

            Console.WriteLine("OP:" + e.LastOperation);
            Console.WriteLine("RX:" + sender);
            Console.WriteLine("TX:" + token);
            // determine which type of operation just completed and call the associated handler

            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    if(sender is Socket) {
                        Interlocked.Increment(ref m_numConnectedSockets);
                    }

                    break;  
                case SocketAsyncOperation.Receive:
                    ActiveClient aClient = token.ConnectedClient;
                    /*else if(client.m_ClientState == ClientState.Request) {
                        client.ProcessSocks
                    } */
                    //((AsyncUserToken)e.UserToken).Client.ProcessReceive(e);
                    break;
                case SocketAsyncOperation.Send:
                    //((AsyncUserToken)e.UserToken).Client.ProcessSend(e);
                    break;
                default:
                    throw new ArgumentException("The last operation completed on the socket was not a receive or send");
            }
        }

        public void CloseClientSocket(SocketAsyncEventArgs e)
        {
            Client token = (Client)e.UserToken;

            // close the socket associated with the client
            try
            {
                token.ConnectedClient.Socket.Shutdown(SocketShutdown.Send);
            }
            // throws if client process has already closed
            catch (Exception) { }
            token.ConnectedClient.Socket.Close();

            // decrement the counter keeping track of the total number of clients connected to the server
            Interlocked.Decrement(ref m_numConnectedSockets);

            // if(token != null) {
            //     for(int i = 0; i < this.m_numConnections; i++) {
            //         if(this.m_acceptedConnections[i] == token) {
            //             this.m_acceptedConnections[i] = null;
            //         }
            //     }
            // }

            // Free the SocketAsyncEventArg so they can be reused by another client
            m_readWritePool.Return(e);

            m_maxNumberAcceptedClients.Release();
            Console.WriteLine("A client has been disconnected from the server. There are {0} clients connected to the server", m_numConnectedSockets);
        }
    }    
}