namespace RestBuilder;

public static class Literals
{
	public const string BaseAddressAttribute = "BaseAddressAttribute";
	public const string HttpClient = "Client";
	public const string BaseNamespace = "RestBuilder";

	public const string AttributeSourceCode = $$"""
		using System;

		#nullable enable

		namespace {{BaseNamespace}};

		[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
		public sealed class {{BaseAddressAttribute}} : Attribute
		{
			/// <summary>
			/// Gets the base address set in this attribute
			/// </summary>
			public string BaseAddress { get; }
			
			/// <summary>
			/// Initialises a new instance of the <see cref="{{BaseAddressAttribute}}"/> class with the given base address
			/// </summary>
			/// <param name="baseAddress">Base path to use</param>
			public {{BaseAddressAttribute}}(string baseAddress)
			{
				BaseAddress = baseAddress;
			}
		}
		""";

	public const string RequestAttributes = $$"""
		using System;
		using System.Net.Http;

		#nullable enable

		namespace {{BaseNamespace}};

		/// <summary>
		/// Base class for all request attributes
		/// </summary>
		public abstract class RequestAttributeBase : Attribute
		{
			/// <summary>
			/// Gets the HTTP method to use (Get/Post/etc)
			/// </summary>
			public HttpMethod Method { get; }
		
		  /// <summary>
		  /// Gets or sets the path to request. This is relative to the base path configured when RestService.For was called, and can contain placeholders
		  /// </summary>
			public string? Path { get; set; }
		
			public RequestAttributeBase(HttpMethod method)
			{
				Method = method;
			}
		
			public RequestAttributeBase(HttpMethod method, string path)
		  {
				Method = method;
				Path = path;
			}
		}

		/// <summary>
		/// Attribute for custom HTTP methods which aren't represented by other subclasses of RequestAttributeBase
		/// </summary>
		[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
		public sealed class RequestAttribute : RequestAttributeBase
		{
			/// <summary>
			/// Initialises a new instance of the <see cref="RequestAttribute"/> class, with the given HttpMethod.
			/// </summary>
			/// <remarks>
			/// Use this if there isn't a <see cref="RequestAttribute"/> subclass for the HTTP method you want to use
			/// </remarks>
			/// <param name="httpMethod">HTTP Method to use, e.g. "PATCH"</param>
			public RequestAttribute(string httpMethod) : base(new HttpMethod(httpMethod))
			{
			}
		
			/// <summary>
			/// Initialises a new instance of the <see cref="RequestAttribute"/> class, with the given HttpMethod and relative path.
			/// </summary>
			/// <remarks>
			/// Use this if there isn't a <see cref="RequestAttribute"/> subclass for the HTTP method you want to use
			/// </remarks>
			/// <param name="httpMethod">HTTP Method to use, e.g. "PATCH"</param>
			/// <param name="path">Relative path to use</param>
			public RequestAttribute(string httpMethod, string path) : base(new HttpMethod(httpMethod), path)
			{
			}
		}

		/// <summary>
		/// Delete request
		/// </summary>
		[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
		public sealed class DeleteAttribute : RequestAttributeBase
		{
			/// <summary>
			/// Initialises a new instance of the <see cref="DeleteAttribute"/> class
			/// </summary>
			public DeleteAttribute() : base(HttpMethod.Delete) { }
		
			/// <summary>
			/// Initialises a new instance of the <see cref="DeleteAttribute"/> class, with the given relative path
			/// </summary>
			/// <param name="path">Relative path to use</param>
			public DeleteAttribute(string path) : base(HttpMethod.Delete, path) { }
		}

		/// <summary>
		/// Get request
		/// </summary>
		[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
		public sealed class GetAttribute : RequestAttributeBase
		{
			/// <summary>
			/// Initialises a new instance of the <see cref="GetAttribute"/> class
			/// </summary>
			public GetAttribute() : base(HttpMethod.Get) { }
		
			/// <summary>
			/// Initialises a new instance of the <see cref="GetAttribute"/> class, with the given relative path
			/// </summary>
			/// <param name="path">Relative path to use</param>
			public GetAttribute(string path) : base(HttpMethod.Get, path) { }
		}

		/// <summary>
		/// Head request
		/// </summary>
		[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
		public sealed class HeadAttribute : RequestAttributeBase
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

		/// <summary>
		/// Options request
		/// </summary>
		[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
		public sealed class OptionsAttribute : RequestAttributeBase
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

		/// <summary>
		/// Post request
		/// </summary>
		[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
		public sealed class PostAttribute : RequestAttributeBase
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

		/// <summary>
		/// Put request
		/// </summary>
		[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
		public sealed class PutAttribute : RequestAttributeBase
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

		/// <summary>
		/// Trace request
		/// </summary>
		[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
		public sealed class TraceAttribute : RequestAttributeBase
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

		/// <summary>
		/// Patch request
		/// </summary>
		[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
		public sealed class PatchAttribute : RequestAttributeBase
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
		""";


