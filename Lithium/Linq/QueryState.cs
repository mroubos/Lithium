using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lithium.Linq
{
	public class QueryState<T>
	{
		public bool Distinct { get; set; }
		public List<Parameter> Parameters { get; set; }

		public List<string> FieldNames { get; set; }

		public bool HasParameters { get { return Parameters.Count > 0; } }

		public QueryState()
		{
			Parameters = new List<Parameter>();
			FieldNames = new List<string>();
		}
	}
}