﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace IO.Ably.CustomSerialisers {
    
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("MsgPack.Serialization.CodeDomSerializers.CodeDomSerializerBuilder", "0.6.0.0")]
    public class IO_Ably_CapabilitySerializer : MsgPack.Serialization.MessagePackSerializer<IO.Ably.Capability> {
        
        private MsgPack.Serialization.MessagePackSerializer<string> _serializer0;
        
        
        public IO_Ably_CapabilitySerializer(MsgPack.Serialization.SerializationContext context) : 
                base(context) {
            MsgPack.Serialization.PolymorphismSchema schema0 = default(MsgPack.Serialization.PolymorphismSchema);
            schema0 = null;
            this._serializer0 = context.GetSerializer<string>(schema0);
        }
        
        

        protected override void PackToCore(MsgPack.Packer packer, IO.Ably.Capability objectTree)
        {
            packer.PackString(objectTree.ToJson());
        }
        
        protected override IO.Ably.Capability UnpackFromCore(MsgPack.Unpacker unpacker)
        {
            var itemsCount = MsgPack.Serialization.UnpackHelpers.GetItemsCount(unpacker);

            if (unpacker.LastReadData.IsRaw)
            {
                var capability = unpacker.LastReadData.ToString();
                return new Capability(capability);
            }
            
            return new Capability();
        }

        private static T @__Conditional<T>(bool condition, T whenTrue, T whenFalse)
         {
            if (condition) {
                return whenTrue;
            }
            else {
                return whenFalse;
            }
        }
    }
}
