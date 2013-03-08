using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Lithium.Linq
{
	public class QueryBuilder<T>
	{
		public string Query { get { EvaluateExpression(); return query; } }
		public Parameters Parameters { get { EvaluateExpression(); return parameters; } }

		private bool evaluated = false;
		private string query;
		private Parameters parameters;
		private readonly Expression expression;

		private static ConcurrentDictionary<Type, TableInfo> tableInfoCache = new ConcurrentDictionary<Type, TableInfo>();

		public QueryBuilder(Expression expression)
		{
			this.expression = expression;
		}		

		private void EvaluateExpression()
		{
			if (evaluated)
				return;

			var state = new QueryState<T>();
			var current = expression;

			while (current != null) {
				if (current is ConstantExpression)
					break;

				if ((current is MethodCallExpression) == false)
					throw new NotImplementedException("Expression is not handled");

				var methodCallExpression = current as MethodCallExpression;
				if (methodCallExpression.Arguments.Count == 0)
					throw new NotSupportedException("Method call expression must have at least one argument");

				// process each method
				ExtensionEvaluator.Process(methodCallExpression.Method.Name)
								  .Evaluate(methodCallExpression, state);

				// up the stack of expressions
				current = methodCallExpression.Arguments[0];
			}

			query = BuildQuery(state);
			if (state.HasParameters)
				parameters = state.Parameters;

			evaluated = true;
		}

		internal class TableInfo
		{
			public string SchemaName;
			public string TableName;
			public List<string> FieldNames = new List<string>();
			public List<string> PrimaryKeyFieldNames = new List<string>();
		}

		internal static TableInfo GetTableInfo(Type type)
		{
			if (tableInfoCache.ContainsKey(type))
				return tableInfoCache[type];

			TableInfo table = new TableInfo();
			table.TableName = type.Name;
			table.SchemaName = "dbo";

			// get the property names that can be mapped
			foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
				table.FieldNames.Add(property.Name);

			// try to add the newly aquired info
			tableInfoCache[type] = table;

			return table;
		}

		private static string BuildQuery(QueryState<T> state)
		{
			// get the database table name from the type or the table attribute
			TableInfo table = GetTableInfo(typeof(T));

			// if there were no specific fields then add the field names from the type
			if (state.FieldNames.Count == 0)
				state.FieldNames.AddRange(table.FieldNames);

			var sb = new StringBuilder("SELECT ");

			//// append the top + fields
			//if (state.Top.HasValue) {
			//	state.Parameters.Add("top", state.Top.Value, DbType.Int32);
			//	sb.Append("TOP (@top) ");
			//}

			// distinct
			if (state.Distinct) sb.Append("DISTINCT ");

			// field list
			sb.Append(string.Join(", ", state.FieldNames.Select(m => "t.[" + m + "]")));

			// append the from
			sb.AppendFormat(" FROM [{0}].[{1}] AS t ", table.SchemaName, table.TableName);

			//// append the hints
			//if (state.Hints.Count > 0) {
			//	sb.Append("WITH (");
			//	sb.Append(string.Join(", ", state.Hints));
			//	sb.Append(")");
			//}

			//// append the where clause
			if (state.Where.Length != 0) {
				sb.Append("WHERE ");
				sb.Append(state.Where.ToString());
			}

			//// append the order by
			//if (state.OrderBy.Count > 0) {
			//	state.OrderBy.Reverse();
			//	sb.Append("ORDER BY ");
			//	sb.Append(string.Join(", ", state.OrderBy));
			//}

			return sb.ToString();
		}
	}
}