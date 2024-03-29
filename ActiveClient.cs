using System;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Text;
using System.Buffers;
using System.Linq;

namespace SocksServer
{
    public enum ClientState {
        NotConnected,
        TCP_SYNACK,
        Authenticated,
        Accepted,
        Acknowledged,
        ReceivingRequest,
        Request,
        Connecting,
        RemoteConnecting,
        RemoteConnected,
        RemoteConnecting_Notify
    }

    public class ActiveClient
    {
    	public static string HexDump(byte[] bytes, int bytesPerLine, int bytesLength)
        {
            char[] HexChars = "0123456789ABCDEF".ToCharArray();

            int firstHexColumn =
                  8                   // 8 characters for the address
                + 3;                  // 3 spaces

            int firstCharColumn = firstHexColumn
                + bytesPerLine * 3       // - 2 digit for the hexadecimal value and 1 space
                + (bytesPerLine - 1) / 8 // - 1 extra space every 8 characters from the 9th
                + 2;                  // 2 spaces 

            int lineLength = firstCharColumn
                + bytesPerLine           // - characters to show the ascii value
                + Environment.NewLine.Length; // Carriage return and line feed (should normally be 2)

            char[] line = (new String(' ', lineLength - Environment.NewLine.Length) + Environment.NewLine).ToCharArray();
            int expectedLines = (bytesLength + bytesPerLine - 1) / bytesPerLine;
            StringBuilder result = new StringBuilder(expectedLines * lineLength);

            for (int i = 0; i < bytesLength; i += bytesPerLine)
            {
                line[0] = HexChars[(i >> 28) & 0xF];
                line[1] = HexChars[(i >> 24) & 0xF];
                line[2] = HexChars[(i >> 20) & 0xF];
                line[3] = HexChars[(i >> 16) & 0xF];
                line[4] = HexChars[(i >> 12) & 0xF];
                line[5] = HexChars[(i >> 8) & 0xF];
                line[6] = HexChars[(i >> 4) & 0xF];
                line[7] = HexChars[(i >> 0) & 0xF];

                int hexColumn = firstHexColumn;
                int charColumn = firstCharColumn;

                for (int j = 0; j < bytesPerLine; j++)
                {
                    if (j > 0 && (j & 7) == 0) hexColumn++;
                    if (i + j >= bytesLength)
                    {
                        line[hexColumn] = ' ';
                        line[hexColumn + 1] = ' ';
                        line[charColumn] = ' ';
                    }
                    else
                    {
                        byte b = bytes[i + j];
                        line[hexColumn] = HexChars[(b >> 4) & 0xF];
                        line[hexColumn + 1] = HexChars[b & 0xF];
                        line[charColumn] = (b < 32 ? '·' : (char)b);
                    }
                    hexColumn += 3;
                    charColumn++;
                }
                result.Append(line);
            }
            return result.ToString();
        }

        SocketAsyncEventArgs m_read;
        SocketAsyncEventArgs l_remoteRead;
        SocketAsyncEventArgs l_remoteConnect;
        Server m_server;
        Socket m_socket;
        Socket m_outbound;
        int l_totalBytesRead;
        public ClientState m_ClientState = ClientState.NotConnected;
        byte? socksVersion = null;
        int? nMethods = null;
        int nMethodsCounter = -1;
        bool[] methods = new bool[255];
        bool m_ackSent = false;
        bool requestGiven = false;
        static short BUFFER_SIZE = 16;
        static short MAX_DNS_SIZE = 255;  //The maximum length of a DNS name is 255 octets. This is spelled out in RFC 1035 section 2.3.4. A customer didn’t understand why the DnsValidateName was rejecting the following string: Is longer than 255 octets. Contains a label longer than 63 octets.
        static short MAX_METHODS = 255;
        static int INDEX = 0;
        static int SOCKS5_SOCK_ARGS_COUNT = 5;
        int m_index;

        byte? requestSocksVersion = null;
        int? requestCommand = null;
        byte? requestReserved = null;
        int? requestAddressCounter = null;
        int? requestAddressType = null;
        bool requestAddressObtained = false;
        bool requestPortObtained = false;
        string requestAddress;
        byte? requestPortCounter = null;
        byte?[] requestPortBytes = new byte?[2];
        short requestPort = -1;
        bool clientUntoten = false;

