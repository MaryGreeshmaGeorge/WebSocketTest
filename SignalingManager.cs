using Signalling.Model;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
//using WebSocketSharp;
//using WebSocketSharp.Server;
//using ErrorEventArgs = WebSocketSharp.ErrorEventArgs;

namespace TestSocket
{
    internal class SignalingManager
    {
        private class ServerClientInfo
        {
            public string ServerId;
            public BeamClient ClientInfo;
        }

        private static readonly ConcurrentDictionary<byte, ServerClientInfo> Clients;

        private readonly DataContractSerializer _serializer;
        private readonly MemoryStream _writeStream;
        private readonly object _streamLock;

        static SignalingManager()
        {  
            Clients = new ConcurrentDictionary<byte, ServerClientInfo>();
        }

        public SignalingManager()
        {
            _serializer = new DataContractSerializer(typeof(BeamClient));
            _writeStream = new MemoryStream();
            _streamLock = new object();
        }

        protected override void OnClose(CloseEventArgs e)
        {
            Sessions.Sweep();
            var curClients = Clients.Keys.ToArray();

            foreach (var clientId in curClients)
            {
                if (!Sessions.Sessions.Any(session => session.ID == Clients[clientId].ServerId))
                {
                    ServerClientInfo info;
                    Clients.TryRemove(clientId, out info);
                }
            }
        }

        protected override void OnError(ErrorEventArgs e)
        {
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            if (!e.IsBinary)
            {
                Debug.WriteLine("non binary message type received");
                return;
            }

            using (var stream = new MemoryStream(e.RawData))
            {
                var destId = (byte) stream.ReadByte();
                var messageType = (MessageType) stream.ReadByte();

                switch (messageType)
                {
                    case MessageType.Registration:
                        Debug.Assert(destId == 0);
                        RegistrationHandler(stream);
                        break;
                    default:
                        Debug.Assert(destId != 0);
                        Sessions.SendTo(e.RawData, Clients[destId].ServerId);
                        break;
                }
            }
        }

        protected override void OnOpen()
        {
        }

        private void SendMessage(MessageHeader header)
        {
            lock (_streamLock)
            {
                _writeStream.SetLength(0);
                _writeStream.WriteByte(header.DestinationId);
                _writeStream.WriteByte((byte) header.Type);

                _writeStream.Position = 0;
                Send(_writeStream, (int) _writeStream.Length);
            }
        }

        //If id = 0 in header, the message is broadcast
        private void SendMessage(MessageHeader header, BeamClient clientInfo)
        {
            lock (_streamLock)
            {
                _writeStream.SetLength(0);
                _writeStream.WriteByte(header.DestinationId);
                _writeStream.WriteByte((byte) header.Type);

                _serializer.WriteObject(_writeStream, clientInfo);
                _writeStream.Position = 0;

                if (header.DestinationId == 0)
                {
                    Sessions.Broadcast(_writeStream.ToArray());
                }
                else
                {
                    Send(_writeStream, (int) _writeStream.Length);
                }
            }
        }

        private void RegistrationHandler(Stream stream)
        {
            byte newClientId = 1;
            while (Clients.ContainsKey(newClientId) && newClientId < byte.MaxValue)
            {
                newClientId++;
            }

            if (newClientId == byte.MaxValue)
            {
                throw new NotImplementedException();
            }

            var newClient = _serializer.ReadObject(stream) as BeamClient;
            Debug.Assert(newClient != null);
            newClient.Id = newClientId;

            //TODO: Check if add fails
            Clients.TryAdd(newClientId, new ServerClientInfo()
            {
                //ServerId = this.ID,
                ServerId = Guid.NewGuid().ToString(),
                ClientInfo = newClient
            });

            //Send back the id to new client
            SendMessage(new MessageHeader()
            {
                DestinationId = newClientId,
                Type = MessageType.Registration
            });

            //broadcast new clients info to everyone
            SendMessage(new MessageHeader()
                {
                    DestinationId = 0,
                    Type = MessageType.ClientInfo
                },
                newClient);

            //Send info about all connected clients to the new client
            var infoHeader = new MessageHeader()
            {
                DestinationId = newClientId,
                Type = MessageType.ClientInfo
            };
            foreach (var client in Clients)
            {
                if (client.Key != newClientId)
                {
                    SendMessage(infoHeader, client.Value.ClientInfo);
                }
            }
        }
    }
}