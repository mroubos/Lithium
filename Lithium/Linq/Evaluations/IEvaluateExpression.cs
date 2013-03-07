using System.Linq.Expressions;

namespace Lithium.Linq.Evaluations
{
	public interface IEvaluateExpression
	{
		void Evaluate<T>(MethodCallExpression expression, QueryState<T> state);
	}
}