using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Lithium.Linq
{
	public class QueryProvider : IQueryProvider
	{
		private readonly IDbConnection connection;

		public QueryProvider(IDbConnection connection)
		{
			this.connection = connection;
		}

		public IQueryable<T> CreateQuery<T>(Expression expression)
		{
			return new Query<T>(connection, expression);
		}

		public IQueryable CreateQuery(Expression expression)
		{
			throw new NotImplementedException();
		}

		public T Execute<T>(Expression expression)
		{
			List<T> results = CreateQuery<T>(expression).ToList();

			switch (results.Count) {
				case 1: return results[0];
				case 0: return default(T);
				default:
					throw new Exception("Query returned more than one result");
			}
		}

		public object Execute(Expression expression)
		{
			throw new NotImplementedException();
		}
	}
}