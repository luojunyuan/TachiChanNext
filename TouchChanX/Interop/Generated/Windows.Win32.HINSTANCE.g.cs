﻿// ------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
// ------------------------------------------------------------------------------

#pragma warning disable CS1591,CS1573,CS0465,CS0649,CS8019,CS1570,CS1584,CS1658,CS0436,CS8981
using global::System;
using global::System.Diagnostics;
using global::System.Diagnostics.CodeAnalysis;
using global::System.Runtime.CompilerServices;
using global::System.Runtime.InteropServices;
using winmdroot = global::Windows.Win32;
namespace Windows.Win32
{
	namespace Foundation
	{
		[DebuggerDisplay("{Value}")]
		[global::System.CodeDom.Compiler.GeneratedCode("Microsoft.Windows.CsWin32", "0.3.183+73e6125f79.RR")]
		internal unsafe readonly partial struct HINSTANCE
			: IEquatable<HINSTANCE>
		{
			internal readonly void* Value;

			internal HINSTANCE(void* value) => this.Value = value;

			internal HINSTANCE(IntPtr value):this((void*)value)
			{
			}

			internal static HINSTANCE Null => default;

			internal bool IsNull => Value == default;

			public static implicit operator void*(HINSTANCE value) => value.Value;

			public static explicit operator HINSTANCE(void* value) => new HINSTANCE(value);

			public static bool operator ==(HINSTANCE left, HINSTANCE right) => left.Value == right.Value;

			public static bool operator !=(HINSTANCE left, HINSTANCE right) => !(left == right);

			public bool Equals(HINSTANCE other) => this.Value == other.Value;

			public override bool Equals(object obj) => obj is HINSTANCE other && this.Equals(other);

			public override int GetHashCode() => unchecked((int)this.Value);

			public override string ToString() => $"0x{(nuint)this.Value:x}";

			public static implicit operator IntPtr(HINSTANCE value) => new IntPtr(value.Value);

			public static explicit operator HINSTANCE(IntPtr value) => new HINSTANCE((void*)value.ToPointer());

			public static explicit operator HINSTANCE(UIntPtr value) => new HINSTANCE((void*)value.ToPointer());

			public static implicit operator HMODULE(HINSTANCE value) => new HMODULE(value.Value);
		}
	}
}
