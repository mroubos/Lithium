using System;
using System.Data;

namespace Lithium
{
	internal class QueryInfo
	{
		public string Query { get; set; }
		public object Deserializer { get; set; }
		public Func<object, object, object> GetParameterCombiner { get; set; }
		public Action<IDbCommand, object> ParameterGenerator { get; set; }
	}
}