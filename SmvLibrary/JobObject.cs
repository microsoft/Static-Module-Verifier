using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SmvLibrary
{
    public class JobObject : IDisposable
    {

        const int JOB_OBJECT_MSG_PROCESS_MEMORY_LIMIT = 9;
        const int JOB_OBJECT_LIMIT_PROCESS_TIME = 2;
        const int COMPKEY_JOBOBJECT = 1;
        const int COMPKEY_TERMINATE = 0;

        IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        public IntPtr handle { get; set; }

        private bool disposed = false;

        /// <summary>
        /// IO Completion Port
        /// </summary>
        IntPtr hiocp;

        /// <summary>
        /// Thread for completion function
        /// </summary>
        uint hThread;


        /// <summary>
        /// Dispose the job object
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose and close the job object
        /// </summary>
        /// <param name="disposing"></param>
        private void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing){ }

            Close();
            disposed = true;
        }

        /// <summary>
        /// Close the job object handle
        /// </summary>
        public void Close()
        {
            NativeMethods.CloseHandle(handle);
            handle = IntPtr.Zero;
        }

        /// <summary>
        /// Add process to job object using handle
        /// </summary>
        /// <param name="processHandle"></param>
        /// <returns></returns>
        public bool AddProcess(IntPtr processHandle)
        {
            return NativeMethods.AssignProcessToJobObject(handle, processHandle);
        }

        /// <summary>
        /// Add process to job object using processId
        /// </summary>
        /// <param name="processId"></param>
        /// <returns></returns>
        public bool AddProcess(int processId)
        {
            return AddProcess(Process.GetProcessById(processId).Handle);
        }

        /// <summary>
        /// Configuring the required data structures
        /// </summary>
        private void configure()
        {
            uint dwThreadId;
            UIntPtr dwThreadParam = new UIntPtr(0);
            hThread = NativeMethods.CreateThread(UIntPtr.Zero, 0, completionThreadFunction, dwThreadParam, 0, out dwThreadId);
            handle = NativeMethods.CreateJobObject(IntPtr.Zero, null);
            hiocp = NativeMethods.CreateIoCompletionPort(INVALID_HANDLE_VALUE, IntPtr.Zero, new UIntPtr(0), 0);
        }


        /// <summary>
        /// Associating completion port to handle
        /// </summary>
        private void associateCompletionPort()
        {
            JOBOBJECT_ASSOCIATE_COMPLETION_PORT joacp = new JOBOBJECT_ASSOCIATE_COMPLETION_PORT();
            joacp.CompletionKey = new IntPtr(COMPKEY_JOBOBJECT);
            joacp.CompletionPort = hiocp;

            int joacpLength = Marshal.SizeOf(typeof(JOBOBJECT_ASSOCIATE_COMPLETION_PORT));
            IntPtr joacpPtr = Marshal.AllocHGlobal(joacpLength);
            Marshal.StructureToPtr(joacp, joacpPtr, false);

            if (!NativeMethods.SetInformationJobObject(handle, JobObjectInfoType.AssociateCompletionPortInformation, joacpPtr, (uint)joacpLength))
            {
                throw new Exception("Cannot set object to completion port class " + NativeMethods.GetLastError());
            }
        }
        /// <summary>
        /// Creates job object with ProcessMemoryLimit as maxMemory
        /// </summary>
        /// <param name="maxMemory"></param>
        public void setMaxMemory(int maxMemory, int maxTime)
        {
            //Configuring the data structures
            configure();

            //Associating port to handle
            associateCompletionPort();

            //Setting process memory limit
            JOBOBJECT_BASIC_LIMIT_INFORMATION info = new JOBOBJECT_BASIC_LIMIT_INFORMATION();
            //2 - close processes on job object close, 1 - process memory limit, 2 - process time limit 
            info.LimitFlags = 0x2102;
            //converting time from ticks to milliseconds
            info.PerProcessUserTimeLimit = maxTime * 10000;
            JOBOBJECT_EXTENDED_LIMIT_INFORMATION extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            extendedInfo.BasicLimitInformation = info;
            extendedInfo.ProcessMemoryLimit = new UIntPtr((uint)maxMemory);
            int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
            Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);

            if (!NativeMethods.SetInformationJobObject(handle, JobObjectInfoType.ExtendedLimitInformation, extendedInfoPtr, (uint)length))
            {
                throw new Exception("Cannot set object to extended limit info class " + NativeMethods.GetLastError());
            }

        }

        private void completionThreadFunction()
        {
            bool fDone = false;
            while (!fDone)
            {
                uint dwBytesXferred;
                UIntPtr compKey;
                IntPtr po;
                NativeMethods.GetQueuedCompletionStatus(hiocp, out dwBytesXferred, out compKey, out po, Timeout.Infinite);
                int value = (int) compKey.ToUInt32();
                switch (value)
                {
                    case COMPKEY_TERMINATE: fDone = true;
                        break;

                    case COMPKEY_JOBOBJECT:
                        {
                            switch (dwBytesXferred)
                            {
                                case JOB_OBJECT_MSG_PROCESS_MEMORY_LIMIT:
                                    Log.LogError("Memory limit exceeded");
                                    fDone = true;
                                    break;
                                case JOB_OBJECT_LIMIT_PROCESS_TIME:
                                    Log.LogError("Time limit exceeded");
                                    fDone = true;
                                    break;
                            }
                            break;
                        }  
                    default:
                        fDone = false;
                        break;
                }
            }
        }

        /// <summary>
        /// Prints the PeakProcessMemoryUsed
        /// </summary>
        public void QueryExtendedLimitInformation()
        {

            JOBOBJECT_EXTENDED_LIMIT_INFORMATION extendedLimit;
            int extenedLimitLength = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr extendedLimitPtr = Marshal.AllocHGlobal(extenedLimitLength);
            try
            {
                bool success = NativeMethods.QueryInformationJobObject(this.handle, JobObjectInfoType.ExtendedLimitInformation, extendedLimitPtr, (uint)extenedLimitLength, IntPtr.Zero);
                if (success == false)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new Win32Exception(error, "QueryInformationJobObject failed.");
                }
                extendedLimit = (JOBOBJECT_EXTENDED_LIMIT_INFORMATION)Marshal.PtrToStructure(extendedLimitPtr, typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                ulong peakProcessMemory = (ulong)extendedLimit.PeakProcessMemoryUsed;
                Log.LogDebug("Peak process memory :" + peakProcessMemory);
            }
            finally
            {
                Marshal.FreeHGlobal(extendedLimitPtr);
            }
        }
    }


    struct JOBOBJECT_ASSOCIATE_COMPLETION_PORT
    {
        public IntPtr CompletionKey;
        public IntPtr CompletionPort;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct IO_COUNTERS
    {
        public UInt64 ReadOperationCount;
        public UInt64 WriteOperationCount;
        public UInt64 OtherOperationCount;
        public UInt64 ReadTransferCount;
        public UInt64 WriteTransferCount;
        public UInt64 OtherTransferCount;
    }


    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public Int64 PerProcessUserTimeLimit;
        public Int64 PerJobUserTimeLimit;
        public UInt32 LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public UInt32 ActiveProcessLimit;
        public UIntPtr Affinity;
        public UInt32 PriorityClass;
        public UInt32 SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    public enum JobObjectInfoType
    {
        AssociateCompletionPortInformation = 7,
        BasicLimitInformation = 2,
        BasicUIRestrictions = 4,
        EndOfJobTimeInformation = 6,
        ExtendedLimitInformation = 9,
        SecurityLimitInformation = 5,
        GroupInformation = 11
    }
}
