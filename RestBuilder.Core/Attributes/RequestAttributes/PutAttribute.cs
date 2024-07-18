using System;
using System.Net.Http;

namespace RestBuilder.Core.Attributes;

/// <summary>
/// Put request
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class PutAttribute : RequestAttribute
{
	/// <summary>
	/// Initialises a new instance of the <see cref="PutAttribute"/> class
	/// </summary>
	public PutAttribute() : base(HttpMethod.Put) { }

	/// <summary>
	/// Initialises a new instance of the <see cref="PutAttribute"/> class, with the given relativ epath
	/// </summary>
	/// <param name="path">Relative path to use</param>
	public PutAttribute(string path) : base(HttpMethod.Put, path) { }
}
