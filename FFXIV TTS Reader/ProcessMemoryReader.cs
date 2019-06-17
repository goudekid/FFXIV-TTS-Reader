using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace FFXIV_TTS_Reader
{
    class ProcessMemoryReader
    {
        const int PROCESS_VM_READ = 0x0010;

        public ProcessMemoryReader()
        {

        }

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess, Int64 lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        /// <summary>
        /// Read up memory of a process via up to five level pointer
        /// </summary>
        /// <param name="process"> Process whose memory is being retrieved</param>
        /// <param name="baseAddress"></param>
        /// <param name="bufferSizeBytes"> How many bytes you want the byte array returned to contain</param>
        /// <param name="offset1"></param>
        /// <param name="offset2"></param>
        /// <param name="offset3"></param>
        /// <param name="offset4"></param>
        /// <param name="offset5"></param>
        /// <returns></returns>
        public static byte[] GetBytesAtAddress(Process process, long baseAddress, int bufferSizeBytes, int? offset1 = null, int? offset2 = null, int? offset3 = null, int? offset4 = null, int? offset5 = null)
        {
            byte[] pointerBuffer = new byte[8];
            byte[] buffer = new byte[bufferSizeBytes];
            int bytesRead = 0;
            IntPtr processHandle = OpenProcess(PROCESS_VM_READ, false, process.Id);

            if (offset1 != null)
            {
                ReadProcessMemory((int)processHandle, baseAddress, pointerBuffer, pointerBuffer.Length, ref bytesRead);
                Int64 baseValue = BitConverter.ToInt64(pointerBuffer, 0);
                Int64 firstAddress = baseValue + (Int32)offset1;

                if (offset2 != null)
                {
                    ReadProcessMemory((int)processHandle, firstAddress, pointerBuffer, pointerBuffer.Length, ref bytesRead);
                    Int64 firstValue = BitConverter.ToInt64(pointerBuffer, 0);
                    Int64 secondAddress = firstValue + (Int32)offset2;
                    if (offset3 != null)
                    {
                        ReadProcessMemory((int)processHandle, secondAddress, pointerBuffer, pointerBuffer.Length, ref bytesRead);
                        Int64 secondValue = BitConverter.ToInt64(pointerBuffer, 0);
                        Int64 thirdAddress = secondValue + (Int32)offset3;

                        if (offset4 != null)
                        {
                            ReadProcessMemory((int)processHandle, thirdAddress, pointerBuffer, pointerBuffer.Length, ref bytesRead);
                            Int64 thirdValue = BitConverter.ToInt64(pointerBuffer, 0);
                            Int64 fourthAddress = thirdValue + (Int32)offset4;

                            if (offset5 != null)
                            {
                                ReadProcessMemory((int)processHandle, fourthAddress, pointerBuffer, pointerBuffer.Length, ref bytesRead);
                                Int64 fourthValue = BitConverter.ToInt64(pointerBuffer, 0);
                                Int64 fifthAddress = fourthValue + (Int32)offset5;

                                ReadProcessMemory((int)processHandle, fifthAddress, buffer, buffer.Length, ref bytesRead);
                                return buffer;
                            }
                            else
                            {
                                ReadProcessMemory((int)processHandle, fourthAddress, buffer, buffer.Length, ref bytesRead);
                                return buffer;
                            }
                        }
                        else
                        {
                            ReadProcessMemory((int)processHandle, thirdAddress, buffer, buffer.Length, ref bytesRead);
                            return buffer;
                        }
                    }
                    else
                    {
                        ReadProcessMemory((int)processHandle, secondAddress, buffer, buffer.Length, ref bytesRead);
                        return buffer;
                    }
                }
                else
                {
                    ReadProcessMemory((int)processHandle, firstAddress, buffer, buffer.Length, ref bytesRead);
                    return buffer;
                }
            }
            else
            {
                ReadProcessMemory((int)processHandle, baseAddress, buffer, buffer.Length, ref bytesRead);
                return buffer;
            }

        }
    }
}
