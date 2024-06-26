using Microsoft.CodeAnalysis;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RestBuilder.Interfaces;
using RestBuilder.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RestBuilder.Helpers;

public static class CompilerHelpers
{
	public static IEnumerable<AttributeData> GetAttributes(this ISymbol symbol, string attributeName)
	{
		return symbol.GetAttributes().Where(a => a.AttributeClass?.Name == attributeName);
	}

	public static bool HasAttribute(this ISymbol symbol, string attributeName)
	{
		return symbol.GetAttributes().Any(a => a.AttributeClass?.Name == attributeName);
	}

	public static bool HasParameters<T>(this IMethodSymbol method)
	{
		return method.Parameters.Length == 1 && 
		       method.Parameters[0].Type.IsType<T>();
	}
	
	public static bool HasParameters<T1, T2>(this IMethodSymbol method)
	{
		return method.Parameters.Length == 2 && 
		       method.Parameters[0].Type.IsType<T1>() && 
		       method.Parameters[1].Type.IsType<T2>();
	}

	public static bool HasParameters<T1, T2, T3>(this IMethodSymbol method)
	{
		return method.Parameters.Length == 3 && 
		       method.Parameters[0].Type.IsType<T1>() && 
		       method.Parameters[1].Type.IsType<T2>() && 
		       method.Parameters[2].Type.IsType<T3>();
	}

	public static bool HasParameters<T1, T2, T3, T4>(this IMethodSymbol method)
	{
		return method.Parameters.Length == 4 && 
		       method.Parameters[0].Type.IsType<T1>() && 
		       method.Parameters[1].Type.IsType<T2>() && 
		       method.Parameters[2].Type.IsType<T3>() && 
		       method.Parameters[3].Type.IsType<T4>();
	}

	public static bool HasReturnType<T>(this IMethodSymbol method)
	{
		return IsType<T>(method.ReturnType);
	}
	
	public static bool IsType<T>(this ISymbol symbol)
	{
		var type = typeof(T);
		return symbol.ContainingNamespace?.ToString() == type.Namespace && symbol.Name == type.Name;
	}

	public static bool IsType<T>(this IType symbol)
	{
		var type = typeof(T);

		if (type.Namespace == "System" && symbol.Namespace == "System")
		{
			switch (type.Name)
			{
				case "String":
					return symbol.Type is "String" or "string";
				case "Int32":
					return symbol.Type is "Int32" or "int";
				case "Int64":
					return symbol.Type is "Int64" or "long";
				case "Boolean":
					return symbol.Type is "Boolean" or "bool";
				case "Decimal":
					return symbol.Type is "Decimal" or "decimal";
				case "Double":
					return symbol.Type is "Double" or "double";
				case "Single":
					return symbol.Type is "Single" or "float";
				case "Byte":
					return symbol.Type is "Byte" or "byte";
				case "SByte":
					return symbol.Type is "SByte" or "sbyte";
				case "Char":
					return symbol.Type is "Char" or "char";
				case "UInt16":
					return symbol.Type is "UInt16" or "ushort";
				case "UInt32":
					return symbol.Type is "UInt32" or "uint";
				case "UInt64":
					return symbol.Type is "UInt64" or "ulong";
				case "Object":
					return symbol.Type is "Object" or "object";
			}
		}
		
		return symbol.Namespace == type.Namespace && symbol.Type == type.Name;
	}

	public static bool IsType<T>(this TypeModel symbol)
	{
		var type = typeof(T);

		if (type.Namespace == "System" && symbol.Namespace == "System")
		{
			switch (type.Name)
			{
				case "String":
					return symbol.Type is "String" or "string";
				case "Int32":
					return symbol.Type is "Int32" or "int";
				case "Int64":
					return symbol.Type is "Int64" or "long";
				case "Boolean":
					return symbol.Type is "Boolean" or "bool";
				case "Decimal":
					return symbol.Type is "Decimal" or "decimal";
				case "Double":
					return symbol.Type is "Double" or "double";
				case "Single":
					return symbol.Type is "Single" or "float";
				case "Byte":
					return symbol.Type is "Byte" or "byte";
				case "SByte":
					return symbol.Type is "SByte" or "sbyte";
				case "Char":
					return symbol.Type is "Char" or "char";
				case "UInt16":
					return symbol.Type is "UInt16" or "ushort";
				case "UInt32":
					return symbol.Type is "UInt32" or "uint";
				case "UInt64":
					return symbol.Type is "UInt64" or "ulong";
				case "Object":
					return symbol.Type is "Object" or "object";
			}
		}

		return symbol.Namespace == type.Namespace && symbol.Type == type.Name;
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

	public static bool IsCollection(this ITypeSymbol type)
	{
		if (type.IsType<String>())
		{
			return false;
		}

		if (type is IArrayTypeSymbol)
		{
			return true;
		}

		foreach (var @interface in type.AllInterfaces)
		{
			if (@interface.ContainingNamespace?.ToString() == "System.Collections.Generic" && @interface.Name == nameof(IEnumerable))
			{
				return true;
			}
		}

		return type.ContainingNamespace?.ToString() == "System.Collections.Generic" && type.Name == nameof(IEnumerable);
	}

	public static bool IsTaskType(this ITypeSymbol? typeSymbol, Compilation compilation)
	{
		if (typeSymbol == null)
		{
			return false;
		}

		var taskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
		var taskOfTType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");

		if (typeSymbol.OriginalDefinition.Equals(taskType) || typeSymbol.OriginalDefinition.Equals(taskOfTType))
		{
			return true;
		}

		return false;
	}
	
	public static IEnumerable<IMethodSymbol> GetMethods(this ITypeSymbol type)
	{
		return type.GetMembers().OfType<IMethodSymbol>();
	}

	public static IEnumerable<IPropertySymbol> GetProperties(this ITypeSymbol type)
	{
		return type.GetMembers().OfType<IPropertySymbol>();
	}

	public static bool IsPartial(this IMethodSymbol method)
	{
		return method.DeclaringSyntaxReferences
			.Any(syntax => syntax.GetSyntax() is MethodDeclarationSyntax declaration && declaration.Modifiers
				.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword)));
	}

	public static Location? GetLocation(this ISymbol symbol)
	{
		return symbol.Locations.FirstOrDefault();
	}

	public static void ReportDiagnostic<T>(this SymbolAnalysisContext context, ISymbol symbol, Func<T, CSharpSyntaxNode> selector, DiagnosticDescriptor descriptor) where T : CSharpSyntaxNode
	{
		foreach (var syntaxReference in symbol.DeclaringSyntaxReferences)
		{
			var syntax = syntaxReference.GetSyntax(context.CancellationToken);

			if (syntax is not T declaration)
			{
				continue;
			}

			context.ReportDiagnostic(Diagnostic.Create(descriptor, selector(declaration).GetLocation(), symbol.Name));
		}
	}

	public static void ReportDiagnostic<T>(this SymbolAnalysisContext context, ISymbol symbol, Func<T, SyntaxToken> selector, DiagnosticDescriptor descriptor) where T : CSharpSyntaxNode
	{
		foreach (var syntaxReference in symbol.DeclaringSyntaxReferences)
		{
			var syntax = syntaxReference.GetSyntax(context.CancellationToken);

			if (syntax is not T declaration)
			{
				continue;
			}

			context.ReportDiagnostic(Diagnostic.Create(descriptor, selector(declaration).GetLocation(), symbol.Name));
		}
	}
}
