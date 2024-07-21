#if NET6_0_OR_GREATER

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace RestBuilder.Core.Builders;

/// <summary>
/// Represents a builder for constructing URL strings with query parameters.
/// </summary>
public struct UrlBuilder
{
	private char[] _buffer;
	private int _position;
	private bool _hasQueries;

	/// <summary>
	/// Initializes a new instance of the <see cref="UrlBuilder"/> struct with the specified interpolated string handler.
	/// </summary>
	/// <param name="handler">The interpolated string handler that provides the buffer for the builder.</param>
	public UrlBuilder(UrlBuilderHandler handler)
	{
		_buffer = handler.Buffer;
		_position = handler.Position;
		_hasQueries = handler.HasQueries;
	}

	/// <summary>
	/// Appends a query parameter to the URL.
	/// </summary>
	/// <param name="key">The key of the query parameter.</param>
	/// <param name="value">The value of the query parameter.</param>
	/// <param name="urlEncode">Indicates whether the value should be URL-encoded.</param>
	/// <typeparam name="T">The type of the value.</typeparam>
	public void AppendQuery<T>(string key, T value, bool urlEncode = true)
	{
		if (_hasQueries)
		{
			AppendChar(_hasQueries ? '&' : '?');
		}

		AppendLiteral(key);
		AppendChar('=');

		if (urlEncode && NeedsUrlEncode<T>())
		{
			var valueString = Uri.EscapeDataString(value?.ToString() ?? String.Empty);

			AppendLiteral(valueString);
		}
		else
		{
			if (value is ISpanFormattable spanFormattable)
			{
				var written = 0;

				while (!spanFormattable.TryFormat(_buffer.AsSpan(_position), out written, null, null))
				{
					Grow();
				}

				_position += written;

				return;
			}

			AppendLiteral(value?.ToString() ?? String.Empty);
		}

		_hasQueries = true;
	}

	/// <summary>
	/// Converts the accumulated URL and query parameters into a <see cref="Uri"/> object and resets the builder state.
	/// </summary>
	/// <remarks>
	/// This method constructs a URI from the internal character buffer that has been populated by appending literals and query parameters.
	/// After constructing the URI, it returns the buffer to the array pool to prevent memory leaks and resets the internal state of the builder,
	/// making it ready for building a new URI. The `dontEscape` parameter is set to true to prevent double escaping of the URI components.
	/// </remarks>
	/// <returns>A <see cref="Uri"/> object representing the constructed URL.</returns>
	public Uri ToUriAndClear()
	{
		var result = new Uri(new string(_buffer, 0, _position), dontEscape: true);

		ArrayPool<char>.Shared.Return(_buffer);

		_position = 0;
		_hasQueries = false;

		return result;
	}

	private void AppendLiteral(string value)
	{
		value.CopyTo(0, _buffer, _position, value.Length);

		_position += value.Length;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void AppendChar(char value)
	{
		_buffer[_position++] = value;
	}

	/// <summary>
	/// Doubles the size of the buffer to accommodate more characters.
	/// </summary>
	private void Grow()
	{
		var newBuffer = ArrayPool<char>.Shared.Rent(_buffer.Length * 2);

		_buffer.CopyTo(newBuffer, 0);

		ArrayPool<char>.Shared.Return(_buffer);

		_buffer = newBuffer;
	}

	private static bool NeedsUrlEncode<T>()
	{
		return typeof(T) != typeof(short)
		       && typeof(T) != typeof(int)
		       && typeof(T) != typeof(long)
		       && typeof(T) != typeof(ushort)
		       && typeof(T) != typeof(uint)
		       && typeof(T) != typeof(ulong)
		       && typeof(T) != typeof(float)
		       && typeof(T) != typeof(double)
		       && typeof(T) != typeof(decimal)
		       && typeof(T) != typeof(bool)
		       && typeof(T) != typeof(char);
	}

	/// <summary>
	/// An interpolated string handler for efficiently constructing URL strings.
	/// </summary>
	[InterpolatedStringHandler]
	public ref struct UrlBuilderHandler(int literalLength, int formattedCount)
	{
		internal char[] Buffer = ArrayPool<char>.Shared.Rent(literalLength + formattedCount * 11);

		internal int Position;
		internal bool HasQueries;

		public void AppendLiteral(string value)
		{
			if (Position + value.Length > Buffer.Length)
			{
				Grow();
			}

			if (!HasQueries)
			{
				HasQueries = value.Contains('?') || value.Contains('&');
			}

			value.CopyTo(0, Buffer, Position, value.Length);

			Position += value.Length;
		}

		public void AppendFormatted<T>(T value)
		{
			var formatted = value?.ToString();

			if (formatted is null)
			{
				return;
			}

			AppendLiteral(formatted);
		}

		public void AppendFormatted<T>(T value, string? format)
		{
			switch (value)
			{
				case ISpanFormattable spanFormattable:
				{
					var written = 0;

					while (!spanFormattable.TryFormat(Buffer.AsSpan(Position), out written, format, null))
					{
						Grow();
					}

					Position += written;
					break;
				}

				case IFormattable formattable:
					AppendLiteral(formattable.ToString(format, null));
					break;
				default:
					AppendFormatted(value);
					break;
			}
		}

		private void Grow()
		{
			var newBuffer = ArrayPool<char>.Shared.Rent(Buffer.Length * 2);

			Buffer.CopyTo(newBuffer, 0);

			ArrayPool<char>.Shared.Return(Buffer);

			Buffer = newBuffer;
		}
	}
}
#endif