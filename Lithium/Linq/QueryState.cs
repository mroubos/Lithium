using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lithium.Linq
{
	public class QueryState<T>
	{
		public StringBuilder Where { get; set; }
		public bool Distinct { get; set; }
		public Parameters Parameters { get; set; }

		public List<string> FieldNames { get; set; }

		public bool HasParameters { get { return Parameters.Count() > 0; } }

		private int nextParameter = 0;

		public QueryState()
		{
			Parameters = new Parameters();
			FieldNames = new List<string>();
			Where = new StringBuilder();
		}

		public string GetNextParameter()
		{
			var s = "p" + nextParameter;
			nextParameter++;
			return s;
		}
	}
}