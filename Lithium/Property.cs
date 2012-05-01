using System;
using System.Collections.Generic;
using System.Reflection;

namespace Lithium
{
	internal class Property
	{
		public string Name { get; set; }
		public Type Type { get; set; }
		public MethodInfo Getter { get; set; }
		public MethodInfo Setter { get; set; }
		public List<MethodInfo> ParentGetters { get; set; }		
	}
}