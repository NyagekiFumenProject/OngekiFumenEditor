using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Utils
{
	public static class ReflectionHelp
	{
		private static Dictionary<Type, string> cacheNames = new Dictionary<Type, string>();
		public static string GetTypeName(this Type type)
		{
			if (!cacheNames.TryGetValue(type, out var name))
			{
				name = type.Name;
				var genericTypes = type.GenericTypeArguments;
				if (genericTypes.Length > 0)
				{
					name = name.Replace("`1", "<`1");
					name = name + ">";
				}
				for (int i = 1; i <= genericTypes.Length; i++)
					name = name.Replace($"`{i}", genericTypes[i - 1].GetTypeName() + (i == genericTypes.Length || genericTypes.Length == 1 ? string.Empty : ","));
				cacheNames[type] = name;
			}

			return name;
		}
	}
}