        CancellationTokenSource startForwardingTokenDestination;
        bool dstTokenRegistered = false;
        CancellationTokenSource sourceOfHappiness;

        public EventHandler<SocketAsyncEventArgs> Loop;
        public EventHandler<SocketAsyncEventArgs> m_InboundReceive;
        public EventHandler<SocketAsyncEventArgs> m_OutboundReceive;

        public SocketAsyncEventArgs[] eArgs;

        bool evenFresherConnectSkip = false;

        public Socket Socket
        {
            get {
                return m_socket;
            }
        }

        public ActiveClient(Server server)
        {
            Console.WriteLine("Creating client");
        	m_server = server;
            //m_socket = socket;
            Loop = new EventHandler<SocketAsyncEventArgs>(eLoop);
            m_InboundReceive = new EventHandler<SocketAsyncEventArgs>(InboundReceive);
            m_OutboundReceive = new EventHandler<SocketAsyncEventArgs>(OutboundReceive);

            this.eArgs = new SocketAsyncEventArgs[Server.SOCK_ARGS_COUNT];

            m_index = INDEX++;
        }

        public void SetSocket(Socket lSocket)
        {
            m_socket = lSocket;
        }

        public void AllocPool()
        {
            for (int y = 0; y < ActiveClient.SOCKS5_SOCK_ARGS_COUNT; y++)
            {
                SocketAsyncEventArgs eventArgs = this.m_server.m_readWritePool.Take();
                eventArgs.UserToken = this;
                eventArgs.Completed += this.Loop;

                int bufferSize;
                switch(y) {
                    /*  
                   +----+----------+----------+
                   |VER | NMETHODS | METHODS  |
                   +----+----------+----------+
                   | 1  |    1     | 1 to 255 |
                   +----+----------+----------+ */

                    case 0:
                        bufferSize = 2 + MAX_METHODS;
                        break;
                    case 1:
                    /*
                         +----+--------+
                         |VER | METHOD |
                         +----+--------+
                         | 1  |   1    |
                         +----+--------+
                    */
                        bufferSize = -1;
                        break;
                    case 2:
                        bufferSize = 5 + MAX_DNS_SIZE;
                        break;
                    case 4:
                        bufferSize = 6 + MAX_DNS_SIZE;//TODO: config
                        break;
                    case 7:
                        bufferSize = BUFFER_SIZE;
                        break;
                    case 8:
                        bufferSize = BUFFER_SIZE;
                        break;
                    default:
                        bufferSize = 512;
                        break;
                }

                if(bufferSize > -1) {
                    eventArgs.SetBuffer(this.m_server.m_bufferManager.TakeBuffer(bufferSize), 0, bufferSize);
                }

                this.eArgs[y] = eventArgs;
                Console.WriteLine("Allocated " + y + " " + eventArgs.ToString());
            }
        }

        public void StartAccept()
        {
            this.AllocPool();
            this.m_ClientState = ClientState.TCP_SYNACK;

            m_socket.ReceiveAsync(this.eArgs[0]);
        }

