using System.Runtime.InteropServices;

namespace Blazor.FileReader
{
    
    [StructLayout(LayoutKind.Explicit)]
    struct ReadFileParams
    {
        [FieldOffset(0)]
        public long TaskId;

        [FieldOffset(8)]
        public long BufferOffset;

        [FieldOffset(16)]
        public int Count;

        [FieldOffset(20)]
        public int FileRef;

        [FieldOffset(24)]
        public long Position;

        [FieldOffset(32)]
        public byte[] Buffer;
    }
}
