using System;
using System.Data;

namespace Lithium
{
	public class Parameter
	{
		public string Name { get; set; }
		public object Value { get; set; }
		public Type Type { get; set; }
		public ParameterDirection Direction { get; set; }
	}
}
