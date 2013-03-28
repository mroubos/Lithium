using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace Lithium.EntityExtensions
{
	public static class EntityMapper
	{
		private static readonly Dictionary<Type, object> maps = new Dictionary<Type, object>();

		// map
		public static EntityMap<T> Entity<T>(this IDbConnection connection)
		{
			object entityMap;
			if (maps.TryGetValue(typeof(T), out entityMap) == false) {
				var newEntityMap = new EntityMap<T>();
				maps.Add(typeof(T), newEntityMap);

				return newEntityMap;
			}

			return entityMap as EntityMap<T>;
		}

		// select
		public static IEnumerable<T> Select<T>(this IDbConnection connection)
		{
			EntityMap<T> entityMap = connection.Entity<T>();
			return connection.QueryInternal<T>(CommandType.Text, entityMap.SelectQuery);
		}
		public static T Select<T>(this IDbConnection connection, int id)
		{
			return connection.Select<T, int>(id);
		}
		public static T Select<T>(this IDbConnection connection, long id)
		{
			return connection.Select<T, long>(id);
		}
		private static T Select<T, TID>(this IDbConnection connection, TID id)
		{
			EntityMap<T> entityMap = connection.Entity<T>();
			return connection.QueryInternal<T>(CommandType.Text, entityMap.SelectWhereIdQuery, new { id }).SingleOrDefault();
		}

		/// <summary>
		/// Currently only supports a single equals statement i.e.: x => x.Name == "John"
		/// </summary>
		public static IEnumerable<T> Select<T>(this IDbConnection connection, Expression<Func<T, bool>> predicate)
		{
			EntityMap<T> entityMap = connection.Entity<T>();

			// todo: cache
			BinaryExpression expression = (BinaryExpression)predicate.Body;
			MemberExpression member = (MemberExpression)expression.Left;

			object value = null;
			Type type = typeof(object);
			if (expression.Right.NodeType == ExpressionType.Constant) {
				ConstantExpression left = (ConstantExpression)expression.Right;

				value = left.Value;
				type = left.Type;
			}
			else if (expression.Right.NodeType == ExpressionType.MemberAccess) {
				MemberExpression right = (MemberExpression)expression.Right;

				value = Expression.Lambda(right).Compile().DynamicInvoke();
				type = right.Type;
			}

			var parameters = new Parameters();
			parameters.Add(member.Member.Name, value, type);
			// todo: cache

			return connection.QueryInternal<T>(CommandType.Text, entityMap.GetSelectWhereQuery(predicate), parameters);
		}
		public static T Scalar<T>(this IDbConnection connection, Expression<Func<T, bool>> predicate)
		{
			return Select(connection, predicate).SingleOrDefault();
		}

		// insert
		public static void Insert<T>(this IDbConnection connection, T entity)
		{
			EntityMap<T> entityMap = connection.Entity<T>();

			var parameters = entityMap.AutoIncrement 
				? entityMap.ParameterRemover(entity) 
				: entity;

			connection.ExecuteInternal(CommandType.Text, entityMap.InsertQuery, parameters);
			if (entityMap.AutoIncrement) {
				dynamic result = connection.QueryInternal(CommandType.Text, "select @@identity ID").Single();
				entityMap.IdentitySetter(entity, result.ID);
			}
		}		

		// update
		public static int Update<T>(this IDbConnection connection, T entity)
		{
			EntityMap<T> entityMap = connection.Entity<T>();
			return connection.ExecuteInternal(CommandType.Text, entityMap.UpdateQuery, entity);
		}

		// delete
		public static bool Delete<T>(this IDbConnection connection, int id)
		{
			return connection.Delete<T, int>(id);
		}
		public static bool Delete<T>(this IDbConnection connection, long id)
		{
			return connection.Delete<T, long>(id);
		}
		public static bool Delete<T>(this IDbConnection connection, T entity)
		{
			EntityMap<T> entityMap = connection.Entity<T>();

			return connection.Execute(entityMap.DeleteQuery, new List<Parameter> {
				new Parameter {
					Name = "id",
					Type = entityMap.IdentityType,
					Value = entityMap.IdentityGetter(entity),
					Direction = ParameterDirection.Input
				}
			}) == 0;
		}
		private static bool Delete<T, TID>(this IDbConnection connection, TID id)
		{
			EntityMap<T> entityMap = connection.Entity<T>();
			return connection.Execute(entityMap.DeleteQuery, new { id }) == 0;
		}
	}
}