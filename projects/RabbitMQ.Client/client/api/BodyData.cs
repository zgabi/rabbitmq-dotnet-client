using System;
using System.Buffers;

namespace RabbitMQ.Client
{
    public struct BodyData
    {
        public ReadOnlySpan<byte> Body => BodyMemory.Span;

        internal ReadOnlyMemory<byte> BodyMemory { get; private set; }

        private byte[] RentedArray { get; set; }

        private bool ShallReturn { get; set; }

        internal BodyData(ReadOnlyMemory<byte> body, byte[] rentedArray)
        {
            BodyMemory = body;
            RentedArray = rentedArray;
            ShallReturn = true;
        }

        public ReadOnlyMemory<byte> TakeoverPayload()
        {
            if (!ShallReturn)
            {
                throw new Exception("Body is already taken.");
            }

            ShallReturn = false;
            return BodyMemory;
        }

        internal void ReturnPayload()
        {
            if (ShallReturn)
            {
                ArrayPool<byte>.Shared.Return(RentedArray);
            }

            BodyMemory = default;
        }
    }
}
