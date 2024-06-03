using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Atom.Serialization
{
    public static class StructTools
    {
        /// <summary>
        /// converts byte[] to struct
        /// </summary>
        public static T RawDeserialize<T>(byte[] rawData, int position)
        {
            int rawsize = Marshal.SizeOf(typeof(T));
            if (rawsize > rawData.Length - position)
                throw new ArgumentException("Not enough data to fill struct. Array length from position: " + (rawData.Length - position) + ", Struct length: " + rawsize);
            IntPtr buffer = Marshal.AllocHGlobal(rawsize);
            Marshal.Copy(rawData, position, buffer, rawsize);
            T retobj = (T)Marshal.PtrToStructure(buffer, typeof(T));
            Marshal.FreeHGlobal(buffer);
            return retobj;
        }

        /// <summary>
        /// converts a struct to byte[]
        /// </summary>
        public static byte[] RawSerialize(object anything)
        {
            int rawSize = Marshal.SizeOf(anything);
            IntPtr buffer = Marshal.AllocHGlobal(rawSize);
            Marshal.StructureToPtr(anything, buffer, false);
            byte[] rawDatas = new byte[rawSize];
            Marshal.Copy(buffer, rawDatas, 0, rawSize);
            Marshal.FreeHGlobal(buffer);
            return rawDatas;
        }
    }

    /// <summary>
    /// Represent a field/property of a serialized type
    /// If the AtomMemberTypes is Object, then the RecursiveBinder won't be null and will be a collection of primitive types or other recursive 
    /// </summary>
    public class MemberSerializationData
    {
        private const string _int16 = "System.Int16";
        private const string _uint16 = "System.UInt16";
        private const string _int32 = "System.Int32";
        private const string _uint32 = "System.UInt32";
        private const string _int64 = "System.Int64";
        private const string _uint64 = "System.UInt64";
        private const string _byte = "System.Byte";
        private const string _sbyte = "System.SByte";
        private const string _float = "System.Single";
        private const string _double = "System.Double";
        private const string _decimal = "System.Decimal";
        private const string _bool = "System.Boolean";
        private const string _char = "System.Char";
        private const string _string = "System.String";
        private const string _enum = "System.Enum";
        private const string _object = "System.Object";
        private const string _dateTime = "System.DateTime";
        private const string _vector3 = "UnityEngine.Vector3";
        private Type _objectType = typeof(object);

        public bool IsCollection;
        public bool IsArray;
        public int CollectionLength = -1;

        public AtomMemberTypes AtomMemberType { get; set; }

        /// <summary>
        /// for non primitive types, a serialization binder is recursively created
        /// </summary>
        public GenericAtomSerializer GenericRecursiveBinder { get; set; } = null;

        public int fixedByteLength { get; private set; } = -1;
        public int fixedParamsLength { get; private set; } = -1;
        public bool isDynamicSize { get; private set; } = false;
        private byte[] _tempBytes;

        private Func<object, int> _getCollectionLengthDelegate;
        //CreateLambdaFieldGetter
        public MemberSerializationData(Type arg_type)
        {
            if (arg_type == typeof(string))
            {
                AtomMemberType = AtomMemberTypes.String;
            }
            else if (arg_type.IsArray)
            {
                IsArray = true;
                var elementType = arg_type.GetElementType();
                AtomMemberType = setMemberType(elementType);
                var props = arg_type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                _getCollectionLengthDelegate = (Func<object, int>)DelegateHelper.GetLambdaPropertyGetter<int>(props[5]); // Length property of ICOllection 
            }
            else if (typeof(ICollection).IsAssignableFrom(arg_type))
            {
                IsCollection = true;

                var gen_args = arg_type.GetGenericArguments(); // use this...
                if (gen_args != null)
                {
                    AtomMemberType = setMemberType(gen_args[0]);
                    var props = arg_type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    _getCollectionLengthDelegate = (Func<object, int>)DelegateHelper.GetLambdaPropertyGetter<int>(props[1]); // Count property of ICOllection 
                }
                else throw new Exception("No type found in collection.");
            }
            else
            {
                AtomMemberType = setMemberType(arg_type);
            }

            if (AtomMemberType == AtomMemberTypes.Object)
            {
                // recursive serializer doesn't need identifiers as they are 'anonymous' in the sense
                // that we won't ever need to access to it directly from the SerializerClass
                GenericRecursiveBinder = new GenericAtomSerializer(arg_type);

                fixedByteLength = GenericRecursiveBinder.byteLenght;
                isDynamicSize = GenericRecursiveBinder.isDynamicSize;
            }
            else
            {
                fixedByteLength = getFixeTypesLength();
                isDynamicSize = fixedByteLength == -1;
            }
        }

        public void Write(object obj, ref byte[] _buffer, ref int writeIndex)
        {
            if (!IsCollection && !IsArray)
            {
                _writeInternal(obj, _buffer, ref writeIndex);
                return;
            }

            // hardcast to a short, we never want collections that big
            // should be someway limited explicitely 

            var flength = _getCollectionLengthDelegate(obj);
            if (flength > short.MaxValue)
                throw new Exception($"Collection value exceed {short.MaxValue}. As collection length is encoded on 2 bytes, this is not possible to send a collection that size. " +
                    $"That is not a good way to serialize packet this big anyway.");

            var length = (short)flength;
            var lengthbytes = BitConverter.GetBytes(length);

            // loop for two bytes is slower than direct assignation
            // as we know its a short (2 bytes), we just do this way
            _buffer[writeIndex] = lengthbytes[0];
            _buffer[writeIndex + 1] = lengthbytes[1];
            writeIndex += 2;

            if (IsArray)
            {
                var as_array = ((Array)obj);
                foreach (var v in as_array)
                {
                    _writeInternal(v, _buffer, ref writeIndex);
                }
            }
            else
            {
                var as_icol = (ICollection)obj;
                foreach (var v in as_icol)
                {
                    _writeInternal(v, _buffer, ref writeIndex);
                }
            }

        }

        internal void _writeInternal(object obj, byte[] _buffer, ref int writeIndex)
        {
            switch (AtomMemberType)
            {
                case AtomMemberTypes.Byte:
                case AtomMemberTypes.SByte:
                    _buffer[writeIndex] = (byte)obj;
                    writeIndex++;
                    return;

                case AtomMemberTypes.Short:
                    _tempBytes = BitConverter.GetBytes((short)obj);
                    break;
                case AtomMemberTypes.UShort:
                    _tempBytes = BitConverter.GetBytes((ushort)obj);
                    break;
                case AtomMemberTypes.Int:
                    _tempBytes = BitConverter.GetBytes((int)obj);
                    break;
                case AtomMemberTypes.UInt:
                    _tempBytes = BitConverter.GetBytes((uint)obj);
                    break;
                case AtomMemberTypes.Long:
                    _tempBytes = BitConverter.GetBytes((long)(obj));
                    break;
                case AtomMemberTypes.ULong:
                    _tempBytes = BitConverter.GetBytes((ulong)obj);
                    break;
                case AtomMemberTypes.Float:
                    _tempBytes = BitConverter.GetBytes((float)obj);
                    break;
                case AtomMemberTypes.Double:
                    _tempBytes = BitConverter.GetBytes((double)obj);
                    break;
                case AtomMemberTypes.Bool:
                    _tempBytes = BitConverter.GetBytes((bool)obj);
                    break;
                case AtomMemberTypes.Char:
                    _tempBytes = BitConverter.GetBytes((char)obj);
                    break;
                case AtomMemberTypes.DateTime:
                    var strdt = ((DateTime)obj).ToString();
                    short stringdtlength = (short)ASCIIEncoding.ASCII.GetByteCount(strdt);
                    var stringdtlengthbytes = BitConverter.GetBytes(stringdtlength);
                    var stringdtbytes = Encoding.ASCII.GetBytes(strdt);

                    _tempBytes = new byte[2 + stringdtlength];
                    // fastest solution (more alloc)
                    Buffer.BlockCopy(stringdtlengthbytes, 0, _tempBytes, 0, stringdtlengthbytes.Length);
                    Buffer.BlockCopy(stringdtbytes, 0, _tempBytes, stringdtlengthbytes.Length, stringdtbytes.Length);

                    break;
                case AtomMemberTypes.String:
                    var str = (string)obj;
                    short stringlength = (short)ASCIIEncoding.ASCII.GetByteCount(str);
                    var stringlengthbytes = BitConverter.GetBytes(stringlength);
                    var stringbytes = Encoding.ASCII.GetBytes((string)obj);

                    _tempBytes = new byte[2 + stringlength];
                    // fastest solution (more alloc)
                    Buffer.BlockCopy(stringlengthbytes, 0, _tempBytes, 0, stringlengthbytes.Length);
                    Buffer.BlockCopy(stringbytes, 0, _tempBytes, stringlengthbytes.Length, stringbytes.Length);
                    /*
                                        // least allocation solution 
                                        _tempBytes = stringlengthbytes.Concat(stringbytes).ToArray();*/
                    break;
                case AtomMemberTypes.Decimal:
                    _tempBytes = new byte[] { decimal.ToByte((decimal)obj) };
                    break;
                case AtomMemberTypes.Enum:
                    _tempBytes = BitConverter.GetBytes((int)obj);
                    break;
                case AtomMemberTypes.Object:
                    _tempBytes = GenericRecursiveBinder.Serialize(obj);
                    break;
                case AtomMemberTypes.Vector3:
                    _tempBytes = StructTools.RawSerialize(obj); 
                    break;
            }

            for (int i = 0; i < _tempBytes.Length; ++i)
            {
                _buffer[writeIndex++] = _tempBytes[i];
            }
        }

        public dynamic Read(ref byte[] _buffer, ref int readIndex)
        {
            if (!IsCollection && !IsArray)
            {
                return _readInternal(_buffer, ref readIndex);
            }

            int lenght = BitConverter.ToInt16(_buffer, readIndex); 
            readIndex += 2;

            if (IsArray)
            {
                var array = new object[lenght];
                for(int i = 0; i < lenght; ++i)
                {
                    array[i] = _readInternal(_buffer, ref readIndex);
                }

                return array;
            }
            else
            {
                var icol = new List<object>(lenght);
                for (int i = 0; i < lenght; ++i)
                {
                    icol.Add(_readInternal(_buffer, ref readIndex));
                }

                return icol;
            }
        }

        private object _readInternal(byte[] _buffer, ref int readIndex)
        {
            int _oldReadIndex = readIndex;
            switch (AtomMemberType)
            {
                case AtomMemberTypes.Byte:
                case AtomMemberTypes.SByte:
                    readIndex++;
                    return _buffer[_oldReadIndex];

                case AtomMemberTypes.Short:
                    readIndex += 2;
                    return BitConverter.ToInt16(_buffer, _oldReadIndex);

                case AtomMemberTypes.UShort:
                    readIndex += 2;
                    return BitConverter.ToUInt16(_buffer, _oldReadIndex);
                case AtomMemberTypes.Int:
                    readIndex += 4;
                    return BitConverter.ToInt32(_buffer, _oldReadIndex);
                case AtomMemberTypes.UInt:
                    readIndex += 4;
                    return BitConverter.ToUInt32(_buffer, _oldReadIndex);
                case AtomMemberTypes.Long:
                    readIndex += 8;
                    return BitConverter.ToInt64(_buffer, _oldReadIndex);
                case AtomMemberTypes.ULong:
                    readIndex += 8;
                    return BitConverter.ToUInt64(_buffer, _oldReadIndex);
                case AtomMemberTypes.Float:
                    readIndex += 4;
                    return BitConverter.ToSingle(_buffer, _oldReadIndex);
                case AtomMemberTypes.Double:
                    readIndex += 8;
                    return BitConverter.ToDouble(_buffer, _oldReadIndex);
                case AtomMemberTypes.Bool:
                    readIndex += 1;
                    return BitConverter.ToBoolean(_buffer, _oldReadIndex);
                case AtomMemberTypes.Char:
                    readIndex += 2;
                    return BitConverter.ToChar(_buffer, _oldReadIndex);
                case AtomMemberTypes.DateTime:
                    var strdtbyteLength = BitConverter.ToInt16(_buffer, readIndex);
                    readIndex += 2;
                    _oldReadIndex = readIndex;
                    readIndex += strdtbyteLength;
                    var dtstr = Encoding.ASCII.GetString(_buffer, _oldReadIndex, strdtbyteLength);
                    return DateTime.Parse(dtstr);
                case AtomMemberTypes.String:
                    var strbyteLength = BitConverter.ToInt16(_buffer, readIndex);
                    readIndex += 2;
                    _oldReadIndex = readIndex;
                    readIndex += strbyteLength;
                    return Encoding.ASCII.GetString(_buffer, _oldReadIndex, strbyteLength);
                case AtomMemberTypes.Decimal:
                    throw new NotImplementedException("who would synchronize decimals over a network ? we are not launching rockets to neptune");
                case AtomMemberTypes.Enum:
                    readIndex += 2;
                    return BitConverter.ToUInt16(_buffer, _oldReadIndex);
                case AtomMemberTypes.Object:
                    readIndex += 2;
                    return BitConverter.ToUInt16(_buffer, _oldReadIndex);
                case AtomMemberTypes.Vector3:
                    readIndex += 12;
                    return StructTools.RawDeserialize<Vector3>(_buffer, _oldReadIndex);
            }

            throw new NotImplementedException("Serializer can't read type " + AtomMemberType);
        }

        #region member type handling
        public AtomMemberTypes setMemberType(Type type)
        {
            if (type.BaseType == _objectType)
                return AtomMemberTypes.Object;

            string tString = type.FullName;
            switch (tString)
            {
                case _byte: return AtomMemberTypes.Byte;
                case _sbyte: return AtomMemberTypes.SByte;
                case _int16: return AtomMemberTypes.Short;
                case _uint16: return AtomMemberTypes.UShort;
                case _int32: return AtomMemberTypes.Int;
                case _uint32: return AtomMemberTypes.UInt;
                case _int64: return AtomMemberTypes.Long;
                case _uint64: return AtomMemberTypes.ULong;
                case _float: return AtomMemberTypes.Float;
                case _double: return AtomMemberTypes.Double;
                case _decimal: return AtomMemberTypes.Decimal;
                case _bool: return AtomMemberTypes.Bool;
                case _char: return AtomMemberTypes.Char;
                case _string: return AtomMemberTypes.String;
                case _enum: return AtomMemberTypes.Enum;
                case _object: return AtomMemberTypes.Object;
                case _dateTime: return AtomMemberTypes.DateTime;
                case _vector3: return AtomMemberTypes.Vector3;
            }

            throw new Exception($"Type not implemented exception {type}");
        }

        public int getFixeTypesLength()
        {
            if(IsCollection || IsArray)
                return -1;

            switch (AtomMemberType)
            {
                case AtomMemberTypes.Byte: return 1;
                case AtomMemberTypes.SByte: return 1;
                case AtomMemberTypes.Short: return 2;
                case AtomMemberTypes.UShort: return 2;
                case AtomMemberTypes.Int: return 4;
                case AtomMemberTypes.UInt: return 4;
                case AtomMemberTypes.Long: return 8;
                case AtomMemberTypes.ULong: return 8;
                case AtomMemberTypes.Float: return 4;
                case AtomMemberTypes.Double: return 8;
                case AtomMemberTypes.Bool: return 1;
                case AtomMemberTypes.Char: return 2;
                case AtomMemberTypes.Decimal: return 24;
                case AtomMemberTypes.Enum: return 4;
                case AtomMemberTypes.Vector3: return 12;
            }

            return -1;
        }
        #endregion

    }
}


