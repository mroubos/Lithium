using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Lithium
{
	public interface IContext
	{
		Query<T> Query<T>();
		EntityMetaData<T> Entity<T>();
	}

	public class EntityMetaData<T>
	{
		public EntityMetaData<T> TableName(string name, string schema = "dbo")
		{
			return this;
		}

		public EntityMetaData<T> PrimaryKey<TProperty>(Expression<Func<T, TProperty>> property)
		{
			return this;
		}
	}

	public class Query<T> : IQueryable<T>, IOrderedQueryable<T>
	{
		public IEnumerator<T> GetEnumerator()
		{
			throw new NotImplementedException();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			throw new NotImplementedException();
		}

		public Type ElementType
		{
			get { throw new NotImplementedException(); }
		}

		public Expression Expression
		{
			get { throw new NotImplementedException(); }
		}

		public IQueryProvider Provider
		{
			get { throw new NotImplementedException(); }
		}
	}

	public class Context : IContext, IDisposable
	{
		public Query<T> Query<T>()
		{
			throw new NotImplementedException();
		}

		public EntityMetaData<T> Entity<T>()
		{
			// check cache, otherwise return a new instance
			throw new NotImplementedException();
		}

		void IDisposable.Dispose()
		{
			throw new NotImplementedException();
		}
	}
}