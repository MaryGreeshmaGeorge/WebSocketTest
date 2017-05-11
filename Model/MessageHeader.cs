using System;
using System.Runtime.Serialization;

namespace Signalling.Model
{
    public enum MessageType : byte
    {
        Registration,
        ClientInfo,
        Offer,
        Answer,
        IceCandidate,
        CameraMappingInfo
    }

   // [Serializable]
    [DataContract]
    public class MessageHeader
    {
        [DataMember]
        public byte DestinationId;

        [DataMember]
        public MessageType Type;
    }
}