        byte[] recvBuffer;
        byte[] sendBuffer;
        public void eLoop(object sender, SocketAsyncEventArgs e)
        {
            if(clientUntoten || this.eArgs == null)
            {
                return;
            }

            Console.WriteLine("[" + m_index + "] " + "DIAG" + m_ClientState);
            Console.WriteLine("[" + m_index + "] " + (sender == m_outbound));
            Console.WriteLine("[" + m_index + "] " + e.LastOperation);
            Console.WriteLine("[" + m_index + "] " + e.BytesTransferred);
            if(e.BytesTransferred > 0)
            {
                Console.Write(HexDump(e.Buffer, 16, e.BytesTransferred));
            }
            Console.WriteLine("[" + m_index + "] " + "DIAGEND");


            if(m_ClientState == ClientState.TCP_SYNACK && e.LastOperation == SocketAsyncOperation.Receive)
            {
                /*
                   The client connects to the server, and sends a version
                   identifier/method selection message:

                                   +----+----------+----------+
                                   |VER | NMETHODS | METHODS  |
                                   +----+----------+----------+
                                   | 1  |    1     | 1 to 255 |
                                   +----+----------+----------+

                   The VER field is set to X'05' for this version of the protocol.  The
                   NMETHODS field contains the number of method identifier octets that
                   appear in the METHODS field.
                */
                this.ProcessSocksAccept(sender, this.eArgs[0]);

                return;
            }
            else if(m_ClientState == ClientState.Accepted && e.LastOperation == SocketAsyncOperation.Receive)
            {
                byte[] buffer = m_server.m_bufferManager.TakeBuffer(2);
                buffer[0] = 5;

            /* 
               If the selected METHOD is X'FF', none of the methods listed by the
               client are acceptable, and the client MUST close the connection.

               The values currently defined for METHOD are:

                      o  X'00' NO AUTHENTICATION REQUIRED
                      o  X'01' GSSAPI
                      o  X'02' USERNAME/PASSWORD
                      o  X'03' to X'7F' IANA ASSIGNED
                      o  X'80' to X'FE' RESERVED FOR PRIVATE METHODS
                      o  X'FF' NO ACCEPTABLE METHODS
            */
                if(methods[0x00]) {
                    this.m_ClientState = ClientState.Authenticated;
                    buffer[1] = 0x00;
                    this.eArgs[1].SetBuffer(buffer, 0, 2);

                    if(!this.m_socket.SendAsync(this.eArgs[1]))
                    {
                        eLoop(sender, this.eArgs[1]);
                        return;
                    } else {
                        return;
                    }
                } else {
                    Console.WriteLine("No authentication methods found");
                    return;
                }

            }
            else if(m_ClientState == ClientState.Authenticated && e.LastOperation == SocketAsyncOperation.Send)
            {
                this.m_ClientState = ClientState.ReceivingRequest;
                if(!this.m_socket.ReceiveAsync(this.eArgs[2]))
                {
                    eLoop(sender, this.eArgs[2]);
                    return;
                }
            }
            /*
            +----+-----+-------+------+----------+----------+
            |VER | CMD |  RSV  | ATYP | DST.ADDR | DST.PORT |
            +----+-----+-------+------+----------+----------+
            | 1  |  1  | X'00' |  1   | Variable |    2     |
            +----+-----+-------+------+----------+----------+
            */
            else if (m_ClientState == ClientState.ReceivingRequest)
            {
                this.ProcessSocksRequest(sender, e);

                if (this.m_ClientState == ClientState.Connecting)
                {
                    bool happy = this.StartProxying(sender);// this.eArgs[3]
                    sourceOfHappiness.Token.Register(() => {
                        this.eArgs[4].UserToken = ClientState.RemoteConnected;
                        this.m_socket.SendAsync(this.eArgs[4]);
                    });
                }
            }
            else if(m_ClientState == ClientState.RemoteConnecting_Notify
                    || (m_ClientState == ClientState.RemoteConnecting && e.LastOperation == SocketAsyncOperation.Connect && this.m_outbound == sender))
            {
                /* RFC1928 §6
                   bytes consumed by SOCKS5 response
                +----+-----+-------+------+----------+----------+
                |VER | REP |  RSV  | ATYP | BND.ADDR | BND.PORT |
                +----+-----+-------+------+----------+----------+
                | 1  |  1  | X'00' |  1   | Variable |    2     |
                +----+-----+-------+------+----------+----------+
                */
                SocketAddress address = ((IPEndPoint)m_outbound.LocalEndPoint).Serialize();
                byte[] buffer, portBytes;
                portBytes = BitConverter.GetBytes((short)((IPEndPoint)m_outbound.LocalEndPoint).Port);
                for(int i = 0; i < 2; i++) {
                    Console.WriteLine(portBytes[i]);
                }
                Console.WriteLine();
                for(int i = 0; i < address.Size; i++) {
                    Console.WriteLine(address[i]);
                }

                switch(m_outbound.LocalEndPoint.AddressFamily) {
                    case AddressFamily.InterNetworkV6:
                        buffer = new byte[22] {0x05, 0, 0, 0x04, address[4], address[5], address[6], address[7], address[8], address[9], address[10], address[11], address[12], address[13], address[14], address[15], address[16], address[17], address[18], address[19], address[4], address[3]};
                        this.eArgs[4].SetBuffer(buffer, 0, 22);
                        break;
                    case AddressFamily.InterNetwork:
                        buffer = new byte[10] {0x05, 0, 0, 0x01, address[4], address[5], address[6], address[7], address[2], address[3]};
                        this.eArgs[4].SetBuffer(buffer, 0, 10);
                        break;
                    default:
                        throw new Exception("Protocol not implemented");
                }

                Console.WriteLine("SOCKS5 response");
                sourceOfHappiness.Cancel();
                return;
            }
            else if(m_socket == sender && (e.UserToken is ClientState && (ClientState)e.UserToken == ClientState.RemoteConnected) && e.LastOperation == SocketAsyncOperation.Send)
            {
                Console.WriteLine("Test");
                this.m_ClientState = ClientState.RemoteConnected;
                startForwardingTokenDestination.Cancel();
                SocketAsyncEventArgs args = this.m_server.m_readWritePool.Take(BUFFER_SIZE);
                args.Completed += this.m_InboundReceive;    
                //args.SocketFlags = SocketFlags.Partial;
                this.m_socket.ReceiveAsync(args);
                return;
            }

            startForwarding:

            if(this.m_ClientState != ClientState.RemoteConnected)
            {
                Console.WriteLine("Dropped out of connecting loop");
                return;
            }

            if(!CheckAlive(sender, e))
            {
                Console.WriteLine("ActiveClient Dead");
                return;
            }

            Console.WriteLine("Connect loop: 3");

            if(m_ClientState == ClientState.RemoteConnected && e.SocketError == SocketError.Success)
            {
                Console.WriteLine("SOCKFLAGS:" + e.SocketFlags);
                Console.WriteLine("SOCKETERROR: " + e.SocketError);

                if((sender == m_socket) && e.LastOperation == SocketAsyncOperation.Send && e.UserToken == null) {
                    SocketAsyncEventArgs args = this.m_server.m_readWritePool.Take(BUFFER_SIZE);
                    args.Completed += this.m_OutboundReceive;
                    //args.SocketFlags = SocketFlags.Partial;

                    if(!this.m_outbound.ReceiveAsync(args))
                        Console.WriteLine("Help");
                    return;
                } else if((sender == m_outbound) && e.LastOperation == SocketAsyncOperation.Send && e.UserToken == null) {
                    SocketAsyncEventArgs args = this.m_server.m_readWritePool.Take(BUFFER_SIZE);
                    args.Completed += this.m_InboundReceive;
                    //args.SocketFlags = SocketFlags.Partial;
                    // args.RemoteEndPoint = this.m_socket.RemoteEndPoint;

                    if(!this.m_socket.ReceiveAsync(args))
                        Console.WriteLine("Test");
                    return;
                }
            }
        }

