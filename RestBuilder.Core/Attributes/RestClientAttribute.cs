using System;

namespace RestBuilder.Core.Attributes;

/// <summary>
/// Initialises a new instance of the <see cref="RestClientAttribute"/> class with the given name
/// </summary>
/// <param name="name">Name to use</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class RestClientAttribute(string name) : Attribute
{
	/// <summary>
	/// Gets the name set in this attribute
	/// </summary>
	public string Name { get; } = name;
}
