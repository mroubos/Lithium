using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Lithium.EntityExtensions
{
	public static class EntityMapper
	{
		private const string SelectQuery = "select {0} from {1} where {2} = @id";
		private const string SelectIdentityQuery = "select @@identity ID";
		private const string InsertQuery = "insert into {0} ({1}) values ({2})";
		private const string UpdateQuery = "update {0} set {1} where {2}";
		private const string DeleteQuery = "delete from {0} where {1} = @id";

		private static readonly Dictionary<Type, object> maps = new Dictionary<Type, object>();
		private static readonly MethodInfo addItem = typeof(IDictionary).GetMethod("Add", new[] { typeof(object), typeof(object) });

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
			if (entityMap.SelectQuery == null)
				entityMap.SelectQuery = GenerateSelectQuery(entityMap);

			return connection.QueryInternal<T>(entityMap.SelectQuery, new { id }).SingleOrDefault();
		}
		private static string GenerateSelectQuery<T>(EntityMap<T> entityMap)
		{
			var properties = typeof(T).GetProperties().Select(p => p.Name).ToArray();

			return string.Format(SelectQuery, string.Join(",", properties), entityMap.TableName, entityMap.IdentityName);
		}

		// insert
		public static void Insert<T>(this IDbConnection connection, T entity)
		{
			EntityMap<T> entityMap = connection.Entity<T>();
			if (entityMap.InsertQuery == null) {
				entityMap.InsertQuery = GenerateInsertQuery(entityMap);

				if (entityMap.AutoIncrement)
					entityMap.ParameterRemover = GetParameterRemover(entity, entityMap.IdentityName);
			}

			var parameters = entityMap.AutoIncrement 
				? entityMap.ParameterRemover(entity) 
				: entity;

			connection.ExecuteInternal(entityMap.InsertQuery, parameters);
			if (entityMap.AutoIncrement) {
				var result = connection.QueryInternal(SelectIdentityQuery).Single();
				entityMap.IdentitySetter(entity, result.ID);
			}
		}
		private static string GenerateInsertQuery<T>(EntityMap<T> entityMap)
		{
			var properties = typeof(T).GetProperties();
			return string.Format(InsertQuery,
								 entityMap.TableName,
								 string.Join(",", properties.Where(p => entityMap.AutoIncrement == false || p.Name != entityMap.IdentityName).Select(l => l.Name).ToArray()),
								 string.Join(",", properties.Where(p => entityMap.AutoIncrement == false || p.Name != entityMap.IdentityName).Select(l => "@" + l.Name).ToArray()));
		}
		private static Func<object, object> GetParameterRemover(object parameters, string parameter)
		{
			var returnType = typeof(Dictionary<string, object>);
			var parametersType = parameters.GetType();
			var dm = new DynamicMethod("ParameterRemover_" + Guid.NewGuid(), typeof(object), new[] { typeof(object) }, returnType, true);
			var il = dm.GetILGenerator();

			il.DeclareLocal(parametersType);
			il.Emit(OpCodes.Ldarg_0); // [untyped parameters]
			il.Emit(OpCodes.Unbox_Any, parametersType); // [typed parameters]
			il.Emit(OpCodes.Stloc_0); // stack is leeg

			il.DeclareLocal(returnType);
			il.Emit(OpCodes.Newobj, returnType.GetConstructor(Type.EmptyTypes)); // [dictionary]

			foreach (var p in parametersType.GetProperties().Where(p => p.Name != parameter)) {
				il.Emit(OpCodes.Dup); // [dictionary] [dictionary]
				il.Emit(OpCodes.Ldstr, p.Name); // [dictionary] [dictionary] [name]
				il.Emit(OpCodes.Ldloc_0); // [dictionary] [dictionary] [name] [parameters]
				il.Emit(OpCodes.Callvirt, p.GetGetMethod()); // [dictionary] [dictionary] [name] [typed value]
				il.Emit(OpCodes.Box, p.PropertyType); // [dictionary] [dictionary] [name] [boxed value]
				il.Emit(OpCodes.Callvirt, addItem); // [dictionary]
			}

			il.Emit(OpCodes.Ret);
			return dm.CreateDelegate(typeof(Func<object, object>)) as Func<object, object>;
		}

		// update
		public static int Update<T>(this IDbConnection connection, T entity)
		{
			EntityMap<T> entityMap = connection.Entity<T>();
			if (entityMap.UpdateQuery == null) {
				entityMap.UpdateQuery = GenerateUpdateQuery(entityMap);
			}

			return connection.ExecuteInternal(entityMap.UpdateQuery, entity);
		}
		private static string GenerateUpdateQuery<T>(EntityMap<T> entityMap)
		{
			var properties = typeof(T).GetProperties();
			return string.Format(UpdateQuery,
			                     entityMap.TableName,
			                     string.Join(",", properties.Where(p => p.Name != entityMap.IdentityName).Select(l => string.Format(@"{0}=@{0}", l.Name)).ToArray()),
			                     string.Format(@"{0}=@{0}", entityMap.IdentityName));
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
			if (entityMap.DeleteQuery == null)
				entityMap.DeleteQuery = GenerateDeleteQuery(entityMap);

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
			if (entityMap.DeleteQuery == null)
				entityMap.DeleteQuery = GenerateDeleteQuery(entityMap);

			return connection.Execute(entityMap.DeleteQuery, new { id }) == 0;
		}
		private static string GenerateDeleteQuery<T>(EntityMap<T> entityMap)
		{
			return string.Format(DeleteQuery, entityMap.TableName, entityMap.IdentityName);
		}
	}
}