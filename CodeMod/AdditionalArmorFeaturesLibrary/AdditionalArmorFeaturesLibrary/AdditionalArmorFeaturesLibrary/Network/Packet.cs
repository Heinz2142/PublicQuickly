using ProtoBuf;

namespace AdditionalArmorFeaturesLibrary.Network
{

    [ProtoContract]
    public sealed class AdditionalArmorFeaturesLibraryPacket
    {
        [ProtoMember(1)]
        public int ItemSlot { get; set; }
    }

}