        public bool CheckAlive(object sender, SocketAsyncEventArgs e)
        {
            Console.WriteLine("Connect loop: 0");
            bool clientUntoten = false;
            if((e.SocketError != SocketError.Success && sender == m_outbound) || (m_outbound != null && !m_outbound.Connected))
            {
                try
                {
                    Console.WriteLine("Error on outbound socket " + e.SocketError);
                    this.eArgs = null;
                    m_socket.Close(0);
                    m_outbound.Close(0);
                }
                catch (SocketException ex)
                {
                    //FxTrace.Exception.TraceHandledException(ex, FxTrace.TraceEventType.Information);
                }
                catch (ObjectDisposedException ex)
                {
                    //FxTrace.Exception.TraceHandledException(ex, FxTrace.TraceEventType.Information);
                }
                finally
                {
                    clientUntoten = true;
                }
                return false;
            }

            Console.WriteLine("Connect loop: 1");

            if((e.SocketError != SocketError.Success && sender == m_socket) || !m_socket.Connected)
            {
                try
                {
                    Console.WriteLine("Error on inbound socket " + e.SocketError);
                    this.eArgs = null;
                    m_outbound.Close(0);
                    m_socket.Close(0);
                }
                catch (SocketException ex)
                {
                    //FxTrace.Exception.TraceHandledException(ex, FxTrace.TraceEventType.Information);
                }
                catch (ObjectDisposedException ex)
                {
                    //FxTrace.Exception.TraceHandledException(ex, FxTrace.TraceEventType.Information);
                }
                finally
                {
                    clientUntoten = true;
                }
                return false;
            }


            Console.WriteLine("Connect loop: 2");

            return clientUntoten || this.eArgs == null;
        }


