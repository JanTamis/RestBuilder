using System;
using System.Net.Http;

namespace RestBuilder.Core.Attributes;

/// <summary>
/// Head request
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class HeadAttribute : RequestAttribute
{
	/// <summary>
	/// Initialises a new instance of the <see cref="HeadAttribute"/> class
	/// </summary>
	public HeadAttribute() : base(HttpMethod.Head) { }

	/// <summary>
	/// Initialises a new instance of the <see cref="HeadAttribute"/> class, with the given relative path
	/// </summary>
	/// <param name="path">Relative path to use</param>
	public HeadAttribute(string path) : base(HttpMethod.Head, path) { }
}
