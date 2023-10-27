using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using static OngekiFumenEditor.Utils.LambdaActivator;

namespace OngekiFumenEditor.Utils
{
	/*
        Source from : https://github.com/eallegretta/lambda-activator/blob/master/src/LambdaActivator/LambdaActivator.cs 

        The MIT License (MIT)

        Copyright (c) 2014 eallegretta

        Permission is hereby granted, free of charge, to any person obtaining a copy
        of this software and associated documentation files (the "Software"), to deal
        in the Software without restriction, including without limitation the rights
        to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        copies of the Software, and to permit persons to whom the Software is
        furnished to do so, subject to the following conditions:

        The above copyright notice and this permission notice shall be included in all
        copies or substantial portions of the Software.

        THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
        SOFTWARE.
     */

	/// <summary>
	/// Allows to create new instances of a specified type using a compiled Lambda expression
	/// http://bloggingabout.net/blogs/vagif/archive/2010/04/02/don-t-use-activator-createinstance-or-constructorinfo-invoke-use-compiled-lambda-expressions.aspx
	/// http://rogeralsing.com/2008/02/28/linq-expressions-creating-objects/
	/// </summary>
	public static class LambdaActivator
	{
		/// <summary>
		/// A delegate to create an instance of a type
		/// </summary>
		/// <param name="args">The constructor args.</param>
		/// <returns>A new instance.</returns>
		public delegate object ObjectActivator(params object[] args);

		/// <summary>
		/// A delegate to create an instance of a type
		/// </summary>
		/// <typeparam name="T">The type to create.</typeparam>
		/// <param name="args">The constructor args.</param>
		/// <returns>
		/// A new instance.
		/// </returns>
		public delegate T ObjectActivator<T>(params object[] args);

		/// <summary>
		/// Creates a new instance of the specified type.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <param name="args">The constructor arguments.</param>
		/// <returns>A new instance of the specified type</returns>
		public static object CreateInstance(Type type, params object[] args)
		{
			var constructor = GetMatchingConstructor(type, args);

			if (type.IsValueType)
			{
				return Activator.CreateInstance(type, args);
			}

			return GetActivator(constructor)(args);
		}

		/// <summary>
		/// Creates a new instance of the specified type.
		/// </summary>
		/// <typeparam name="T">The type.</typeparam>
		/// <param name="args">The constructor arguments.</param>
		/// <returns>
		/// A new instance of the specified type
		/// </returns>
		public static T CreateInstance<T>(params object[] args)
		{
			var type = typeof(T);
			var constructor = GetMatchingConstructor(type, args);

			if (type.IsValueType)
			{
				return (T)Activator.CreateInstance(typeof(T), args);
			}

			return GetActivator<T>(constructor)(args);
		}

		/// <summary>
		/// Gets the activator for a specified type.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <returns>An object activator for the specified type.</returns>
		public static ObjectActivator GetActivator(Type type)
		{
			if (type.IsValueType)
			{
				return args => Activator.CreateInstance(type);
			}

			var ctor = type.GetConstructors().First();
			return GetActivator(ctor);
		}

		/// <summary>
		/// Gets the activator for a specified constructor info.
		/// </summary>
		/// <param name="ctor">The constructor.</param>
		/// <returns>
		/// An object activator for the specified type.
		/// </returns>
		public static ObjectActivator GetActivator(ConstructorInfo ctor)
		{
			return (ObjectActivator)GetActivatorDelegate(typeof(ObjectActivator), ctor);
		}

		/// <summary>
		/// Gets the activator for a specified type.
		/// </summary>
		/// <typeparam name="T">The type.</typeparam>
		/// <returns>
		/// An object activator for the specified type.
		/// </returns>
		public static ObjectActivator<T> GetActivator<T>()
		{
			var ctor = typeof(T).GetConstructors().First();
			return GetActivator<T>(ctor);
		}

		/// <summary>
		/// Gets the activator for a specified constructor info.
		/// </summary>
		/// <typeparam name="T">The type.</typeparam>
		/// <param name="ctor">The constructor.</param>
		/// <returns>
		/// An object activator for the specified type.
		/// </returns>
		public static ObjectActivator<T> GetActivator<T>(ConstructorInfo ctor)
		{
			return (ObjectActivator<T>)GetActivatorDelegate(typeof(ObjectActivator<T>), ctor);
		}

		private static Delegate GetActivatorDelegate(Type activatorDelegateType, ConstructorInfo ctor)
		{
			var paramsInfo = ctor.GetParameters();

			var param = Expression.Parameter(typeof(object[]), "args");

			var argsExp = new Expression[paramsInfo.Length];

			for (int i = 0; i < paramsInfo.Length; i++)
			{
				Expression index = Expression.Constant(i);
				var paramType = paramsInfo[i].ParameterType;

				Expression paramAccessorExp = Expression.ArrayIndex(param, index);

				Expression paramCastExp = Expression.Convert(paramAccessorExp, paramType);

				argsExp[i] = paramCastExp;
			}

			var newExp = Expression.New(ctor, argsExp);

			var lambda = Expression.Lambda(activatorDelegateType, newExp, param);

			return lambda.Compile();
		}

		public static ConstructorInfo GetMatchingConstructor(Type type, params object[] args)
		{
			var types = from arg in args
						where arg != null
						select arg.GetType();

			return type.GetConstructor(types.ToArray());
		}
	}

	public static class CacheLambdaActivator
	{
		private readonly static Dictionary<Type, ObjectActivator> cachedActivatorGenerator = new();

		public static object CreateInstance(Type type)
		{
			if (!cachedActivatorGenerator.TryGetValue(type, out var activator))
			{
				activator = GetActivator(type);
				cachedActivatorGenerator[type] = activator;
			}
			return activator();
		}

		public static T CreateInstance<T>() where T : class, new() => CreateInstance(typeof(T)) as T;
	}
}