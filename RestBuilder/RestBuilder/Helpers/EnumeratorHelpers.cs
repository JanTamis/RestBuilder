using System;
using System.Collections.Generic;
using System.Linq;

namespace RestBuilder.Helpers;

public static class EnumeratorHelpers
{
	public static IEnumerable<T> DistinctBy<T, TKey>(this IEnumerable<T> items, Func<T, TKey> property)
	{
		var hashSet = new HashSet<TKey>();

		foreach (var item in items)
		{
			if (hashSet.Add(property(item)))
			{
				yield return item;
			}
		}
	}
}