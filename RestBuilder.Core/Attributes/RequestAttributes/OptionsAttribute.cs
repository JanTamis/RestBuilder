using System;
using System.Net.Http;

namespace RestBuilder.Core.Attributes;

/// <summary>
/// Options request
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class OptionsAttribute : RequestAttribute
{
	/// <summary>
	/// Initialises a new instance of the <see cref="OptionsAttribute"/> class
	/// </summary>
	public OptionsAttribute() : base(HttpMethod.Options) { }

	/// <summary>
	/// Initialises a new instance of the <see cref="OptionsAttribute"/> class, with the given relative path
	/// </summary>
	/// <param name="path">Relative path to use</param>
	public OptionsAttribute(string path) : base(HttpMethod.Options, path) { }
}
