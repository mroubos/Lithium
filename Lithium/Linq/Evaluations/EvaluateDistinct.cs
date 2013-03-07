using Lithium.Linq.Evaluations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Lithium.Linq.Evaluations
{
	public class ProcessDistinct : IEvaluateExpression
	{
		public void Evaluate<T>(MethodCallExpression expression, QueryState<T> state)
		{
			state.Distinct = true;
		}
	}
}