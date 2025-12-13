using Unity.Collections;
using Unity.Netcode;

namespace BossFight2D.Quiz
{
    public struct QuestionPayload : INetworkSerializable
    {
        public FixedString512Bytes Prompt;
        public int OptionsCount;
        public FixedString128Bytes Option1;
        public FixedString128Bytes Option2;
        public FixedString128Bytes Option3;
        public FixedString128Bytes Option4;


        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Prompt);
            serializer.SerializeValue(ref OptionsCount);
            serializer.SerializeValue(ref Option1);
            serializer.SerializeValue(ref Option2);
            serializer.SerializeValue(ref Option3);
            serializer.SerializeValue(ref Option4);
        }
    }
}