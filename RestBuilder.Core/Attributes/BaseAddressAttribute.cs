using System;

namespace RestBuilder.Core.Attributes;

/// <summary>
/// Initialises a new instance of the <see cref="BaseAddressAttribute"/> class with the given base address
/// </summary>
/// <param name="baseAddress">Base path to use</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class BaseAddressAttribute(string baseAddress) : Attribute
{
	/// <summary>
	/// Gets the base address set in this attribute
	/// </summary>
	public string BaseAddress { get; } = baseAddress;
}
