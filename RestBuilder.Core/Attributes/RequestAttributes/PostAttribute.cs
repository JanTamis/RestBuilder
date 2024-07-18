using System;
using System.Net.Http;

namespace RestBuilder.Core.Attributes;

/// <summary>
/// Post request
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class PostAttribute : RequestAttribute
{
	/// <summary>
	/// Initialises a new instance of the <see cref="PostAttribute"/> class
	/// </summary>
	public PostAttribute() : base(HttpMethod.Post) { }

	/// <summary>
	/// Initialises a new instance of the <see cref="PostAttribute"/> class, with the given relative path
	/// </summary>
	/// <param name="path">Relative path to use</param>
	public PostAttribute(string path) : base(HttpMethod.Post, path) { }
}