        public void InboundReceive(object sender, SocketAsyncEventArgs e)
        {
            if((sender == m_socket) && (e.LastOperation == SocketAsyncOperation.Receive)) {
            // tryAgainS:
                if(e.BytesTransferred > 0 && e.Buffer != null) {
                    Console.WriteLine("A");
                    int copied = (e.BytesTransferred > BUFFER_SIZE ? BUFFER_SIZE : e.BytesTransferred);
                    byte[] originBuffer = m_server.m_bufferManager.TakeBuffer(copied);
                    Buffer.BlockCopy(e.Buffer, 0, originBuffer, 0, copied);
                    int rcv = Int32.Parse(e.BytesTransferred.ToString());
                    int offset = Int32.Parse(e.Offset.ToString());
                    Console.WriteLine("B");
                    Console.WriteLine(e.Count);

                    if(rcv <= BUFFER_SIZE) this.m_server.m_readWritePool.Return(e);
                    //this.m_server.m_readWritePool.Return(e);
                    //this.m_server.m_readWritePool.Return(this.eArgs[10]);

                    if(rcv > BUFFER_SIZE || e.SocketFlags == SocketFlags.Partial) {
                        Console.WriteLine("C");
                        bool wasUserTokenNull = e.UserToken == null;
                        SocketAsyncEventArgs eC = wasUserTokenNull ? this.m_server.m_readWritePool.Take(BUFFER_SIZE) : (SocketAsyncEventArgs) e.UserToken;
                        if(wasUserTokenNull) eC.Completed += this.Loop;

                        eC.SetBuffer(originBuffer, 0, BUFFER_SIZE);
                        eC.RemoteEndPoint = m_outbound.RemoteEndPoint;
                        e.UserToken = eC;
                        m_outbound.SendAsync(eC);

                        Console.WriteLine("D");
                        SocketAsyncEventArgs args = this.m_server.m_readWritePool.Take(BUFFER_SIZE);
                        args.Completed += this.Loop;
                        args.UserToken = eC;
                        //args.SocketFlags = SocketFlags.Partial;
                        //this.m_server.m_readWritePool.Return(e);

                        //this.eArgs[8] = this.m_server.m_readWritePool.Take();
                        if(!this.m_socket.ReceiveAsync(args)) {
                            Console.WriteLine("RECEIVE CANCELLED?");
                            Console.WriteLine("RECEIVE CANCELLED?");
                            Console.WriteLine("RECEIVE CANCELLED?");
                            return;
                        }
                        // goto tryAgainS;
                    } else {
                        Console.WriteLine("E");
                        bool wasUserTokenNull = (e.UserToken == null || !(e.UserToken is SocketAsyncEventArgs));
                        SocketAsyncEventArgs eC = wasUserTokenNull ? this.m_server.m_readWritePool.Take(copied) : (SocketAsyncEventArgs) e.UserToken;
                        Console.WriteLine("F");
                        eC.SetBuffer(originBuffer, 0, copied);
                        Console.WriteLine("G");
                        //this.eArgs[8] = this.m_server.m_readWritePool.Take();
                        eC.UserToken = null;
                        if(wasUserTokenNull) eC.Completed += this.Loop;
                        //eC.SocketFlags = e.UserToken == null ? SocketFlags.None : SocketFlags.Truncated;
                        m_outbound.SendAsync(eC);
                        Console.WriteLine("H");
                        //this.m_server.m_readWritePool.Return(e);
                    }
                }
                Console.WriteLine("inbound receive finished");

                return;
            } 
        }

