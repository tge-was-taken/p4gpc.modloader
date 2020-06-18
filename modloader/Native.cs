// Copied & adapted from https://github.com/Sewer56/AfsFsRedir.ReloadedII

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace modloader
{
    public unsafe class Native
    {
        // ntioapi.h
        public const uint FILE_USE_FILE_POINTER_POSITION = 0xfffffffe;


        /// <summary>
        /// Creates a new file or directory, or opens an existing file, device, directory, or volume.
        /// (The description here is a partial, lazy copy from MSDN)
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [Reloaded.Hooks.Definitions.X64.Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
        [Reloaded.Hooks.Definitions.X86.Function(Reloaded.Hooks.Definitions.X86.CallingConventions.Stdcall)]
        public delegate NtStatus NtCreateFile(out IntPtr handle, FileAccess access, ref OBJECT_ATTRIBUTES objectAttributes, 
            ref IO_STATUS_BLOCK ioStatus, ref long allocSize, uint fileAttributes, FileShare share, uint createDisposition, uint createOptions,
            IntPtr eaBuffer, uint eaLength);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [Reloaded.Hooks.Definitions.X64.Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
        [Reloaded.Hooks.Definitions.X86.Function(Reloaded.Hooks.Definitions.X86.CallingConventions.Stdcall)]
        public delegate NtStatus NtReadFile(IntPtr handle, IntPtr hEvent, IntPtr* apcRoutine, IntPtr* apcContext, 
            ref IO_STATUS_BLOCK ioStatus, byte* buffer, uint length, LARGE_INTEGER* byteOffset, IntPtr key);


        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [Reloaded.Hooks.Definitions.X64.Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
        [Reloaded.Hooks.Definitions.X86.Function(Reloaded.Hooks.Definitions.X86.CallingConventions.Stdcall)]
        public delegate NtStatus SetFilePointer(IntPtr hFile, int liDistanceToMove, IntPtr lpNewFilePointer, uint dwMoveMethod);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [Reloaded.Hooks.Definitions.X64.Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
        [Reloaded.Hooks.Definitions.X86.Function(Reloaded.Hooks.Definitions.X86.CallingConventions.Stdcall)]
        public delegate NtStatus NtSetInformationFile(IntPtr hFile, out IO_STATUS_BLOCK ioStatusBlock, void* fileInformation, uint length, FileInformationClass fileInformationClass);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [Reloaded.Hooks.Definitions.X64.Function(Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft)]
        [Reloaded.Hooks.Definitions.X86.Function(Reloaded.Hooks.Definitions.X86.CallingConventions.Stdcall)]
        public delegate NtStatus NtQueryInformationFile(IntPtr hFile, out IO_STATUS_BLOCK ioStatusBlock, void* fileInformation, uint length, FileInformationClass fileInformationClass);

        [UnmanagedFunctionPointer( CallingConvention.Cdecl )]
        [Reloaded.Hooks.Definitions.X64.Function( Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft )]
        [Reloaded.Hooks.Definitions.X86.Function( Reloaded.Hooks.Definitions.X86.CallingConventions.Stdcall )]
        public delegate void FreeConsoleDelegate();

        [UnmanagedFunctionPointer( CallingConvention.Cdecl )]
        [Reloaded.Hooks.Definitions.X64.Function( Reloaded.Hooks.Definitions.X64.CallingConventions.Microsoft )]
        [Reloaded.Hooks.Definitions.X86.Function( Reloaded.Hooks.Definitions.X86.CallingConventions.Stdcall )]
        public delegate bool CloseHandleDelegate( IntPtr handle );

        /// <summary>
        /// A driver sets an IRP's I/O status block to indicate the final status of an I/O request, before calling IoCompleteRequest for the IRP.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct IO_STATUS_BLOCK
        {
            public UInt32 Status;
            public IntPtr Information;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FILE_STANDARD_INFORMATION
        {
            public long AllocationSize;
            public long EndOfFile;
            public uint NumberOfLinks;
            public bool DeletePending;
            public bool Directory;
        }

        /// <summary>
        /// The OBJECT_ATTRIBUTES structure specifies attributes that can be applied to objects or object
        /// handles by routines that create objects and/or return handles to objects.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct OBJECT_ATTRIBUTES : IDisposable
        {
            /// <summary>
            /// Lengthm of this structure.
            /// </summary>
            public int Length;

            /// <summary>
            /// Optional handle to the root object directory for the path name specified by the ObjectName member.
            /// If RootDirectory is NULL, ObjectName must point to a fully qualified object name that includes the full path to the target object.
            /// If RootDirectory is non-NULL, ObjectName specifies an object name relative to the RootDirectory directory.
            /// The RootDirectory handle can refer to a file system directory or an object directory in the object manager namespace.
            /// </summary>
            public IntPtr RootDirectory;

            /// <summary>
            /// Pointer to a Unicode string that contains the name of the object for which a handle is to be opened.
            /// This must either be a fully qualified object name, or a relative path name to the directory specified by the RootDirectory member.
            /// </summary>
            private IntPtr objectName;

            /// <summary>
            /// Bitmask of flags that specify object handle attributes. This member can contain one or more of the flags in the following table (See MSDN)
            /// </summary>
            public uint Attributes;

            /// <summary>
            /// Specifies a security descriptor (SECURITY_DESCRIPTOR) for the object when the object is created.
            /// If this member is NULL, the object will receive default security settings.
            /// </summary>
            public IntPtr SecurityDescriptor;

            /// <summary>
            /// Optional quality of service to be applied to the object when it is created.
            /// Used to indicate the security impersonation level and context tracking mode (dynamic or static).
            /// Currently, the InitializeObjectAttributes macro sets this member to NULL.
            /// </summary>
            public IntPtr SecurityQualityOfService;

            /// <summary>
            /// Gets or sets the file path of the files loaded in or out.
            /// </summary>
            public unsafe UNICODE_STRING ObjectName
            {
                get => *(UNICODE_STRING*)objectName;

                set
                {
                    // Check if we need to delete old memory.
                    bool fDeleteOld = objectName != IntPtr.Zero;

                    // Allocates the necessary bytes for the string.
                    if (!fDeleteOld)
                        objectName = Marshal.AllocHGlobal(Marshal.SizeOf(value));

                    // Deallocate old string while writing the new one.
                    Marshal.StructureToPtr(value, objectName, fDeleteOld);
                }
            }

            /// <summary>
            /// Disposes of the actual object name (file name) in question.
            /// </summary>
            public void Dispose()
            {
                if (objectName != IntPtr.Zero)
                {
                    Marshal.DestroyStructure(objectName, typeof(UNICODE_STRING));
                    Marshal.FreeHGlobal(objectName);
                    objectName = IntPtr.Zero;
                }
            }
        }


        /// <summary>
        /// Does this really need to be explained to you?
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct UNICODE_STRING : IDisposable
        {
            public ushort Length;
            public ushort MaximumLength;
            private IntPtr buffer;

            public UNICODE_STRING(string s)
            {
                Length = (ushort)(s.Length * 2);
                MaximumLength = (ushort)(Length + 2);
                buffer = Marshal.StringToHGlobalUni(s);
            }

            /// <summary>
            /// Disposes of the current file name assigned to this Unicode String.
            /// </summary>
            public void Dispose()
            {
                Marshal.FreeHGlobal(buffer);
                buffer = IntPtr.Zero;
            }

            /// <summary>
            /// Returns a string with the contents
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                try
                {
                    if (buffer != IntPtr.Zero)
                    {
                        Reloaded.Memory.Sources.Memory.CurrentProcess.ReadRaw(buffer, out byte[] uniString, Length);
                        return Encoding.Unicode.GetString(uniString);
                    }

                    return "";
                }
                catch { return ""; }
            }
        }

        [StructLayout( LayoutKind.Explicit, Size = 8 )]
        public struct LARGE_INTEGER
        {
            [FieldOffset(0)]public Int64 QuadPart;
            [FieldOffset(0)]public UInt32 LowPart;
            [FieldOffset(4)]public Int32 HighPart;
        }


        /// <summary>
        ///     Enumeration of the various file information classes.
        ///     See wdm.h.
        /// </summary>
        public enum FileInformationClass
        {
            None = 0,
            FileDirectoryInformation = 1,
            FileFullDirectoryInformation, // 2
            FileBothDirectoryInformation, // 3
            FileBasicInformation, // 4
            FileStandardInformation, // 5
            FileInternalInformation, // 6
            FileEaInformation, // 7
            FileAccessInformation, // 8
            FileNameInformation, // 9
            FileRenameInformation, // 10
            FileLinkInformation, // 11
            FileNamesInformation, // 12
            FileDispositionInformation, // 13
            FilePositionInformation, // 14
            FileFullEaInformation, // 15
            FileModeInformation, // 16
            FileAlignmentInformation, // 17
            FileAllInformation, // 18
            FileAllocationInformation, // 19
            FileEndOfFileInformation, // 20
            FileAlternateNameInformation, // 21
            FileStreamInformation, // 22
            FilePipeInformation, // 23
            FilePipeLocalInformation, // 24
            FilePipeRemoteInformation, // 25
            FileMailslotQueryInformation, // 26
            FileMailslotSetInformation, // 27
            FileCompressionInformation, // 28
            FileObjectIdInformation, // 29
            FileCompletionInformation, // 30
            FileMoveClusterInformation, // 31
            FileQuotaInformation, // 32
            FileReparsePointInformation, // 33
            FileNetworkOpenInformation, // 34
            FileAttributeTagInformation, // 35
            FileTrackingInformation, // 36
            FileIdBothDirectoryInformation, // 37
            FileIdFullDirectoryInformation, // 38
            FileValidDataLengthInformation, // 39
            FileShortNameInformation, // 40
            FileIoCompletionNotificationInformation, // 41
            FileIoStatusBlockRangeInformation, // 42
            FileIoPriorityHintInformation, // 43
            FileSfioReserveInformation, // 44
            FileSfioVolumeInformation, // 45
            FileHardLinkInformation, // 46
            FileProcessIdsUsingFileInformation, // 47
            FileNormalizedNameInformation, // 48
            FileNetworkPhysicalNameInformation, // 49
            FileIdGlobalTxDirectoryInformation, // 50
            FileIsRemoteDeviceInformation, // 51
            FileAttributeCacheInformation, // 52
            FileNumaNodeInformation, // 53
            FileStandardLinkInformation, // 54
            FileRemoteProtocolInformation, // 55
            FileMaximumInformation,
        }

        public enum NtStatus : uint
        {
            // Success
            Success = 0x00000000,
            Wait0 = 0x00000000,
            Wait1 = 0x00000001,
            Wait2 = 0x00000002,
            Wait3 = 0x00000003,
            Wait63 = 0x0000003f,
            Abandoned = 0x00000080,
            AbandonedWait0 = 0x00000080,
            AbandonedWait1 = 0x00000081,
            AbandonedWait2 = 0x00000082,
            AbandonedWait3 = 0x00000083,
            AbandonedWait63 = 0x000000bf,
            UserApc = 0x000000c0,
            KernelApc = 0x00000100,
            Alerted = 0x00000101,
            Timeout = 0x00000102,
            Pending = 0x00000103,
            Reparse = 0x00000104,
            MoreEntries = 0x00000105,
            NotAllAssigned = 0x00000106,
            SomeNotMapped = 0x00000107,
            OpLockBreakInProgress = 0x00000108,
            VolumeMounted = 0x00000109,
            RxActCommitted = 0x0000010a,
            NotifyCleanup = 0x0000010b,
            NotifyEnumDir = 0x0000010c,
            NoQuotasForAccount = 0x0000010d,
            PrimaryTransportConnectFailed = 0x0000010e,
            PageFaultTransition = 0x00000110,
            PageFaultDemandZero = 0x00000111,
            PageFaultCopyOnWrite = 0x00000112,
            PageFaultGuardPage = 0x00000113,
            PageFaultPagingFile = 0x00000114,
            CrashDump = 0x00000116,
            ReparseObject = 0x00000118,
            NothingToTerminate = 0x00000122,
            ProcessNotInJob = 0x00000123,
            ProcessInJob = 0x00000124,
            ProcessCloned = 0x00000129,
            FileLockedWithOnlyReaders = 0x0000012a,
            FileLockedWithWriters = 0x0000012b,

            // Informational
            Informational = 0x40000000,
            ObjectNameExists = 0x40000000,
            ThreadWasSuspended = 0x40000001,
            WorkingSetLimitRange = 0x40000002,
            ImageNotAtBase = 0x40000003,
            RegistryRecovered = 0x40000009,

            // Warning
            Warning = 0x80000000,
            GuardPageViolation = 0x80000001,
            DatatypeMisalignment = 0x80000002,
            Breakpoint = 0x80000003,
            SingleStep = 0x80000004,
            BufferOverflow = 0x80000005,
            NoMoreFiles = 0x80000006,
            HandlesClosed = 0x8000000a,
            PartialCopy = 0x8000000d,
            DeviceBusy = 0x80000011,
            InvalidEaName = 0x80000013,
            EaListInconsistent = 0x80000014,
            NoMoreEntries = 0x8000001a,
            LongJump = 0x80000026,
            DllMightBeInsecure = 0x8000002b,

            // Error
            Error = 0xc0000000,
            Unsuccessful = 0xc0000001,
            NotImplemented = 0xc0000002,
            InvalidInfoClass = 0xc0000003,
            InfoLengthMismatch = 0xc0000004,
            AccessViolation = 0xc0000005,
            InPageError = 0xc0000006,
            PagefileQuota = 0xc0000007,
            InvalidHandle = 0xc0000008,
            BadInitialStack = 0xc0000009,
            BadInitialPc = 0xc000000a,
            InvalidCid = 0xc000000b,
            TimerNotCanceled = 0xc000000c,
            InvalidParameter = 0xc000000d,
            NoSuchDevice = 0xc000000e,
            NoSuchFile = 0xc000000f,
            InvalidDeviceRequest = 0xc0000010,
            EndOfFile = 0xc0000011,
            WrongVolume = 0xc0000012,
            NoMediaInDevice = 0xc0000013,
            NoMemory = 0xc0000017,
            NotMappedView = 0xc0000019,
            UnableToFreeVm = 0xc000001a,
            UnableToDeleteSection = 0xc000001b,
            IllegalInstruction = 0xc000001d,
            AlreadyCommitted = 0xc0000021,
            AccessDenied = 0xc0000022,
            BufferTooSmall = 0xc0000023,
            ObjectTypeMismatch = 0xc0000024,
            NonContinuableException = 0xc0000025,
            BadStack = 0xc0000028,
            NotLocked = 0xc000002a,
            NotCommitted = 0xc000002d,
            InvalidParameterMix = 0xc0000030,
            ObjectNameInvalid = 0xc0000033,
            ObjectNameNotFound = 0xc0000034,
            ObjectNameCollision = 0xc0000035,
            ObjectPathInvalid = 0xc0000039,
            ObjectPathNotFound = 0xc000003a,
            ObjectPathSyntaxBad = 0xc000003b,
            DataOverrun = 0xc000003c,
            DataLate = 0xc000003d,
            DataError = 0xc000003e,
            CrcError = 0xc000003f,
            SectionTooBig = 0xc0000040,
            PortConnectionRefused = 0xc0000041,
            InvalidPortHandle = 0xc0000042,
            SharingViolation = 0xc0000043,
            QuotaExceeded = 0xc0000044,
            InvalidPageProtection = 0xc0000045,
            MutantNotOwned = 0xc0000046,
            SemaphoreLimitExceeded = 0xc0000047,
            PortAlreadySet = 0xc0000048,
            SectionNotImage = 0xc0000049,
            SuspendCountExceeded = 0xc000004a,
            ThreadIsTerminating = 0xc000004b,
            BadWorkingSetLimit = 0xc000004c,
            IncompatibleFileMap = 0xc000004d,
            SectionProtection = 0xc000004e,
            EasNotSupported = 0xc000004f,
            EaTooLarge = 0xc0000050,
            NonExistentEaEntry = 0xc0000051,
            NoEasOnFile = 0xc0000052,
            EaCorruptError = 0xc0000053,
            FileLockConflict = 0xc0000054,
            LockNotGranted = 0xc0000055,
            DeletePending = 0xc0000056,
            CtlFileNotSupported = 0xc0000057,
            UnknownRevision = 0xc0000058,
            RevisionMismatch = 0xc0000059,
            InvalidOwner = 0xc000005a,
            InvalidPrimaryGroup = 0xc000005b,
            NoImpersonationToken = 0xc000005c,
            CantDisableMandatory = 0xc000005d,
            NoLogonServers = 0xc000005e,
            NoSuchLogonSession = 0xc000005f,
            NoSuchPrivilege = 0xc0000060,
            PrivilegeNotHeld = 0xc0000061,
            InvalidAccountName = 0xc0000062,
            UserExists = 0xc0000063,
            NoSuchUser = 0xc0000064,
            GroupExists = 0xc0000065,
            NoSuchGroup = 0xc0000066,
            MemberInGroup = 0xc0000067,
            MemberNotInGroup = 0xc0000068,
            LastAdmin = 0xc0000069,
            WrongPassword = 0xc000006a,
            IllFormedPassword = 0xc000006b,
            PasswordRestriction = 0xc000006c,
            LogonFailure = 0xc000006d,
            AccountRestriction = 0xc000006e,
            InvalidLogonHours = 0xc000006f,
            InvalidWorkstation = 0xc0000070,
            PasswordExpired = 0xc0000071,
            AccountDisabled = 0xc0000072,
            NoneMapped = 0xc0000073,
            TooManyLuidsRequested = 0xc0000074,
            LuidsExhausted = 0xc0000075,
            InvalidSubAuthority = 0xc0000076,
            InvalidAcl = 0xc0000077,
            InvalidSid = 0xc0000078,
            InvalidSecurityDescr = 0xc0000079,
            ProcedureNotFound = 0xc000007a,
            InvalidImageFormat = 0xc000007b,
            NoToken = 0xc000007c,
            BadInheritanceAcl = 0xc000007d,
            RangeNotLocked = 0xc000007e,
            DiskFull = 0xc000007f,
            ServerDisabled = 0xc0000080,
            ServerNotDisabled = 0xc0000081,
            TooManyGuidsRequested = 0xc0000082,
            GuidsExhausted = 0xc0000083,
            InvalidIdAuthority = 0xc0000084,
            AgentsExhausted = 0xc0000085,
            InvalidVolumeLabel = 0xc0000086,
            SectionNotExtended = 0xc0000087,
            NotMappedData = 0xc0000088,
            ResourceDataNotFound = 0xc0000089,
            ResourceTypeNotFound = 0xc000008a,
            ResourceNameNotFound = 0xc000008b,
            ArrayBoundsExceeded = 0xc000008c,
            FloatDenormalOperand = 0xc000008d,
            FloatDivideByZero = 0xc000008e,
            FloatInexactResult = 0xc000008f,
            FloatInvalidOperation = 0xc0000090,
            FloatOverflow = 0xc0000091,
            FloatStackCheck = 0xc0000092,
            FloatUnderflow = 0xc0000093,
            IntegerDivideByZero = 0xc0000094,
            IntegerOverflow = 0xc0000095,
            PrivilegedInstruction = 0xc0000096,
            TooManyPagingFiles = 0xc0000097,
            FileInvalid = 0xc0000098,
            InstanceNotAvailable = 0xc00000ab,
            PipeNotAvailable = 0xc00000ac,
            InvalidPipeState = 0xc00000ad,
            PipeBusy = 0xc00000ae,
            IllegalFunction = 0xc00000af,
            PipeDisconnected = 0xc00000b0,
            PipeClosing = 0xc00000b1,
            PipeConnected = 0xc00000b2,
            PipeListening = 0xc00000b3,
            InvalidReadMode = 0xc00000b4,
            IoTimeout = 0xc00000b5,
            FileForcedClosed = 0xc00000b6,
            ProfilingNotStarted = 0xc00000b7,
            ProfilingNotStopped = 0xc00000b8,
            NotSameDevice = 0xc00000d4,
            FileRenamed = 0xc00000d5,
            CantWait = 0xc00000d8,
            PipeEmpty = 0xc00000d9,
            CantTerminateSelf = 0xc00000db,
            InternalError = 0xc00000e5,
            InvalidParameter1 = 0xc00000ef,
            InvalidParameter2 = 0xc00000f0,
            InvalidParameter3 = 0xc00000f1,
            InvalidParameter4 = 0xc00000f2,
            InvalidParameter5 = 0xc00000f3,
            InvalidParameter6 = 0xc00000f4,
            InvalidParameter7 = 0xc00000f5,
            InvalidParameter8 = 0xc00000f6,
            InvalidParameter9 = 0xc00000f7,
            InvalidParameter10 = 0xc00000f8,
            InvalidParameter11 = 0xc00000f9,
            InvalidParameter12 = 0xc00000fa,
            MappedFileSizeZero = 0xc000011e,
            TooManyOpenedFiles = 0xc000011f,
            Cancelled = 0xc0000120,
            CannotDelete = 0xc0000121,
            InvalidComputerName = 0xc0000122,
            FileDeleted = 0xc0000123,
            SpecialAccount = 0xc0000124,
            SpecialGroup = 0xc0000125,
            SpecialUser = 0xc0000126,
            MembersPrimaryGroup = 0xc0000127,
            FileClosed = 0xc0000128,
            TooManyThreads = 0xc0000129,
            ThreadNotInProcess = 0xc000012a,
            TokenAlreadyInUse = 0xc000012b,
            PagefileQuotaExceeded = 0xc000012c,
            CommitmentLimit = 0xc000012d,
            InvalidImageLeFormat = 0xc000012e,
            InvalidImageNotMz = 0xc000012f,
            InvalidImageProtect = 0xc0000130,
            InvalidImageWin16 = 0xc0000131,
            LogonServer = 0xc0000132,
            DifferenceAtDc = 0xc0000133,
            SynchronizationRequired = 0xc0000134,
            DllNotFound = 0xc0000135,
            IoPrivilegeFailed = 0xc0000137,
            OrdinalNotFound = 0xc0000138,
            EntryPointNotFound = 0xc0000139,
            ControlCExit = 0xc000013a,
            PortNotSet = 0xc0000353,
            DebuggerInactive = 0xc0000354,
            CallbackBypass = 0xc0000503,
            PortClosed = 0xc0000700,
            MessageLost = 0xc0000701,
            InvalidMessage = 0xc0000702,
            RequestCanceled = 0xc0000703,
            RecursiveDispatch = 0xc0000704,
            LpcReceiveBufferExpected = 0xc0000705,
            LpcInvalidConnectionUsage = 0xc0000706,
            LpcRequestsNotAllowed = 0xc0000707,
            ResourceInUse = 0xc0000708,
            ProcessIsProtected = 0xc0000712,
            VolumeDirty = 0xc0000806,
            FileCheckedOut = 0xc0000901,
            CheckOutRequired = 0xc0000902,
            BadFileType = 0xc0000903,
            FileTooLarge = 0xc0000904,
            FormsAuthRequired = 0xc0000905,
            VirusInfected = 0xc0000906,
            VirusDeleted = 0xc0000907,
            TransactionalConflict = 0xc0190001,
            InvalidTransaction = 0xc0190002,
            TransactionNotActive = 0xc0190003,
            TmInitializationFailed = 0xc0190004,
            RmNotActive = 0xc0190005,
            RmMetadataCorrupt = 0xc0190006,
            TransactionNotJoined = 0xc0190007,
            DirectoryNotRm = 0xc0190008,
            CouldNotResizeLog = 0xc0190009,
            TransactionsUnsupportedRemote = 0xc019000a,
            LogResizeInvalidSize = 0xc019000b,
            RemoteFileVersionMismatch = 0xc019000c,
            CrmProtocolAlreadyExists = 0xc019000f,
            TransactionPropagationFailed = 0xc0190010,
            CrmProtocolNotFound = 0xc0190011,
            TransactionSuperiorExists = 0xc0190012,
            TransactionRequestNotValid = 0xc0190013,
            TransactionNotRequested = 0xc0190014,
            TransactionAlreadyAborted = 0xc0190015,
            TransactionAlreadyCommitted = 0xc0190016,
            TransactionInvalidMarshallBuffer = 0xc0190017,
            CurrentTransactionNotValid = 0xc0190018,
            LogGrowthFailed = 0xc0190019,
            ObjectNoLongerExists = 0xc0190021,
            StreamMiniversionNotFound = 0xc0190022,
            StreamMiniversionNotValid = 0xc0190023,
            MiniversionInaccessibleFromSpecifiedTransaction = 0xc0190024,
            CantOpenMiniversionWithModifyIntent = 0xc0190025,
            CantCreateMoreStreamMiniversions = 0xc0190026,
            HandleNoLongerValid = 0xc0190028,
            NoTxfMetadata = 0xc0190029,
            LogCorruptionDetected = 0xc0190030,
            CantRecoverWithHandleOpen = 0xc0190031,
            RmDisconnected = 0xc0190032,
            EnlistmentNotSuperior = 0xc0190033,
            RecoveryNotNeeded = 0xc0190034,
            RmAlreadyStarted = 0xc0190035,
            FileIdentityNotPersistent = 0xc0190036,
            CantBreakTransactionalDependency = 0xc0190037,
            CantCrossRmBoundary = 0xc0190038,
            TxfDirNotEmpty = 0xc0190039,
            IndoubtTransactionsExist = 0xc019003a,
            TmVolatile = 0xc019003b,
            RollbackTimerExpired = 0xc019003c,
            TxfAttributeCorrupt = 0xc019003d,
            EfsNotAllowedInTransaction = 0xc019003e,
            TransactionalOpenNotAllowed = 0xc019003f,
            TransactedMappingUnsupportedRemote = 0xc0190040,
            TxfMetadataAlreadyPresent = 0xc0190041,
            TransactionScopeCallbacksNotSet = 0xc0190042,
            TransactionRequiredPromotion = 0xc0190043,
            CannotExecuteFileInTransaction = 0xc0190044,
            TransactionsNotFrozen = 0xc0190045,

            MaximumNtStatus = 0xffffffff
        }

        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr LoadLibraryW([MarshalAs(UnmanagedType.LPWStr)] string lpFileName);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [SuppressUnmanagedCodeSecurity]
        [DllImport( "kernel32.dll" )]
        public static extern bool FreeConsole();
    }
}