/*
 using AtomServer.Transport;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace AtomServer
{
    public class TcpPacket : IDisposable, INetworkMessage
    {
        public PacketTypes packetType { get; protected set; }
        public ulong MessageID { get; set; }

        protected List<byte> buffer;
        protected byte[] readableBuffer;
        protected int readPos;

        protected const int headerLength = 13; // 1 (packetType) + 4 (length) + 8 (messageID)

        public TcpPacket(TcpPacket packet)
        {
            this.packetType = packet.packetType;
            this.MessageID = MessageID;
            this.buffer = new List<byte>();
            this.buffer.AddRange(packet.buffer);
            this.readPos = packet.readPos;
        }

        /// <summary>Creates a new packet with a given ID. Used for sending.</summary>
        /// <param name="_id">The packet ID.</param>
        public TcpPacket(int _id)
        {
            packetType = (PacketTypes)_id;
            MessageID = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray());
            buffer = new List<byte>(); // Initialize buffer
            readPos = 0; // Set readPos to 0

            Write((byte)_id); // Write packet id to the buffer
            Write(MessageID);
        }

        /// <summary>Creates a packet from which data can be read. Used for receiving.</summary>
        /// <param name="_data">The bytes to add to the packet.</param>
        public TcpPacket(byte[] _data)
        {
            packetType = PacketTypes.None;

            buffer = new List<byte>(); // Initialize buffer
            readPos = headerLength; // Set readPos to 5 (byte type + int length = 5 bytes + messageID)

            SetBytes(_data);
        }

        public PacketTypes ReadType()
        {
            return (PacketTypes)readableBuffer[0];
        }

        public int ReadLenght()
        {
            return BitConverter.ToInt32(readableBuffer, 1);
        }

        public ulong ReadID()
        {
            MessageID = BitConverter.ToUInt64(readableBuffer, 5); // Convert the bytes to a long
            return MessageID;
        }

        /// <summary>
        /// Offseting the read position
        /// </summary>
        /// <param name="length"></param>
        public void Offset(int length)
        {
            readPos += length;
        }

        #region Functions
        /// <summary>Sets the packet's content and prepares it to be read.</summary>
        /// <param name="_data">The bytes to add to the packet.</param>
        public void SetBytes(byte[] _data)
        {
            Write(_data);
            readableBuffer = buffer.ToArray();
        }

        /// <summary>Inserts the length of the packet's content at the start of the buffer.</summary>
        public void WriteLength()
        {
            buffer.InsertRange(1, BitConverter.GetBytes(buffer.Count + 4)); // Insert the byte length of the packet at the very beginning
        }

        /// <summary>Inserts the given int at the start of the buffer.</summary>
        /// <param name="_value">The int to insert.</param>
        public void InsertInt(int _value)
        {
            buffer.InsertRange(0, BitConverter.GetBytes(_value)); // Insert the int at the start of the buffer
        }

        /// <summary>Gets the packet's content in array form.</summary>
        public byte[] ToArray()
        {
            readableBuffer = buffer.ToArray();
            return readableBuffer;
        }

        /// <summary>Gets the length of the packet's content.</summary>
        public int Length()
        {
            return buffer.Count; // Return the length of buffer
        }

        /// <summary>Gets the length of the unread data contained in the packet.</summary>
        public int UnreadLength()
        {
            return Length() - readPos; // Return the remaining length (unread)
        }

        /// <summary>Resets the packet instance to allow it to be reused.</summary>
        /// <param name="_shouldReset">Whether or not to reset the packet.</param>
        public void Reset(bool _shouldReset = true)
        {
            if (_shouldReset)
            {
                buffer.Clear(); // Clear buffer
                readableBuffer = null;
                readPos = 0; // Reset readPos
            }
            else
            {
                readPos -= 4; // "Unread" the last read int
            }
        }
        #endregion

        #region Write Data
        /// <summary>Adds a byte to the packet.</summary>
        /// <param name="_value">The byte to add.</param>
        public void Write(byte _value)
        {
            buffer.Add(_value);
        }
        /// <summary>Adds an array of bytes to the packet.</summary>
        /// <param name="_value">The byte array to add.</param>
        public void Write(byte[] _value)
        {
            buffer.AddRange(_value);
        }
        /// <summary>Adds a short to the packet.</summary>
        /// <param name="_value">The short to add.</param>
        public void Write(short _value)
        {
            buffer.AddRange(BitConverter.GetBytes(_value));
        }
        /// <summary>Adds an int to the packet.</summary>
        /// <param name="_value">The int to add.</param>
        public void Write(int _value)
        {
            buffer.AddRange(BitConverter.GetBytes(_value));
        }
        /// <summary>Adds a long to the packet.</summary>
        /// <param name="_value">The long to add.</param>
        public void Write(long _value)
        {
            buffer.AddRange(BitConverter.GetBytes(_value));
        }

        public void Write(ulong _value)
        {
            buffer.AddRange(BitConverter.GetBytes(_value));
        }
        /// <summary>Adds a float to the packet.</summary>
        /// <param name="_value">The float to add.</param>
        public void Write(float _value)
        {
            buffer.AddRange(BitConverter.GetBytes(_value));
        }
        /// <summary>Adds a bool to the packet.</summary>
        /// <param name="_value">The bool to add.</param>
        public void Write(bool _value)
        {
            buffer.AddRange(BitConverter.GetBytes(_value));
        }
        /// <summary>Adds a string to the packet.</summary>
        /// <param name="_value">The string to add.</param>
        public void Write(string _value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(_value);
            Write(bytes.Length); // Add the length of the string to the packet
            buffer.AddRange(bytes); // Add the string itself
        }
        /// <summary>Adds a Vector3 to the packet.</summary>
        /// <param name="_value">The Vector3 to add.</param>
        public void Write(Vector3 _value)
        {
            Write(_value.X);
            Write(_value.Y);
            Write(_value.Z);
        }
        /// <summary>Adds a Quaternion to the packet.</summary>
        /// <param name="_value">The Quaternion to add.</param>
        public void Write(Quaternion _value)
        {
            Write(_value.X);
            Write(_value.Y);
            Write(_value.Z);
            Write(_value.W);
        }
        #endregion

        #region Read Data
        /// <summary>Reads a byte from the packet.</summary>
        /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
        public byte ReadByte(bool _moveReadPos = true)
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                byte _value = readableBuffer[readPos]; // Get the byte at readPos' position
                if (_moveReadPos)
                {
                    // If _moveReadPos is true
                    readPos += 1; // Increase readPos by 1
                }
                return _value; // Return the byte
            }
            else
            {
                throw new Exception("Could not read value of type 'byte'!");
            }
        }

        /// <summary>Reads an array of bytes from the packet.</summary>
        /// <param name="_length">The length of the byte array.</param>
        /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
        public byte[] ReadBytes(int _length, bool _moveReadPos = true)
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                byte[] _value = buffer.GetRange(readPos, _length).ToArray(); // Get the bytes at readPos' position with a range of _length
                if (_moveReadPos)
                {
                    // If _moveReadPos is true
                    readPos += _length; // Increase readPos by _length
                }
                return _value; // Return the bytes
            }
            else
            {
                throw new Exception("Could not read value of type 'byte[]'!");
            }
        }

        /// <summary>Reads a short from the packet.</summary>
        /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
        public short ReadShort(bool _moveReadPos = true)
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                short _value = BitConverter.ToInt16(readableBuffer, readPos); // Convert the bytes to a short
                if (_moveReadPos)
                {
                    // If _moveReadPos is true and there are unread bytes
                    readPos += 2; // Increase readPos by 2
                }
                return _value; // Return the short
            }
            else
            {
                throw new Exception("Could not read value of type 'short'!");
            }
        }

        /// <summary>Reads an int from the packet.</summary>
        /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
        public int ReadInt(bool _moveReadPos = true)
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                int _value = BitConverter.ToInt32(readableBuffer, readPos); // Convert the bytes to an int
                if (_moveReadPos)
                {
                    // If _moveReadPos is true
                    readPos += 4; // Increase readPos by 4
                }
                return _value; // Return the int
            }
            else
            {
                throw new Exception("Could not read value of type 'int'!");
            }
        }

        /// <summary>Reads a long from the packet.</summary>
        /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
        public long ReadLong(bool _moveReadPos = true)
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                long _value = BitConverter.ToInt64(readableBuffer, readPos); // Convert the bytes to a long
                if (_moveReadPos)
                {
                    // If _moveReadPos is true
                    readPos += 8; // Increase readPos by 8
                }
                return _value; // Return the long
            }
            else
            {
                throw new Exception("Could not read value of type 'long'!");
            }
        }

        public ulong ReadULong(bool _moveReadPos = true)
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                ulong _value = BitConverter.ToUInt64(readableBuffer, readPos); // Convert the bytes to a long
                if (_moveReadPos)
                {
                    // If _moveReadPos is true
                    readPos += 8; // Increase readPos by 8
                }
                return _value; // Return the long
            }
            else
            {
                throw new Exception("Could not read value of type 'long'!");
            }
        }

        /// <summary>Reads a float from the packet.</summary>
        /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
        public float ReadFloat(bool _moveReadPos = true)
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                float _value = BitConverter.ToSingle(readableBuffer, readPos); // Convert the bytes to a float
                if (_moveReadPos)
                {
                    // If _moveReadPos is true
                    readPos += 4; // Increase readPos by 4
                }
                return _value; // Return the float
            }
            else
            {
                throw new Exception("Could not read value of type 'float'!");
            }
        }

        /// <summary>Reads a bool from the packet.</summary>
        /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
        public bool ReadBool(bool _moveReadPos = true)
        {
            if (buffer.Count > readPos)
            {
                // If there are unread bytes
                bool _value = BitConverter.ToBoolean(readableBuffer, readPos); // Convert the bytes to a bool
                if (_moveReadPos)
                {
                    // If _moveReadPos is true
                    readPos += 1; // Increase readPos by 1
                }
                return _value; // Return the bool
            }
            else
            {
                throw new Exception("Could not read value of type 'bool'!");
            }
        }

        /// <summary>Reads a string from the packet.</summary>
        public string ReadString()
        {
            int _length = ReadInt(); // Get the length of the string
            Console.WriteLine($"String length {_length}, readPos {readPos}, readableBufferLength {readableBuffer.Length}");

            string _value = Encoding.ASCII.GetString(readableBuffer, readPos, _length); // Convert the bytes to a string
            readPos += _length; // Increase readPos by the length of the string

            return _value; // Return the string
        }

        #endregion

        private bool disposed = false;

        protected virtual void Dispose(bool _disposing)
        {
            if (!disposed)
            {
                if (_disposing)
                {
                    buffer = null;
                    readableBuffer = null;
                    readPos = 0;
                }

                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}

 */