        public void OutboundReceive(object sender, SocketAsyncEventArgs e)
        {
            if((sender == m_outbound) && (e.LastOperation == SocketAsyncOperation.Receive))
            {
                tryAgain:
                if(e.BytesTransferred > 0 && e.Buffer != null) {
                    Console.WriteLine("outbound receive");
                    int copied = (e.BytesTransferred > BUFFER_SIZE ? BUFFER_SIZE : e.BytesTransferred);
                    Console.WriteLine("copied: " + copied.ToString());
                    byte[] originBuffer = m_server.m_bufferManager.TakeBuffer(copied);
                    Buffer.BlockCopy(e.Buffer, 0, originBuffer, 0, copied);
                    int rcv = Int32.Parse(e.BytesTransferred.ToString());
                    int offset = Int32.Parse(e.Offset.ToString());
                    Console.WriteLine("rcv: " + rcv.ToString());
                    Console.WriteLine("offset: " + offset.ToString());
                    if(rcv <= BUFFER_SIZE) this.m_server.m_readWritePool.Return(e);

                    //this.m_server.m_readWritePool.Return(e);
                    //this.m_server.m_readWritePool.Return(this.eArgs[10]);

                    if(rcv > BUFFER_SIZE || e.SocketFlags == SocketFlags.Truncated) {
                        Console.WriteLine("buffer_size < rcv");
                        bool wasUserTokenNull = e.UserToken == null;
                        SocketAsyncEventArgs eC = wasUserTokenNull ? this.m_server.m_readWritePool.Take(BUFFER_SIZE) : (SocketAsyncEventArgs) e.UserToken;
                        if(wasUserTokenNull) eC.Completed += this.Loop;

                        eC.SetBuffer(originBuffer, 0, BUFFER_SIZE);
                        eC.RemoteEndPoint = m_socket.RemoteEndPoint;
                        m_socket.SendAsync(eC);

                        SocketAsyncEventArgs args = this.m_server.m_readWritePool.Take(BUFFER_SIZE);
                        args.Completed += this.Loop;
                        args.UserToken = eC;
                        //args.SocketFlags = SocketFlags.Partial;
                        //this.m_server.m_readWritePool.Return(e);

                        //this.eArgs[8] = this.m_server.m_readWritePool.Take();
                        if(!this.m_outbound.ReceiveAsync(args)) {
                            Console.WriteLine("RECEIVE CANCELLED?");
                            Console.WriteLine("RECEIVE CANCELLED?");
                            Console.WriteLine("RECEIVE CANCELLED?");
                            return;
                        }
                        // goto tryAgain;
                    } else {
                        bool wasUserTokenNull = (e.UserToken == null || !(e.UserToken is SocketAsyncEventArgs));
                        SocketAsyncEventArgs eC = wasUserTokenNull ? this.m_server.m_readWritePool.Take(copied) : (SocketAsyncEventArgs) e.UserToken;
                        eC.SetBuffer(originBuffer, 0, copied);
                        eC.UserToken = null;
                        if(wasUserTokenNull) eC.Completed += this.Loop;
                        //eC.SocketFlags = SocketFlags.Truncated;
                        //this.eArgs[8] = this.m_server.m_readWritePool.Take();
                        m_socket.SendAsync(eC);
                        //this.m_server.m_readWritePool.Return(e);
                    }
                }

                Console.WriteLine("outbound receive finished ");
            }
        }

        public void ProcessSocksAccept(object sender, SocketAsyncEventArgs e)
        {
            int offset = 0;

            Console.WriteLine(e.SocketError);
            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                //increment the count of the total bytes receive by the server
                Interlocked.Add(ref l_totalBytesRead, e.BytesTransferred);
                Console.WriteLine("The server has read a total of {0} bytes for this client", l_totalBytesRead);

                Console.Write(HexDump(e.Buffer, 16, e.BytesTransferred));
                if(socksVersion == null && e.BytesTransferred >= 0)
                {
                	socksVersion = e.Buffer[offset];
                	offset++;
                	Console.WriteLine("SocksVersion: " + socksVersion.ToString());
                }

                if(socksVersion == 5) {
	                if(nMethods == null && e.BytesTransferred >= 0)
	                {
	                	nMethods = e.Buffer[offset];
	                	nMethodsCounter = (int) nMethods;
	                	offset++;
	                	Console.WriteLine("nMethods: " + nMethods.ToString());
                    }

               	moreMethods:
	               	if(nMethodsCounter > 0 && e.BytesTransferred >= 0)
	               	{
	               		nMethodsCounter--;
	                	methods[e.Buffer[offset]] = true;
	               		Console.WriteLine("Method: " + e.Buffer[offset]);
	               		offset++;
	               		if(e.BytesTransferred >= 0) {
	               			goto moreMethods;
	               		}
                    }

	               	// client has finished sending their methods
	               	if(nMethodsCounter == 0) {
                        this.m_ClientState = ClientState.Accepted;
                        eLoop(sender, e);
	               	} else {
                        // count timeout
                    }
                }
            }
            else
            {
                try
                {
                    m_socket.Close(0);
                }
                catch (SocketException ex)
                {
                    //FxTrace.Exception.TraceHandledException(ex, FxTrace.TraceEventType.Information);
                }
                catch (ObjectDisposedException ex)
                {
                    //FxTrace.Exception.TraceHandledException(ex, FxTrace.TraceEventType.Information);
                }
                finally
                {
                    clientUntoten = true;
                }
            }
        }

