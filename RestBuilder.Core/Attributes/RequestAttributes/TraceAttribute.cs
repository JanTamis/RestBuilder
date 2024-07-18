using System;
using System.Net.Http;

namespace RestBuilder.Core.Attributes;

/// <summary>
/// Trace request
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class TraceAttribute : RequestAttribute
{
	/// <summary>
	/// Initialises a new instance of the <see cref="TraceAttribute"/> class
	/// </summary>
	public TraceAttribute() : base(HttpMethod.Trace) { }

	/// <summary>
	/// Initialises a new instance of the <see cref="TraceAttribute"/> class, with the given relative path
	/// </summary>
	/// <param name="path">Relative path to use</param>
	public TraceAttribute(string path) : base(HttpMethod.Trace, path) { }
}
