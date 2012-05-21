using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Lithium.SimpleExtensions
{
	public static class SimpleExtensions
	{
		private const string SelectIdentityQuery = "select @@identity";
		private const string InsertQuery = "insert into {0} ({1}) values ({2})";
		private const string UpdateQuery = "update {0} set {1} where {2}";
		private const string DeleteQuery = "delete from {0} where {1}";
		private const string StoredProcedureQuery = "exec {0} {1}";

		private static readonly MethodInfo addItem = typeof(IDictionary).GetMethod("Add", new[] { typeof(object), typeof(object) });

		// stored procedure
		public static IEnumerable<T> StoredProcedure<T>(this IDbConnection connection, string storedProcedureName, object parameters = null, IDbTransaction transaction = null)
		{
			QueryIdentity identity = new QueryIdentity(connection.ConnectionString, storedProcedureName, typeof(T), parameters != null ? parameters.GetType() : null);
			QueryInfo info = SqlMapper.GetQueryInfo(identity);

			if (info.Query == null ) {
				info.Query = GenerateStoredProcedureQuery(storedProcedureName, parameters);
			}

			return connection.QueryInternal<T>(info.Query, parameters, transaction, identity);
		}
		public static MultiResult StoredProcedureMulti(this IDbConnection connection, string storedProcedureName, object parameters = null, IDbTransaction transaction = null)
		{
			QueryIdentity identity = new QueryIdentity(connection.ConnectionString, storedProcedureName, typeof(MultiResult), parameters != null ? parameters.GetType() : null);
			QueryInfo info = SqlMapper.GetQueryInfo(identity);

			if (info.Query == null) {
				info.Query = GenerateStoredProcedureQuery(storedProcedureName, parameters);
			}

			return connection.QueryMultiInternal(info.Query, parameters, transaction, identity);
		}
		private static string GenerateStoredProcedureQuery(string storedProcedureName, object param)
		{
			var properties = param.GetType().GetProperties();
			return string.Format(StoredProcedureQuery,
								 storedProcedureName,
								 string.Join(", ", properties.Select(l => string.Concat("@", l.Name)).ToArray()));
		}

		// insert
		public static void Insert(this IDbConnection connection, string tableName, object parameters)
		{
			Insert<object>(connection, tableName, parameters);
		}
		public static T Insert<T>(this IDbConnection connection, string tableName, object parameters)
		{
			QueryIdentity identity = new QueryIdentity(connection.ConnectionString, "insert " + tableName, null, parameters.GetType());
			QueryInfo info = SqlMapper.GetQueryInfo(identity);

			if (info.Query == null) {
				info.Query = GenerateInsertQuery(tableName, parameters);
			}

			connection.ExecuteInternal(info.Query, parameters, null, identity);
			return (typeof(T) != typeof(object))
				? connection.QueryInternal<T>(SelectIdentityQuery).Single()
				: default(T);
		}
		private static string GenerateInsertQuery(string name, object parameters)
		{
			var properties = parameters.GetType().GetProperties();
			return string.Format(InsertQuery,
								 name,
								 string.Join(", ", properties.Select(l => l.Name).ToArray()),
								 string.Join(", ", properties.Select(l => "@" + l.Name).ToArray()));
		}

		// update
		public static int Update(this IDbConnection connection, string tableName, object parameters, object parametersWhere)
		{
			QueryIdentity identity = new QueryIdentity(connection.ConnectionString, "update " + tableName, null, parameters.GetType(), parametersWhere.GetType());
			QueryInfo info = SqlMapper.GetQueryInfo(identity);

			if (info.Query == null) {
				info.Query = GenerateUpdateQuery(tableName, parameters, parametersWhere);
				info.GetParameterCombiner = GetParameterCombiner(parameters, parametersWhere);
			}
			
			return connection.ExecuteInternal(info.Query, info.GetParameterCombiner(parameters, parametersWhere), null, identity);
		}
		private static string GenerateUpdateQuery(string name, object parameters, object parametersWhere)
		{
			var properties = parameters.GetType().GetProperties();
			var propertiesWhere = parametersWhere.GetType().GetProperties();
			return string.Format(UpdateQuery,
								 name,
								 string.Join(", ", properties.Select(l => string.Concat(l.Name, " = @", l.Name)).ToArray()),
								 string.Join(" and ", propertiesWhere.Select(l => string.Concat(l.Name, " = @", l.Name, "2")).ToArray()));
		}
		private static Func<object, object, object> GetParameterCombiner(object parameters, object additionalParameters)
		{
			var returnType = typeof(Dictionary<string, object>);
			var parametersType = parameters.GetType();
			var additionalParametersType = additionalParameters.GetType();
			var dm = new DynamicMethod("Combiner_" + Guid.NewGuid(), typeof(object), new[] { typeof(object), typeof(object) }, returnType, true);
			var il = dm.GetILGenerator();

			il.DeclareLocal(parametersType);
			il.Emit(OpCodes.Ldarg_0); // [untyped parameters]
			il.Emit(OpCodes.Unbox_Any, parametersType); // [typed parameters]
			il.Emit(OpCodes.Stloc_0); // stack is leeg

			il.DeclareLocal(additionalParametersType);
			il.Emit(OpCodes.Ldarg_1); // [untyped parameters]
			il.Emit(OpCodes.Unbox_Any, additionalParametersType); // [typed parameters]
			il.Emit(OpCodes.Stloc_1); // stack is leeg

			il.DeclareLocal(returnType);
			il.Emit(OpCodes.Newobj, returnType.GetConstructor(Type.EmptyTypes)); // [dictionary]

			foreach (var parameter in parametersType.GetProperties()) {
				il.Emit(OpCodes.Dup); // [dictionary] [dictionary]
				il.Emit(OpCodes.Ldstr, parameter.Name); // [dictionary] [dictionary] [name]
				il.Emit(OpCodes.Ldloc_0); // [dictionary] [dictionary] [name] [parameters]
				il.Emit(OpCodes.Callvirt, parameter.GetGetMethod()); // [dictionary] [dictionary] [name] [typed value]
				il.Emit(OpCodes.Box, parameter.PropertyType); // [dictionary] [dictionary] [name] [untyped value]
				il.Emit(OpCodes.Callvirt, addItem); // [dictionary]
			}

			foreach (var parameter in additionalParametersType.GetProperties()) {
				il.Emit(OpCodes.Dup); // [dictionary] [dictionary]
				il.Emit(OpCodes.Ldstr, parameter.Name + "2"); // [dictionary] [dictionary] [name]
				il.Emit(OpCodes.Ldloc_1); // [dictionary] [dictionary] [parameters]
				il.Emit(OpCodes.Callvirt, parameter.GetGetMethod()); // [dictionary] [dictionary] [typed value]
				il.Emit(OpCodes.Box, parameter.PropertyType); // [dictionary] [dictionary] [untyped value]
				il.Emit(OpCodes.Callvirt, addItem); // [dictionary]
			}

			il.Emit(OpCodes.Ret);
			return dm.CreateDelegate(typeof(Func<object, object, object>)) as Func<object, object, object>;
		}

		// delete
		public static int Delete(this IDbConnection connection, string tableName, object parameters)
		{
			QueryIdentity identity = new QueryIdentity(connection.ConnectionString, "delete " + tableName, null, parameters.GetType());
			QueryInfo info = SqlMapper.GetQueryInfo(identity);

			if (info.Query == null) {
				info.Query = GenerateDeleteQuery(tableName, parameters);
			}

			return connection.ExecuteInternal(info.Query, parameters, null, identity);
		}
		private static string GenerateDeleteQuery(string tableName, object parameters)
		{
			var properties = parameters.GetType().GetProperties();
			return string.Format(DeleteQuery,
								 tableName,
								 string.Join(" and ", properties.Select(l => string.Concat(l.Name, " = @", l.Name)).ToArray()));
		}
	}
}