        public void ProcessSocksAckSent(object sender, SocketAsyncEventArgs e)
        {
            Console.WriteLine("ProcessSend");
            if (this.m_ClientState == ClientState.Accepted)
            {
                this.m_ClientState = ClientState.Acknowledged;
            }
        }

        public void ProcessSocksRequest(object sender, SocketAsyncEventArgs e)
        {
            int offset = 0;

            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                Console.WriteLine("Trying dump");
                Console.WriteLine(HexDump(e.Buffer, 16, e.BytesTransferred));  
                if(requestSocksVersion == null && e.BytesTransferred >= 0)
                {
                    requestSocksVersion = e.Buffer[offset];
                    offset++;
                    Console.WriteLine("RequestSocksVersion: " + requestSocksVersion.ToString());
                }
                if(requestCommand == null && e.BytesTransferred >= 0)
                {
                    requestCommand = e.Buffer[offset];
                    offset++;
                    Console.WriteLine("RequestCommand: " + requestCommand.ToString());
                }
                if(requestReserved == null && e.BytesTransferred >= 0)
                {
                    requestReserved = e.Buffer[offset];
                    offset++;
                    Console.WriteLine("RequestReserved: " + requestReserved.ToString());
                }
                if(requestAddressType == null && e.BytesTransferred >= 0)
                { 
                    requestAddressType = e.Buffer[offset];
                    offset++;
                    Console.WriteLine("RequestAddressType: " + requestAddressType.ToString());
                }
                if(!requestAddressObtained && requestAddressType != null)
                {
                    switch(requestAddressType)
                    {
                        case 0x01:
                            Console.WriteLine("Request is an IPV4 address");
                            if(requestAddressCounter == null)
                            {
                                requestAddressCounter = 4;
                            }
                            if(requestAddressCounter == 4 && e.BytesTransferred >= 0)
                            {
                                requestAddress += e.Buffer[offset].ToString();
                                offset++;
                                requestAddressCounter--;
                            }
                            if(requestAddressCounter == 3 && e.BytesTransferred >= 0)
                            {
                                requestAddress += "." + e.Buffer[offset].ToString();
                                offset++;
                                requestAddressCounter--;
                            }
                            if(requestAddressCounter == 2 && e.BytesTransferred >= 0)
                            {
                                requestAddress += "." + e.Buffer[offset].ToString();
                                offset++;
                                requestAddressCounter--;
                            }
                            if(requestAddressCounter == 1 && e.BytesTransferred >= 0)
                            {
                                requestAddress += "." + e.Buffer[offset].ToString();
                                offset++;
                                requestAddressCounter--;
                                requestAddressObtained = requestAddressCounter == 0;
                            }
                            break;
                        case 0x03:
                            Console.WriteLine("Request is a fully qualified domain name");
                            if(requestAddressCounter == null && e.BytesTransferred >= 0)
                            {
                                requestAddressCounter = e.Buffer[offset];
                                offset++;
                            }
                            if(requestAddressCounter != 0 && e.BytesTransferred >= 0)
                            {
                                requestAddress += Convert.ToChar(e.Buffer[offset]);
                                requestAddressCounter--;
                                offset++;
                            }
                            requestAddressObtained = true;
                            break;
                        case 0x04:
                            Console.WriteLine("Request is an IPV6 address");
                            if(requestAddressCounter == null) {
                                requestAddressCounter = 16;
                            }
                            while(requestAddressCounter != 0 && e.BytesTransferred >= 0)
                            {
                                requestAddress += BitConverter.ToString(new byte[]{e.Buffer[offset]}) + (requestAddressCounter % 2 != 0 ? ":" : "");
                                offset++;
                                requestAddressCounter--;
                                requestAddressObtained = requestAddressCounter == 0;
                            }
                            if(requestAddress.EndsWith(":")) {
                                requestAddress = requestAddress.Substring(0, requestAddress.Length - 1);
                            }
                            break;
                    }
                }
                Console.WriteLine("Address: " + requestAddress);

                if(requestAddressObtained && requestPortCounter == null && e.BytesTransferred >= 0)
                {
                    requestPortBytes[0] = e.Buffer[offset];
                    requestPortCounter = 1;
                    offset++;
                    Console.WriteLine("Port[0]: " + requestPortBytes[0]);
                }

                if(requestAddressObtained && requestPortCounter == 1 && e.BytesTransferred >= 0)
                {
                    requestPortBytes[1] = e.Buffer[offset];
                    requestPortCounter = 2;
                    offset++;
                    Console.WriteLine("Port[1]: " + requestPortBytes[1]);
                    byte[] requestPortBytes_littleEndianRead = new byte[2];
                    requestPortBytes_littleEndianRead[0] = (byte)requestPortBytes[1];
                    requestPortBytes_littleEndianRead[1] = (byte)requestPortBytes[0];
                    requestPort = BitConverter.ToInt16(requestPortBytes_littleEndianRead, 0);
                    Console.WriteLine("Port: " + requestPort);
                }

                if(requestPort != -1)
                {
                    //this.m_read.Completed -= this.RequestHandler;
                    this.m_ClientState = ClientState.Connecting;
                }
            }
            else
            {
                try
                {

                    Console.WriteLine("Couldn't process request further, disconnecting socket");
                    m_socket.Close(0);
                }
                catch (SocketException ex)
                {
                    //FxTrace.Exception.TraceHandledException(ex, FxTrace.TraceEventType.Information);
                }
                catch (ObjectDisposedException ex)
                {
                    //FxTrace.Exception.TraceHandledException(ex, FxTrace.TraceEventType.Information);
                }
                finally
                {
                    clientUntoten = true;
                }
            }
        }

        public bool StartProxying(object sender)
        {
            Console.WriteLine("EndpointGet");

            IPEndPoint endpoint = NetHelper.GetIPEndPointFromHostName(requestAddress, requestPort, false);
            Console.WriteLine("EndpointGot");
            Console.WriteLine(endpoint);
            //this.l_readOutbound = new SocketAsyncEventArgs();
            Console.WriteLine("SettingEndpoint");
            SocketAsyncEventArgs remoteConnect = this.eArgs[3];
            remoteConnect.RemoteEndPoint = endpoint;

//            this.l_readOutbound.UserToken = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            this.m_outbound = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

          //  listenSocket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            System.Threading.Tasks.Task task = System.Threading.Tasks.Task.Run(() =>
            {
                startForwardingTokenDestination = new CancellationTokenSource();
                startForwardingTokenDestination.CancelAfter(60 * 60 * 1000);
                CancellationToken tokenDst = startForwardingTokenDestination.Token;
                tokenDst.Register(() => {
                    SocketAsyncEventArgs e = this.m_server.m_readWritePool.Take(BUFFER_SIZE);
                    e.Completed += this.m_OutboundReceive;
                    //e.SocketFlags = SocketFlags.Partial;
                    this.m_outbound.ReceiveAsync(e);
                });

                this.m_ClientState = ClientState.RemoteConnecting;
                if(!this.m_outbound.ConnectAsync(remoteConnect)) {
                    this.m_ClientState = ClientState.RemoteConnecting_Notify;
                    eLoop(this.m_outbound, remoteConnect);

                    // if(this.m_ClientState == ClientState.RemoteConnected) {
                    //     if(!this.m_outbound.ReceiveAsync(this.eArgs[8])) {
                    //         eLoop(this.m_outbound, this.eArgs[8]);
                    //         return;
                    //     }
                    // }
                }
            });

            sourceOfHappiness = new CancellationTokenSource();
            sourceOfHappiness.CancelAfter(60 * 60 * 1000);


            //task.Wait(1000);
            return true;
            //task.Wait();
            //this.l_readOutbound.UserToken = m_outbound;

            //m_outbound.
        }

        void SendConnected(object sender, SocketAsyncEventArgs e)
        {
            // recycle the outbound read to write the connected packet
        }
    }
}