using System;
using System.IO;
using System.Runtime.InteropServices;
using Reloaded.Hooks.Definitions;
using Reloaded.Memory.Sources;

namespace modloader.Hooking
{
	public static unsafe class ImportAddressTableHooker
	{
		public static IHook<TFunction> Hook<TFunction>( IReloadedHooks hookFactory,
			string libraryName, string functionName, TFunction function )
		{
			const int IMAGE_DIRECTORY_ENTRY_IMPORT = 1;
			var cleanLibraryName = Path.GetFileNameWithoutExtension(libraryName);

			var imageBase = GetModuleHandle(null);
			var dosHeaders = (IMAGE_DOS_HEADERS*)imageBase;
			var ntHeaders = (IMAGE_NT_HEADERS32*)(imageBase + dosHeaders->e_lfanew);

			var importsDirectory = ntHeaders->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
			var importDescriptor = (IMAGE_IMPORT_DESCRIPTOR*)(importsDirectory.VirtualAddress + (uint)imageBase);

			while ( importDescriptor->Name != 0 )
			{
				var importLibraryName = new string((sbyte*)((long)imageBase + importDescriptor->Name));
				var cleanImportLibraryName = Path.GetFileNameWithoutExtension(importLibraryName);

				if ( cleanImportLibraryName.Equals( cleanLibraryName, StringComparison.OrdinalIgnoreCase ) )
				{
					var importLibrary = LoadLibrary(importLibraryName);

					if ( importLibrary != IntPtr.Zero )
					{
						var originalFirstThunk = (IMAGE_THUNK_DATA32*)((long)imageBase + importDescriptor->OriginalFirstThunk);
						var firstThunk = (IMAGE_THUNK_DATA32*)((long)imageBase + importDescriptor->FirstThunk);

						while ( originalFirstThunk->AddressOfData != 0 )
						{
							var importFunctionName = (IMAGE_IMPORT_BY_NAME*)((long)imageBase + originalFirstThunk->AddressOfData);

							if ( importFunctionName->Name == functionName )
							{
								return new IndirectHook<TFunction>( new IntPtr( &firstThunk->Function ), function, hookFactory);
							}

							++originalFirstThunk;
							++firstThunk;
						}
					}
				}

				++importDescriptor;
			}

			return null;
		}

		private class IndirectHook<TFunction> : IHook<TFunction>
		{
			private readonly IntPtr mAddressToFunctionPointer;

			public bool IsHookEnabled { get; private set; }
			public bool IsHookActivated { get; private set; }
			public TFunction OriginalFunction { get; }
			public IntPtr OriginalFunctionAddress { get; }
			public IntPtr OriginalFunctionWrapperAddress { get; }
			public IReverseWrapper<TFunction> ReverseWrapper { get; }

			public IndirectHook(IntPtr addressToFunctionPointer, TFunction function, IReloadedHooks hookFactory)
			{
				mAddressToFunctionPointer = addressToFunctionPointer;
				OriginalFunctionAddress = *( IntPtr* )mAddressToFunctionPointer;
				OriginalFunction = hookFactory.CreateWrapper<TFunction>( (long) OriginalFunctionAddress, out IntPtr originalFunctionWrapperAddress );
				OriginalFunctionWrapperAddress = OriginalFunctionAddress;
                ReverseWrapper = hookFactory.CreateReverseWrapper(function);
			}

			public IHook<TFunction> Activate()
			{
				IsHookActivated = true;
				Enable();
				return this;
			}

			public void Disable()
			{
				Memory.CurrentProcess.SafeWrite( mAddressToFunctionPointer, OriginalFunctionAddress );
				IsHookEnabled = false;
			}

			public void Enable()
			{
				Memory.CurrentProcess.SafeWrite( mAddressToFunctionPointer, ReverseWrapper.WrapperPointer );
				IsHookEnabled = true;
			}
		}

		[StructLayout( LayoutKind.Sequential, Pack = 1 )]
		private struct IMAGE_IMPORT_BY_NAME
		{
			public short Hint;
			public fixed byte NameBytes[1];

			public string Name
			{
				get
				{
					fixed ( byte* pNameBytes = NameBytes )
						return new string( ( sbyte* )pNameBytes );
				}
			}
		}

		[StructLayout( LayoutKind.Explicit )]
		private struct IMAGE_THUNK_DATA32
		{
			[FieldOffset(0)]
			public uint ForwarderString;

			[FieldOffset(0)]
			public uint Function;

			[FieldOffset(0)]
			public uint Ordinal;

			[FieldOffset(0)]
			public uint AddressOfData;
		}

