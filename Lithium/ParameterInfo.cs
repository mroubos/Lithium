using System;
using System.Reflection;

namespace Lithium
{
	internal class ParameterInfo
	{
		public string Name { get; set; }
		public Type Type { get; set; }
		public MethodInfo Getter { get; set; }
	}
}