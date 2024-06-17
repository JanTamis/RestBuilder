using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RestBuilder.Helpers;

public static class CompilerHelpers
{
	public static IEnumerable<AttributeData> GetAttributes(this ISymbol symbol, string attributeName)
	{
		return symbol.GetAttributes().Where(a => a.AttributeClass?.Name == attributeName);
	}

	public static bool HasParameters<T>(this IMethodSymbol method)
	{
		return method.Parameters.Length == 1 && method.Parameters[0].Type.IsType<T>();
	}
	
	public static bool HasParameters<T1, T2>(this IMethodSymbol method)
	{
		return method.Parameters.Length == 2 && method.Parameters[0].Type.IsType<T1>() && method.Parameters[1].Type.IsType<T2>();
	}

	public static bool HasParameters<T1, T2, T3>(this IMethodSymbol method)
	{
		return method.Parameters.Length == 3 && method.Parameters[0].Type.IsType<T1>() && method.Parameters[1].Type.IsType<T2>() && method.Parameters[2].Type.IsType<T3>();
	}

	public static bool HasParameters<T1, T2, T3, T4>(this IMethodSymbol method)
	{
		return method.Parameters.Length == 4 && method.Parameters[0].Type.IsType<T1>() && method.Parameters[1].Type.IsType<T2>() && method.Parameters[2].Type.IsType<T3>() && method.Parameters[3].Type.IsType<T4>();
	}
	
	public static bool IsType<T>(this ISymbol symbol)
	{
		var type = typeof(T);
		return symbol.ContainingNamespace.ToString() == type.Namespace && symbol.Name == type.Name;
	}

	public static bool IsAwaitableType(this ITypeSymbol typeSymbol)
	{
		// Zoek de GetAwaiter methode zonder parameters
		var getAwaiterMethod = typeSymbol.GetMembers("GetAwaiter")
				.OfType<IMethodSymbol>()
				.FirstOrDefault(m => m.Parameters.IsEmpty && !m.ReturnsVoid);

		if (getAwaiterMethod == null)
		{
			return false;
		}

		var awaiterType = getAwaiterMethod.ReturnType;

		// Controleer de IsCompleted property
		var isCompletedProperty = awaiterType
			.GetMembers("IsCompleted")
			.OfType<IPropertySymbol>()
			.FirstOrDefault(p => p.Type.SpecialType == SpecialType.System_Boolean);

		if (isCompletedProperty == null)
		{
			return false;
		}

		// Controleer de GetResult methode
		var getResultMethod = awaiterType.GetMembers("GetResult")
			.OfType<IMethodSymbol>()
			.FirstOrDefault(m => m.Parameters.IsEmpty);

		if (getResultMethod == null)
		{
			return false;
		}

		// Controleer de OnCompleted methode
		var onCompletedMethod = awaiterType
			.GetMembers("OnCompleted")
			.OfType<IMethodSymbol>()
			.FirstOrDefault(m => m.Parameters.Length == 1 && m.Parameters[0].Type.ToDisplayString() == "System.Action");

		if (onCompletedMethod == null)
		{
			return false;
		}

		return true;
	}

	public static ITypeSymbol? GetAwaitableReturnType(this ITypeSymbol awaitableType)
	{
		// Verkrijg de GetAwaiter methode
		var getAwaiterMethod = awaitableType.GetMembers("GetAwaiter")
				.OfType<IMethodSymbol>()
				.FirstOrDefault(m => m.Parameters.IsEmpty && !m.ReturnsVoid);

		if (getAwaiterMethod == null)
		{
			return null; // Niet een geldig awaitable type
		}

		var awaiterType = getAwaiterMethod.ReturnType;

		// Zoek de GetResult methode binnen de awaiter
		var getResultMethod = awaiterType.GetMembers("GetResult")
				.OfType<IMethodSymbol>()
				.FirstOrDefault(m => m.Parameters.IsEmpty);

		// Het return type van GetResult is het type dat je zoekt
		return getResultMethod?.ReturnType;
	}

}