		[StructLayout( LayoutKind.Sequential, Pack = 1 )]
		private struct IMAGE_IMPORT_DESCRIPTOR
		{
			public uint OriginalFirstThunk;
			public uint TimeDateStamp;
			public uint ForwarderChain;
			public uint Name;
			public uint FirstThunk;
		}

		[StructLayout( LayoutKind.Sequential, Pack = 1 )]
		private struct IMAGE_FILE_HEADER
		{
			public ushort Machine;
			public ushort NumberOfSections;
			public uint TimeDateStamp;
			public uint PointerToSymbolTable;
			public uint NumberOfSymbols;
			public ushort SizeOfOptionalHeader;
			public ushort Characteristics;
		}

		[StructLayout( LayoutKind.Sequential, Pack = 1 )]
		public struct IMAGE_DATA_DIRECTORY
		{
			public const int SIZE = 8;

			public uint VirtualAddress;
			public uint Size;
		}

		[StructLayout( LayoutKind.Sequential, Pack = 1 )]
		private struct IMAGE_OPTIONAL_HEADER32
		{
			public const int IMAGE_NUMBEROF_DIRECTORY_ENTRIES = 16;

			public ushort Magic;
			public byte MajorLinkerVersion;
			public byte MinorLinkerVersion;
			public uint SizeOfCode;
			public uint SizeOfInitializedData;
			public uint SizeOfUninitializedData;
			public uint AddressOfEntryPoint;
			public uint BaseOfCode;
			public uint BaseOfData;
			public uint ImageBase;
			public uint SectionAlignment;
			public uint FileAlignment;
			public ushort MajorOperatingSystemVersion;
			public ushort MinorOperatingSystemVersion;
			public ushort MajorImageVersion;
			public ushort MinorImageVersion;
			public ushort MajorSubsystemVersion;
			public ushort MinorSubsystemVersion;
			public uint Win32VersionValue;
			public uint SizeOfImage;
			public uint SizeOfHeaders;
			public uint CheckSum;
			public ushort Subsystem;
			public ushort DllCharacteristics;
			public uint SizeOfStackReserve;
			public uint SizeOfStackCommit;
			public uint SizeOfHeapReserve;
			public uint SizeOfHeapCommit;
			public uint LoaderFlags;
			public uint NumberOfRvaAndSizes;
			public fixed byte DataDirectoryBytes[IMAGE_NUMBEROF_DIRECTORY_ENTRIES * IMAGE_DATA_DIRECTORY.SIZE];

			public IMAGE_DATA_DIRECTORY* DataDirectory
			{
				get
				{
					fixed ( byte* pDataDirectoryBytes = DataDirectoryBytes )
						return ( IMAGE_DATA_DIRECTORY* )pDataDirectoryBytes;
				}
			}
		}

		[StructLayout( LayoutKind.Sequential, Pack = 1 )]
		private struct IMAGE_NT_HEADERS32
		{
			public uint Signature;
			public IMAGE_FILE_HEADER FileHeader;
			public IMAGE_OPTIONAL_HEADER32 OptionalHeader;
		}

		[StructLayout( LayoutKind.Sequential, Pack = 1 )]
		private unsafe struct IMAGE_DOS_HEADERS
		{
			public fixed byte e_magic_byte[2];       // Magic number
			public UInt16 e_cblp;    // Bytes on last page of file
			public UInt16 e_cp;      // Pages in file
			public UInt16 e_crlc;    // Relocations
			public UInt16 e_cparhdr;     // Size of header in paragraphs
			public UInt16 e_minalloc;    // Minimum extra paragraphs needed
			public UInt16 e_maxalloc;    // Maximum extra paragraphs needed
			public UInt16 e_ss;      // Initial (relative) SS value
			public UInt16 e_sp;      // Initial SP value
			public UInt16 e_csum;    // Checksum
			public UInt16 e_ip;      // Initial IP value
			public UInt16 e_cs;      // Initial (relative) CS value
			public UInt16 e_lfarlc;      // File address of relocation table
			public UInt16 e_ovno;    // Overlay number
			public fixed UInt16 e_res1[4];    // Reserved words
			public UInt16 e_oemid;       // OEM identifier (for e_oeminfo)
			public UInt16 e_oeminfo;     // OEM information; e_oemid specific
			public fixed UInt16 e_res2[10];    // Reserved words
			public Int32 e_lfanew;      // File address of new exe header
		}

		[DllImport( "kernel32.dll" )]
		private static extern IntPtr GetModuleHandle( string lpModuleName );

		[DllImport( "kernel32.dll", CharSet = CharSet.Ansi )]
		private static extern IntPtr LoadLibrary( [MarshalAs( UnmanagedType.LPStr )] string lpFileName );
	}
}