	public const string QueryAttribute = $$"""
		using System;

		namespace {{BaseNamespace}};
		
		#nullable enable
		
		/// <summary>
		/// Marks a parameter as being a query param
		/// </summary>
		[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
		public sealed class QueryAttribute : Attribute
		{
			private string? _name;
	
			/// <summary>
			/// Gets or sets the name of the query param. Will use the parameter / property name if unset.
			/// </summary>
			public string? Name
			{
				get => this._name;
				set
				{
					this._name = value;
					this.HasName = true;
				}
			}
	
			/// <summary>
			/// Gets a value indicating whether the user has set the name attribute
			/// </summary>
			public bool HasName { get; private set; }
	
			/// <summary>
			/// Gets the serialization method to use to serialize the value. Defaults to QuerySerializationMethod.ToString
			/// </summary>
			public QuerySerializationMethod SerializationMethod { get; set; }
	
			/// <summary>
			/// Gets or sets the format string used to format the value
			/// </summary>
			/// <remarks>
			/// If <see cref="SerializationMethod"/> is <see cref="QuerySerializationMethod.Serialized"/>, this is passed to the serializer
			/// as <see cref="RequestQueryParamSerializerInfo.Format"/>.
			/// Otherwise, if this looks like a format string which can be passed to <see cref="string.Format(IFormatProvider, string, object[])"/>,
			/// (i.e. it contains at least one format placeholder), then this happens with the value passed as the first arg.
			/// Otherwise, if the value implements <see cref="IFormattable"/>, this is passed to the value's
			/// <see cref="IFormattable.ToString(string, IFormatProvider)"/> method. Otherwise this is ignored.
			/// Example values: "X2", "{0:X2}", "test{0}".
			/// </remarks>
			public string? Format { get; set; }
	
			/// <summary>
			/// Initialises a new instance of the <see cref="QueryAttribute"/> class
			/// </summary>
			public QueryAttribute() : this(QuerySerializationMethod.Default)
			{
			}
	
			/// <summary>
			/// Initialises a new instance of the <see cref="QueryAttribute"/> class, with the given serialization method
			/// </summary>
			/// <param name="serializationMethod">Serialization method to use to serialize the value</param>
			public QueryAttribute(QuerySerializationMethod serializationMethod)
			{
				// Don't set this.Name
				this.SerializationMethod = serializationMethod;
			}
	
			/// <summary>
			/// Initialises a new instance of the <see cref="QueryAttribute"/> class, with the given name
			/// </summary>
			/// <param name="name">Name of the query parameter</param>
			public QueryAttribute(string? name) : this(name, QuerySerializationMethod.Default)
			{
			}
	
			/// <summary>
			/// Initialises a new instance of the <see cref="QueryAttribute"/> class, with the given name and serialization method
			/// </summary>
			/// <param name="name">Name of the query parameter</param>
			/// <param name="serializationMethod">Serialization method to use to serialize the value</param>
			public QueryAttribute(string? name, QuerySerializationMethod serializationMethod)
			{
				this.Name = name;
				this.SerializationMethod = serializationMethod;
			}
		}
		""";
	
