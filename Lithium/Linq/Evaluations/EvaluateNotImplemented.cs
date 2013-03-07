using Lithium.Linq.Evaluations;
using System;
using System.Linq.Expressions;

namespace Lithium.Linq.Evaluations
{
	public class ProcessNotImplemented : IEvaluateExpression
	{
		public void Evaluate<T>(MethodCallExpression expression, QueryState<T> state)
		{
			throw new NotImplementedException(string.Format("Lithium.Linq does not support the {0}() method", (expression as MethodCallExpression).Method.Name));
		}
	}
}