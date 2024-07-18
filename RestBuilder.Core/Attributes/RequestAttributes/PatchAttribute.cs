using System;
using System.Net.Http;

namespace RestBuilder.Core.Attributes;

/// <summary>
/// Patch request
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class PatchAttribute : RequestAttribute
{
	/// <summary>
	/// Gets a static instance of <see cref="HttpMethod"/> corresponding to a PATCH request
	/// </summary>
	public static HttpMethod PatchMethod { get; } = new HttpMethod("PATCH");

	/// <summary>
	/// Initialises a new instance of the <see cref="PatchAttribute"/> class
	/// </summary>
	public PatchAttribute() : base(PatchMethod) { }

	/// <summary>
	/// Initialises a new instance of the <see cref="PatchAttribute"/> class, with the given relativ epath
	/// </summary>
	/// <param name="path">Relative path to use</param>
	public PatchAttribute(string path) : base(PatchMethod, path) { }
}
