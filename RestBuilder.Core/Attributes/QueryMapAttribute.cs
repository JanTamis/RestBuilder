using RestBuilder.Core.Enumerators;
using System;

namespace RestBuilder.Core.Attributes;

/// <summary>
/// Marks a parameter as being the method's Query Map
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false, AllowMultiple = true)]
public sealed class QueryMapAttribute : Attribute
{
	/// <summary>
	/// Gets and sets the serialization method to use to serialize the value. Defaults to QuerySerializationMethod.ToString
	/// </summary>
	public QuerySerializationMethod SerializationMethod { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this path parameter should be URL-encoded. Defaults to true.
	/// </summary>
	public bool UrlEncode { get; set; } = true;

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
	/// Initialises a new instance of the <see cref="QueryMapAttribute"/> class
	/// </summary>
	public QueryMapAttribute() : this(QuerySerializationMethod.Default)
	{
	}

	/// <summary>
	/// Initialises a new instance of the <see cref="QueryMapAttribute"/> with the given serialization method
	/// </summary>
	/// <param name="serializationMethod">Serialization method to use to serialize the value</param>
	public QueryMapAttribute(QuerySerializationMethod serializationMethod)
	{
		SerializationMethod = serializationMethod;
	}
}
