using System;
using System.Text;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Caches a string's byte representation to be used for certain methods like IModel.BasicPublish/>.
    /// </summary>
    public struct CachedString
    {
        public static readonly CachedString Empty = new CachedString(string.Empty, ReadOnlyMemory<byte>.Empty);

        /// <summary>
        /// The string value to cache.
        /// </summary>
        public string Value
        {
            get
            {
                if (_value == null)
                {
                    var bytes = _bytes.Value;
#if !NETSTANDARD
                    _value = Encoding.UTF8.GetString(bytes.Span);
#else
                    unsafe
                    {
                        fixed (byte* bytePointer = bytes.Span)
                        {
                            _value = Encoding.UTF8.GetString(bytePointer, bytes.Length);
                        }
                    }
#endif
                }

                return _value;
            }
        }

        /// <summary>
        /// Gets the bytes representing the <see cref="Value"/>.
        /// </summary>
        public ReadOnlyMemory<byte> Bytes
        {
            get
            {
                _bytes ??= Encoding.UTF8.GetBytes(Value);
                return _bytes.Value;
            }
        }

        private string _value;
        
        private ReadOnlyMemory<byte>? _bytes;

        /// <summary>
        /// Creates a new <see cref="CachedString"/> based on the provided string.
        /// </summary>
        /// <param name="value">The string to cache.</param>
        public CachedString(string value)
        {
            _value = value;
            _bytes = null;
        }

        /// <summary>
        /// Creates a new <see cref="CachedString"/> based on the provided bytes.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        public CachedString(ReadOnlyMemory<byte> bytes)
        {
            _value = null;
            _bytes = bytes;
        }

        /// <summary>
        /// Creates a new <see cref="CachedString"/> based on the provided values.
        /// </summary>
        /// <param name="value">The string to cache.</param>
        /// <param name="bytes">The byte representation of the string value.</param>
        private CachedString(string value, ReadOnlyMemory<byte> bytes)
        {
            _value = value;
            _bytes = bytes;
        }

        public readonly bool HasBytes => _bytes.HasValue;

        public readonly int BytesLength
        {
            get
            {
                if (_bytes.HasValue)
                {
                    return _bytes.Value.Length;
                }

                return Encoding.UTF8.GetByteCount(_value);
            }
        }

        public readonly bool Equals(in CachedString other) 
        {
            // both structs has string values
            if (other._value != null && _value != null)
            {
                return other._value == _value;
            }

            // both structs has byte values
            if (other._bytes.HasValue && _bytes.HasValue)
            {
                return other._bytes.Value.Span.SequenceEqual(_bytes.Value.Span);
            }

            // one struct has only string, another struct has only byte value
            if (_value != null)
            {
                // this struct has string value, another has bytes
                return other.Equals(_value);
            }

            // this struct has byte value, another has string
            return Equals(other._value);
        }

        public readonly bool Equals(string other)
        {
            if (other == null)
            {
                return false; // cached string is never null
            }

            if (_value != null)
            {
                return other == _value;
            }

            // this struct has only byte value
            var bytes = _bytes.Value;
            var utf8 = Encoding.UTF8;
            int length = utf8.GetByteCount(other);
            if (length != bytes.Length)
            {
                return false;
            }

            Span<byte> bytes2 = stackalloc byte[length];
            unsafe
            {

                fixed (char* chars = other)
                fixed (byte* bytesPtr = bytes2)
                {
                    utf8.GetBytes(chars, other.Length, bytesPtr, length);
                }
            }

            return bytes2.SequenceEqual(bytes.Span);
        }
    }
}
