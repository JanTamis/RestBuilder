using System;
using System.Net.Http;

namespace RestBuilder.Core.Attributes;

/// <summary>
/// Attribute for custom HTTP methods which aren't represented by other subclasses of RequestAttributeBase
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class RequestAttribute : Attribute
{
	/// <summary>
	/// Gets the HTTP method to use (Get/Post/etc)
	/// </summary>
	public HttpMethod Method { get; }

	/// <summary>
	/// Gets or sets the path to request.
	/// </summary>
	public string? Path { get; set; }

	/// <summary>
	/// Initialises a new instance of the <see cref="RequestAttribute"/> class, with the given HttpMethod.
	/// </summary>
	/// <remarks>
	/// Use this if there isn't a <see cref="RequestAttribute"/> subclass for the HTTP method you want to use
	/// </remarks>
	/// <param name="method">HTTP Method to use, e.g. "PATCH"</param>
	public RequestAttribute(HttpMethod method)
	{
		Method = method;
	}

	/// <summary>
	/// Initialises a new instance of the <see cref="RequestAttribute"/> class, with the given HttpMethod and relative path.
	/// </summary>
	/// <remarks>
	/// Use this if there isn't a <see cref="RequestAttribute"/> subclass for the HTTP method you want to use
	/// </remarks>
	/// <param name="method">HTTP Method to use, e.g. "PATCH"</param>
	/// <param name="path">Relative path to use</param>
	public RequestAttribute(HttpMethod method, string path)
	{
		Method = method;
		Path = path;
	}
}