	public const string QuerySerializationMethod = $$"""
		namespace {{BaseNamespace}};

		#nullable enable
		
		/// <summary>
		/// Type of serialization that should be applied to the query parameter's value
		/// </summary>
		public enum QuerySerializationMethod
		{
			/// <summary>
			/// Serialized using its .ToString() method
			/// </summary>
			ToString,
	
			/// <summary>
			/// Serialized using the configured IRequestQueryParamSerializer (uses Json.NET by default)
			/// </summary>
			Serialized,
	
			/// <summary>
			/// Use the default serialization method. You probably don't want to specify this yourself
			/// </summary>
			Default,
		}
		""";
	
	public const string AllowAnyStatusCodeAttribute = $$"""
		using System;
		
		namespace {{BaseNamespace}};

		#nullable enable

		/// <summary>
		/// Controls whether the given method, or all methods within the given interface, will throw an exception if the response status code does not indicate success
		/// </summary>
		[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
		public sealed class AllowAnyStatusCodeAttribute : Attribute
		{
			/// <summary>
			/// Gets or sets a value indicating whether to suppress the exception normally thrown on responses that do not indicate success
			/// </summary>
			public bool AllowAnyStatusCode { get; set; }
	
			/// <summary>
			/// Initialises a new instance of the <see cref="AllowAnyStatusCodeAttribute"/> class, which does allow any status code
			/// </summary>
			public AllowAnyStatusCodeAttribute()
				: this(true)
			{
			}
	
			/// <summary>
			/// Initialises a new instance of the <see cref="AllowAnyStatusCodeAttribute"/> class
			/// </summary>
			/// <param name="allowAnyStatusCode">True to allow any response status code; False to throw an exception on response status codes that do not indicate success</param>
			public AllowAnyStatusCodeAttribute(bool allowAnyStatusCode)
			{
				this.AllowAnyStatusCode = allowAnyStatusCode;
			}
		}
		""";
	
	public const string HeaderAttribute = $$"""
		using System;
		using System.Diagnostics.CodeAnalysis;
		
		namespace {{BaseNamespace}};

		#nullable enable

		/// <summary>
		/// Attribute allowing interface-level, method-level, or parameter-level headers to be defined. See the docs for details
		/// </summary>
		[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Parameter | AttributeTargets.Property, Inherited = true, AllowMultiple = true)]
		public sealed class HeaderAttribute : Attribute
		{
			/// <summary>
			/// Gets the Name of the header
			/// </summary>
			public string Name { get; }
	
			/// <summary>
			/// Gets the value of the header, if present
			/// </summary>
			public string? Value { get; }
	
			/// <summary>
			/// Gets or sets the format string used to format the value, if this is used as a variable header
			/// (i.e. <see cref="Value"/> is null).
			/// </summary>
			/// <remarks>
			/// If this looks like a format string which can be passed to <see cref="string.Format(IFormatProvider, string, object[])"/>,
			/// (i.e. it contains at least one format placeholder), then this happens with the value passed as the first arg.
			/// Otherwise, if the value implements <see cref="IFormattable"/>, this is passed to the value's
			/// <see cref="IFormattable.ToString(string, IFormatProvider)"/> method. Otherwise this is ignored.
			/// Example values: "X2", "{0:X2}", "test{0}".
			/// </remarks>
			[StringSyntax(StringSyntaxAttribute.CompositeFormat)]
			public string? Format { get; set; }
	
			/// <summary>
			/// Initialises a new instance of the <see cref="HeaderAttribute"/> class
			/// </summary>
			/// <param name="name">Name of the header</param>
			public HeaderAttribute(string name)
			{
				this.Name = name;
				this.Value = null;
			}
	
			/// <summary>
			/// Initialises a new instance of the <see cref="HeaderAttribute"/> class
			/// </summary>
			/// <param name="name">Name of the header</param>
			/// <param name="value">Value of the header</param>
			public HeaderAttribute(string name, string? value)
			{
				this.Name = name;
				this.Value = value;
			}
		}
		""";
}