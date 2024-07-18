using System;
using System.Net.Http;

namespace RestBuilder.Core.Attributes;

/// <summary>
/// Delete request
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class GetAttribute : RequestAttribute
{
	/// <summary>
	/// Initialises a new instance of the <see cref="DeleteAttribute"/> class
	/// </summary>
	public GetAttribute() : base(HttpMethod.Get) { }

	/// <summary>
	/// Initialises a new instance of the <see cref="DeleteAttribute"/> class, with the given relative path
	/// </summary>
	/// <param name="path">Relative path to use</param>
	public GetAttribute(string path) : base(HttpMethod.Get, path) { }
}