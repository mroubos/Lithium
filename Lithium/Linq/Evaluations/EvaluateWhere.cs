using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Lithium.Linq.Evaluations
{
	public class EvaluateWhere : IEvaluateExpression
	{
		private static readonly MethodInfo String_IsNullOrEmpty;
		private static readonly MethodInfo String_StartsWith;
		private static readonly MethodInfo String_StartsWithAndComparison;
		private static readonly MethodInfo String_EndsWith;
		private static readonly MethodInfo String_EndsWithAndComparison;
		private static readonly MethodInfo String_Contains;
		private static readonly MethodInfo String_Equals;
		private static readonly MemberInfo Nullable_HasValue;

		private class Operator
		{
			private readonly string normal;
			private readonly string reversed;

			public string Value(bool reversed)
			{
				return reversed ? this.reversed : this.normal;
			}

			public Operator(string normal, string reversed)
			{
				this.normal = normal;
				this.reversed = reversed;
			}
		}

		private readonly static Dictionary<ExpressionType, Operator> comparisonOperators = new Dictionary<ExpressionType, Operator>()
		{
			{ ExpressionType.Equal, new Operator("=", "!=") },
			{ ExpressionType.NotEqual, new Operator("!=", "=") },
			{ ExpressionType.GreaterThan, new Operator(">", "<=") },
			{ ExpressionType.LessThan, new Operator("<", ">=") },
			{ ExpressionType.GreaterThanOrEqual, new Operator(">=", "<") },
			{ ExpressionType.LessThanOrEqual,  new Operator("<=", ">") }
		};

		static EvaluateWhere()
		{
			String_IsNullOrEmpty = typeof(string).GetMethod("IsNullOrEmpty", BindingFlags.Static | BindingFlags.Public);
			String_StartsWith = typeof(string).GetMethod("StartsWith", new Type[] { typeof(string) });
			String_StartsWithAndComparison = typeof(string).GetMethod("StartsWith", new Type[] { typeof(string), typeof(StringComparison) });
			String_EndsWith = typeof(string).GetMethod("EndsWith", new Type[] { typeof(string) });
			String_EndsWithAndComparison = typeof(string).GetMethod("EndsWith", new Type[] { typeof(string), typeof(StringComparison) });
			String_Contains = typeof(string).GetMethod("Contains", new Type[] { typeof(string) });
			String_Equals = typeof(string).GetMethod("Equals", new Type[] { typeof(string) });
			Nullable_HasValue = typeof(Nullable<>).GetProperty("HasValue", BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty);
		}

		public void Evaluate<T>(MethodCallExpression expression, QueryState<T> state)
		{
			var whereExpression = expression.Arguments[1];
			if (whereExpression is UnaryExpression) {
				var unaryWhereExpression = whereExpression as UnaryExpression;
				var operandExpression = unaryWhereExpression.Operand;
				EvaluateExpression(operandExpression, state);
			}
			else
				throw new NotSupportedException("Unable to resolve value for Where()");
		}

		private void EvaluateExpression<T>(Expression expression, QueryState<T> state)
		{
			if (expression is LambdaExpression) {
				var lambdaOperandExpression = expression as LambdaExpression;
				EvaluateExpression(lambdaOperandExpression.Body, state);
			}
			else if (expression is UnaryExpression)
				UnaryExpression(expression as UnaryExpression, state);
			else if (expression is BinaryExpression)
				BinaryExpression(expression as BinaryExpression, state);
			else if (expression is MemberExpression)
				MemberExpression(expression as MemberExpression, state);
			else if (expression is MethodCallExpression)
				MethodCallExpression(expression as MethodCallExpression, state);
			else
				throw new NotSupportedException();
		}

		private void CheckArgumentIsValue<T>(Expression argument, T expectedValue, string exceptionText) where T : struct
		{
			// ensure that an argument is of the correct type and contains a specific value
			if (argument is ConstantExpression && argument.Type == typeof(T) && EqualityComparer<T>.Default.Equals((T)(argument as ConstantExpression).Value, expectedValue))
				return;

			throw new NotSupportedException(exceptionText);
		}

		private void UnaryExpression<T>(UnaryExpression body, QueryState<T> state)
		{
			// simple case of a ! prior to a function call
			if (body.NodeType == ExpressionType.Not && body.Operand is MethodCallExpression) {
				MethodCallExpression(body.Operand as MethodCallExpression, state, true);
				return;
			}

			// simple case of a ! prior to a property call
			if (body.NodeType == ExpressionType.Not && body.Operand is MemberExpression) {
				MemberExpression(body.Operand as MemberExpression, state, true);
				return;
			}

			// simple case of a ! prior to a binary expression
			if (body.NodeType == ExpressionType.Not && body.Operand is BinaryExpression) {
				BinaryExpression(body.Operand as BinaryExpression, state, true);
				return;
			}

			throw new NotSupportedException();
		}

		private void BinaryExpression<T>(BinaryExpression body, QueryState<T> state, bool inverse = false)
		{
			var left = body.Left;
			var right = body.Right;

			if (body.NodeType == ExpressionType.AndAlso || body.NodeType == ExpressionType.OrElse) {
				if (inverse) state.Where.Append("( NOT ");
				state.Where.Append("( ");
				EvaluateExpression(left, state);
				state.Where.Append(body.NodeType == ExpressionType.AndAlso ? " AND " : " OR ");
				EvaluateExpression(right, state);
				state.Where.Append(" )");
				if (inverse) state.Where.Append(" )");
			}
			else {
				// reverse where applicable
				bool reversed = false;
				if (left is ConstantExpression && right is MemberExpression) {
					reversed = true;
					var swap = right;
					right = left;
					left = swap;
				}

				if (right is ConstantExpression && left is MemberExpression) {
					string fieldName = (left as MemberExpression).Member.Name;
					object value = (right as ConstantExpression).Value;
					// caching this would lead to caching one or the other depending on whether the original value was NULL or not!
					// could optimise out non-nullable values
					if (value == null) {
						if (body.NodeType == ExpressionType.Equal || body.NodeType == ExpressionType.NotEqual) {
							bool equal = body.NodeType == ExpressionType.Equal;
							if (inverse) equal = !equal;
							state.Where.AppendFormat(" ( t.[{0}] {1} NULL ) ", fieldName, equal ? "IS" : "IS NOT");
							return;
						}
						else
							throw new NotSupportedException();
					}
					else {
						Operator op;
						if (!comparisonOperators.TryGetValue(body.NodeType, out op))
							throw new NotSupportedException();

						string parameterName = state.GetNextParameter();
						if (!reversed)
							state.Where.AppendFormat(" ( t.[{0}] {1} @{2} ) ", fieldName, op.Value(inverse), parameterName);
						else
							state.Where.AppendFormat(" ( @{2} {1} t.[{0}] ) ", fieldName, op.Value(inverse), parameterName);

						state.Parameters.Add(parameterName, value);
					}
				}
				else if (left is MemberExpression && right is MemberExpression) {
					Operator op;
					if (!comparisonOperators.TryGetValue(body.NodeType, out op))
						throw new NotSupportedException();

					if (!reversed)
						state.Where.AppendFormat(" ( t.[{0}] {1} t.[{2}] ) ", (left as MemberExpression).Member.Name, op.Value(inverse), (right as MemberExpression).Member.Name);
					else
						state.Where.AppendFormat(" ( t.[{2}] {1} t.[{0}] ) ", (left as MemberExpression).Member.Name, op.Value(inverse), (right as MemberExpression).Member.Name);
				}
				else
					throw new NotSupportedException();
			}
		}

		private void MethodCallExpression<T>(MethodCallExpression body, QueryState<T> state, bool inverse = false)
		{
			string negative = inverse ? "NOT " : string.Empty;
			
			if (body.Method == String_IsNullOrEmpty) {
				string fieldName = (body.Arguments[0] as MemberExpression).Member.Name;
				state.Where.AppendFormat(" ( t.[{0}] IS {1}NULL ) ", fieldName, negative);
			}
			else if (body.Method == String_Equals) {
				string fieldName = (body.Object as MemberExpression).Member.Name;
				Expression valueExpression = body.Arguments[0];

				if (valueExpression is ConstantExpression) {
					var constantValueExpression = valueExpression as ConstantExpression;
					var parameter = state.GetNextParameter();
					if (constantValueExpression.Type == typeof(string) && constantValueExpression.Value != null) {
						string value = (string)constantValueExpression.Value;
						state.Parameters.Add(parameter, value);
						state.Where.AppendFormat(" ( t.[{0}] = @{1} )", fieldName, parameter);
					}
					else
						throw new NotSupportedException();
				}
				else if (valueExpression is MemberExpression) {
					MemberExpression memberValueExpression = valueExpression as MemberExpression;
					string value = Expression.Lambda(memberValueExpression).Compile().DynamicInvoke() as string;
					string parameter = state.GetNextParameter();

					state.Parameters.Add(parameter, value);
					state.Where.AppendFormat(" ( t.[{0}] = @{1} )", fieldName, parameter);					
				}
				else
					throw new NotSupportedException();
			}
			else if (body.Method == String_EndsWith || body.Method == String_EndsWithAndComparison) {
				string fieldName = (body.Object as MemberExpression).Member.Name;
				Expression valueExpression = body.Arguments[0];

				// if comparison is provide ensure it's one that SQL server will respect
				if (body.Arguments.Count == 2) CheckArgumentIsValue<StringComparison>(body.Arguments[1], StringComparison.OrdinalIgnoreCase, "EndsWith can only be used with StringComparison.OrdinalIgnoreCase");

				if (valueExpression is ConstantExpression) {
					var constantValueExpression = valueExpression as ConstantExpression;
					var parameter = state.GetNextParameter();
					if (constantValueExpression.Type == typeof(string) && constantValueExpression.Value != null) {
						string value = (string)constantValueExpression.Value;
						state.Parameters.Add(parameter, "%" + value);
						state.Where.AppendFormat(" ( t.[{0}] {1}LIKE @{2} )", fieldName, negative, parameter);
					}
					else
						throw new NotSupportedException();
				}
				else
					throw new NotSupportedException();
			}
			else if (body.Method == String_StartsWith || body.Method == String_StartsWithAndComparison) {
				string fieldName = (body.Object as MemberExpression).Member.Name;
				Expression valueExpression = body.Arguments[0];

				// if comparison is provide ensure it's one that SQL server will respect
				if (body.Arguments.Count == 2) CheckArgumentIsValue<StringComparison>(body.Arguments[1], StringComparison.OrdinalIgnoreCase, "StartsWith can only be used with StringComparison.OrdinalIgnoreCase");

				if (valueExpression is ConstantExpression) {
					var constantValueExpression = valueExpression as ConstantExpression;
					var parameter = state.GetNextParameter();
					if (constantValueExpression.Type == typeof(string) && constantValueExpression.Value != null) {
						string value = (string)constantValueExpression.Value;
						state.Parameters.Add(parameter, value + "%");
						state.Where.AppendFormat(" ( t.[{0}] {1}LIKE @{2} )", fieldName, negative, parameter);
					}
					else
						throw new NotSupportedException();
				}
				else
					throw new NotSupportedException();
			}
			else if (body.Method == String_Contains) {
				string fieldName = (body.Object as MemberExpression).Member.Name;
				Expression valueExpression = body.Arguments[0];
				if (valueExpression is ConstantExpression) {
					var constantValueExpression = valueExpression as ConstantExpression;
					var parameter = state.GetNextParameter();
					if (constantValueExpression.Type == typeof(string) && constantValueExpression.Value != null) {
						string value = (string)constantValueExpression.Value;
						state.Parameters.Add(parameter, "%" + value + "%");
						state.Where.AppendFormat(" ( t.[{0}] {1}LIKE @{2} )", fieldName, negative, parameter);
					}
					else
						throw new NotSupportedException();
				}
				else
					throw new NotSupportedException();
			}
			else
				throw new NotSupportedException();
		}

		private void MemberExpression<T>(MemberExpression body, QueryState<T> state, bool reversed = false)
		{
			if (body.Member.Name == Nullable_HasValue.Name) {
				string fieldName = (body.Expression as MemberExpression).Member.Name;
				state.Where.AppendFormat(" ( t.[{0}] IS {1}NULL ) ", fieldName, reversed ? string.Empty : "NOT ");
			}
			else
				throw new NotSupportedException();
		}
	}
}