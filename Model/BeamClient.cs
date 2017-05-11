using System;
using System.Runtime.Serialization;

namespace Signalling.Model
{
   // [System.Serializable]
    [DataContract]
    public class BeamClient
    {
        [DataMember]
        public byte Id;

        [DataMember]
        public string Name;

        [DataMember]
        public bool Available;
    }
}