using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Lithium.Extensions;

namespace Lithium
{
	public static class SqlMapper
	{
		private static readonly ConcurrentDictionary<QueryIdentity, QueryInfo> cache = new ConcurrentDictionary<QueryIdentity, QueryInfo>();
		private static readonly Dictionary<RuntimeTypeHandle, DbType> typeMap = new Dictionary<RuntimeTypeHandle, DbType>();

		private static readonly MethodInfo createParameter;
		private static readonly MethodInfo createParameterList;
		private static readonly MethodInfo convertStringToChar;
		private static readonly MethodInfo throwDataException;
		private static readonly MethodInfo getValueByIndex;
		private static readonly MethodInfo getValueByKey;

		static SqlMapper()
		{
			createParameter = typeof(SqlMapper).GetMethod("CreateParameter", BindingFlags.NonPublic | BindingFlags.Static);
			createParameterList = typeof(SqlMapper).GetMethod("CreateParameterList", BindingFlags.NonPublic | BindingFlags.Static);
			convertStringToChar = typeof(SqlMapper).GetMethod("ConvertStringToChar", BindingFlags.NonPublic | BindingFlags.Static);
			throwDataException = typeof(SqlMapper).GetMethod("ThrowDataException", BindingFlags.NonPublic | BindingFlags.Static);
			getValueByIndex = (from m in typeof(IDataRecord).GetProperties(BindingFlags.Instance | BindingFlags.Public)
							   where m.GetIndexParameters().Any() && m.GetIndexParameters()[0].ParameterType == typeof(int)
							   select m.GetGetMethod()).First();
			getValueByKey = (from m in typeof(IDictionary<string, object>).GetProperties(BindingFlags.Instance | BindingFlags.Public)
			                 where m.GetIndexParameters().Any() && m.GetIndexParameters()[0].ParameterType == typeof(string)
			                 select m.GetGetMethod()).First();

			#region TypeMap
			typeMap[typeof(byte).TypeHandle] = DbType.Byte;
			typeMap[typeof(byte?).TypeHandle] = DbType.Byte;
			typeMap[typeof(byte[]).TypeHandle] = DbType.Binary;
			typeMap[typeof(short).TypeHandle] = DbType.Int16;
			typeMap[typeof(short?).TypeHandle] = DbType.Int16;
			typeMap[typeof(int).TypeHandle] = DbType.Int32;
			typeMap[typeof(int?).TypeHandle] = DbType.Int32;
			typeMap[typeof(long).TypeHandle] = DbType.Int64;
			typeMap[typeof(long?).TypeHandle] = DbType.Int64;
			typeMap[typeof(float).TypeHandle] = DbType.Single;
			typeMap[typeof(float?).TypeHandle] = DbType.Single;
			typeMap[typeof(double).TypeHandle] = DbType.Double;
			typeMap[typeof(double?).TypeHandle] = DbType.Double;
			typeMap[typeof(decimal).TypeHandle] = DbType.Decimal;
			typeMap[typeof(decimal?).TypeHandle] = DbType.Decimal;
			typeMap[typeof(bool).TypeHandle] = DbType.Boolean;
			typeMap[typeof(bool?).TypeHandle] = DbType.Boolean;
			typeMap[typeof(string).TypeHandle] = DbType.String;
			typeMap[typeof(char).TypeHandle] = DbType.StringFixedLength;
			typeMap[typeof(char?).TypeHandle] = DbType.StringFixedLength;
			typeMap[typeof(Guid).TypeHandle] = DbType.Guid;
			typeMap[typeof(Guid?).TypeHandle] = DbType.Guid;
			typeMap[typeof(DateTime).TypeHandle] = DbType.DateTime;
			typeMap[typeof(DateTime?).TypeHandle] = DbType.DateTime;
			typeMap[typeof(DateTimeOffset).TypeHandle] = DbType.DateTimeOffset;
			typeMap[typeof(DateTimeOffset?).TypeHandle] = DbType.DateTimeOffset;
			#endregion
		}

		// public interface
		public static T Scalar<T>(this IDbConnection connection, string query, object parameters = null, IDbTransaction transaction = null)
		{
			return QueryInternal<T>(connection, query, parameters, transaction).SingleOrDefault();
		}
		public static IEnumerable<dynamic> Query(this IDbConnection connection, string query, object parameters = null, IDbTransaction transaction = null)
		{
			return QueryInternal<DynamicRow>(connection, query, parameters, transaction);
		}
		public static IEnumerable<T> Query<T>(this IDbConnection connection, string query, object parameters = null, IDbTransaction transaction = null)
		{
			return QueryInternal<T>(connection, query, parameters, transaction);
		}
		public static MultiResult QueryMulti(this IDbConnection connection, string query, object parameters = null, IDbTransaction transaction = null)
		{
			return QueryMultiInternal(connection, query, parameters, transaction);
		}
		public static int Execute(this IDbConnection connection, string query, object parameters = null, IDbTransaction transaction = null)
		{
			return ExecuteInternal(connection, query, parameters, transaction);
		}

		// internal interface
		internal static IEnumerable<dynamic> QueryInternal(this IDbConnection connection, string query, object parameters = null, IDbTransaction transaction = null, QueryIdentity identity = null)
		{
			return QueryInternal<DynamicRow>(connection, query, parameters, transaction, identity);
		}
		internal static IEnumerable<T> QueryInternal<T>(this IDbConnection connection, string query, object parameters = null, IDbTransaction transaction = null, QueryIdentity identity = null)
		{
			if (identity == null)
				identity = new QueryIdentity(connection.ConnectionString, query, typeof(T), parameters != null ? parameters.GetType() : null);

			QueryInfo info = GetQueryInfo(identity);
			if (info.ParameterGenerator == null && parameters != null)
				info.ParameterGenerator = GetParameterGenerator(parameters);

			using (IDbCommand command = SetupCommand(connection, query, info.ParameterGenerator, parameters, transaction))
			using (IDataReader reader = command.ExecuteReader()) {
				if (info.Deserializer == null)
					info.Deserializer = GetDeserializer<T>(reader);

				var deserializer = info.Deserializer as Func<IDataReader, T>;
				while (reader.Read())
					yield return deserializer(reader);
			}
		}
		internal static MultiResult QueryMultiInternal(this IDbConnection connection, string query, object parameters = null, IDbTransaction transaction = null, QueryIdentity identity = null)
		{
			if (identity == null)
				identity = new QueryIdentity(connection.ConnectionString, query, null, parameters != null ? parameters.GetType() : null);

			QueryInfo info = GetQueryInfo(identity);
			if (info.ParameterGenerator == null)
				info.ParameterGenerator = GetParameterGenerator(parameters);

			IDbCommand command = null;
			IDataReader reader = null;
			try {
				command = SetupCommand(connection, query, info.ParameterGenerator, parameters, transaction);
				reader = command.ExecuteReader();
				return new MultiResult(command, reader, identity);
			}
			catch {
				if (reader != null) reader.Dispose();
				if (command != null) command.Dispose();
				throw;
			}
		}
		internal static int ExecuteInternal(this IDbConnection connection, string query, object parameters = null, IDbTransaction transaction = null, QueryIdentity identity = null)
		{
			if (identity == null)
				identity = new QueryIdentity(connection.ConnectionString, query, null, parameters != null ? parameters.GetType() : null);

			QueryInfo info = GetQueryInfo(identity);
			if (info.ParameterGenerator == null && parameters != null)
				info.ParameterGenerator = GetParameterGenerator(parameters);

			using (IDbCommand command = SetupCommand(connection, query, info.ParameterGenerator, parameters, transaction)) {
				return command.ExecuteNonQuery();
			}
		}

		// parameter generators
		private static Action<IDbCommand, object> GetParameterGenerator(object parameters)
		{
			if (parameters is List<Parameter>)
				return GetStaticParameterGenerator(parameters);
			
			return GetAnonymousParameterGenerator(parameters);
		}
		private static Action<IDbCommand, object> GetStaticParameterGenerator(object parameters)
		{
			return (command, entity) => {
				var list = parameters as List<Parameter>;

				foreach (var parameter in list)
					CreateParameter(command, parameter.Name, parameter.Value, (int)GetDbType(parameter.Type));
			};
		}
		private static Action<IDbCommand, object> GetAnonymousParameterGenerator(object parameters)
		{
			var type = parameters.GetType();
			var dm = new DynamicMethod("ParameterGenerator_" + Guid.NewGuid(), null, new[] { typeof(IDbCommand), typeof(object) }, type, true);
			var il = dm.GetILGenerator();

			// type parameters object and store it in slot 0
			il.DeclareLocal(type);
			il.Emit(OpCodes.Ldarg_1); // [untyped parameters object]
			il.Emit(OpCodes.Unbox_Any, type); // [typed parameters object]
			il.Emit(OpCodes.Stloc_0); // stack is empty

			foreach (var property in GetPropertyInfo(parameters)) {
				il.Emit(OpCodes.Ldarg_0); // [command]
				il.Emit(OpCodes.Ldstr, property.Name); // [command] [name]
				il.Emit(OpCodes.Ldloc_0); // [command] [name] [typed parameter]

				if (property.Getter != null) {
					il.Emit(OpCodes.Callvirt, property.Getter); // [command] [name] [typed value]
					il.Emit(OpCodes.Box, property.Type); // [command] [name] [boxed value]
				}
				else {
					il.Emit(OpCodes.Ldstr, property.Name); // [command] [name] [typed parameter] [name]
					il.Emit(OpCodes.Callvirt, getValueByKey); // [command] [name] [typed value]
				}
				
				var dbType = GetDbType(property.Type);
				if (dbType != DbType.Xml) {
					il.Emit(OpCodes.Ldc_I4, (int)dbType); // [command] [name] [typed value] [dbtype]
					il.Emit(OpCodes.Call, createParameter); // stack is empty
				}
				else {
					il.Emit(OpCodes.Call, createParameterList); // stack is empty
				}
			}

			il.Emit(OpCodes.Ret);
			return dm.CreateDelegate(typeof(Action<IDbCommand, object>)) as Action<IDbCommand, object>;
		}
		private static void CreateParameter(IDbCommand command, string name, object value, int dbType)
		{
			var parameter = command.CreateParameter();
			var type = (DbType)dbType;
			parameter.ParameterName = name;
			parameter.DbType = type;
			parameter.Value = value ?? DBNull.Value;
			parameter.Direction = ParameterDirection.Input;

			if (value != null) {
				switch (type) {
					case DbType.String:
						parameter.Size = value.ToString().Length <= 4000 ? 4000 : -1;
						break;
					case DbType.StringFixedLength:
						parameter.Size = value.ToString().Length;
						break;
				}
			}

			command.Parameters.Add(parameter);
		}
		private static void CreateParameterList(IDbCommand command, string name, object value)
		{
			if (value == null)
				return;

			int count = 0;
			var dbType = -1;
			var list = (IEnumerable)value;

			foreach (var item in list) {
				count++;

				if (dbType == -1)
					dbType = (int)GetDbType(item.GetType());

				CreateParameter(command, name + count, item, dbType);
			}

			name = "@" + name;
			var builder = new StringBuilder("(").Append(name).Append(1);
			for (var i = 2; i <= count; i++) builder.Append(",").Append(name).Append(i);
			var names = builder.Append(")").ToString();

			command.CommandText = command.CommandText.Replace(name, names);
		}
		private static IEnumerable<ParameterInfo> GetPropertyInfo(object parameters)
		{
			if (parameters is Dictionary<string, object>) {
				var dictionary = parameters as Dictionary<string, object>;
				return dictionary.Select(d => new ParameterInfo {
					Name = d.Key,
					Type = d.Value != null ? d.Value.GetType() : typeof(string)
				});
			}

			return parameters.GetType().GetProperties().Select(p => new ParameterInfo {
				Name = p.Name,
				Type = p.PropertyType,
				Getter = p.GetGetMethod()
			});
		}

		// deserializers
		internal static Func<IDataReader, T> GetDeserializer<T>(IDataRecord dataRecord)
		{
			var t = typeof(T);

			if (t == typeof(object) || t == typeof(DynamicRow))
				return GetDynamicDeserializer<T>(dataRecord);

			if (t.IsClass && t != typeof(string))
				return GetClassDeserializer<T>(dataRecord);

			return GetStructDeserializer<T>();
		}
		private static Func<IDataReader, T> GetClassDeserializer<T>(IDataRecord dataRecord)
		{
			var t = typeof(T);
			int index = -1;

			// select all columns from the resultset
			var columns = new List<string>();
			for (var i = 0; i < dataRecord.FieldCount; i++)
				columns.Add(dataRecord.GetName(i));

			// select all properties and fields
			var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			var properties = from p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
							 let setMethod = p.DeclaringType == t ? p.GetSetMethod(true) : p.DeclaringType.GetProperty(p.Name).GetSetMethod(true)
							 where setMethod != null
							 select new {
								 Name = p.Name,
								 Type = p.PropertyType,
								 SetMethod = setMethod
							 };

			// setters
			var setters = (from c in columns
						   let property = properties.FirstOrDefault(p => string.Equals(p.Name, c, StringComparison.InvariantCulture)) ??								// property case sensitive first
										  properties.FirstOrDefault(p => string.Equals(p.Name, c, StringComparison.InvariantCultureIgnoreCase))							// property case insensitive second
						   let field = property != null ? null : (fields.FirstOrDefault(p => string.Equals(p.Name, c, StringComparison.InvariantCulture)) ??			// field case sensitive third
																  fields.FirstOrDefault(p => string.Equals(p.Name, c, StringComparison.InvariantCultureIgnoreCase)))	// field case insensitive fourth
						   select new {
							   Name = c,
							   Property = property,
							   Field = field
						   }).ToList();

			var dm = new DynamicMethod(string.Format("Deserializer_{0}", Guid.NewGuid()), t, new[] { typeof(IDataReader) }, true);
			var il = dm.GetILGenerator();
			il.DeclareLocal(typeof(int));
			il.Emit(OpCodes.Ldc_I4, index);
			il.Emit(OpCodes.Stloc_0);

			il.BeginExceptionBlock();
			{
				// create instance of T
				il.DeclareLocal(t);
				il.Emit(OpCodes.Newobj, t.GetConstructor(Type.EmptyTypes)); // [result]

				foreach (var setter in setters) {
					index += 1;

					if (setter.Property == null && setter.Field == null)
						continue;

					Type memberType = setter.Property != null ? setter.Property.Type : setter.Field.FieldType;
					Type nullUnderlyingType = Nullable.GetUnderlyingType(memberType);
					Type unboxType = nullUnderlyingType ?? memberType;

					// create labels to jump to
					var nullLabel = il.DefineLabel();
					var nextLabel = il.DefineLabel();

					il.Emit(OpCodes.Dup); // [result] [result]

					il.Emit(OpCodes.Ldarg_0); // [result] [result] [reader]
					il.EmitInt32(index); // [result] [result] [reader] [index]
					il.Emit(OpCodes.Dup); // [result] [result] [reader] [index] [index]
					il.Emit(OpCodes.Stloc_0); // [result] [result] [reader] [index]
					il.Emit(OpCodes.Callvirt, getValueByIndex); // [result] [result] [untyped value]

					il.Emit(OpCodes.Dup); // [result] [result] [untyped value] [untyped value]
					il.Emit(OpCodes.Isinst, typeof(DBNull)); // [result] [result] [untyped value] [DBNull or null]
					il.Emit(OpCodes.Brtrue_S, nullLabel); // [result] [result] [untyped value], value is null

					// a char value is returned as a string and its not possible to implicitly cast it, so we have to convert it
					if (unboxType == typeof(char))
						il.Emit(OpCodes.Call, convertStringToChar); // [result] [result] [untyped char]

					il.Emit(OpCodes.Unbox_Any, memberType); // [result] [result] [typed value]

					// set value
					if (setter.Property != null)
						il.Emit(OpCodes.Callvirt, setter.Property.SetMethod); // [result]
					else
						il.Emit(OpCodes.Stfld, setter.Field); // [result]

					// jump to the next setter
					il.Emit(OpCodes.Br, nextLabel);

					// value was null so clear the stack and jump to the next setter
					il.MarkLabel(nullLabel); // [result] [result] [untyped value]
					il.Emit(OpCodes.Pop); // [result] [result]
					il.Emit(OpCodes.Pop); // [result]

					// end of the "loop"
					il.MarkLabel(nextLabel); // [result]
				}

				// store the result
				il.Emit(OpCodes.Stloc_1);
			}
			il.BeginCatchBlock(typeof(Exception)); // [exception]
			{
				il.Emit(OpCodes.Ldloc_0); // [exception] [index]
				il.Emit(OpCodes.Ldarg_0); // [exception] [index] [reader]
				il.Emit(OpCodes.Call, throwDataException); // stack is empty
				il.Emit(OpCodes.Ldnull); // [null]
				il.Emit(OpCodes.Stloc_1); // store value null over the result (slot 0)
			}
			il.EndExceptionBlock();

			il.Emit(OpCodes.Ldloc_1); // load result
			il.Emit(OpCodes.Ret);
			return dm.CreateDelegate(typeof(Func<IDataReader, T>)) as Func<IDataReader, T>;
		}
		private static Func<IDataReader, T> GetDynamicDeserializer<T>(IDataRecord dataRecord)
		{
			int fieldCount = dataRecord.FieldCount;

			return r => {
				var row = new Dictionary<string, object>(fieldCount);

				for (var i = 0; i < fieldCount; i++) {
					object value = r.GetValue(i);
					row[r.GetName(i)] = value != DBNull.Value ? value : null;
				}

				return (T)(object)new DynamicRow(row);
			};
		}
		private static Func<IDataReader, T> GetStructDeserializer<T>()
		{
			if (typeof(T) == typeof(char) || typeof(T) == typeof(char?)) {
				return r => {
					var value = r.GetValue(0);
					if (value == DBNull.Value)
						return (T)(null as object);

					return (T)(value.ToString()[0] as object);
				};
			}

			return r => {
				var value = r.GetValue(0);
				if (value == DBNull.Value)
					value = null;

				try {
					// TODO: replace this temp fix, sqlce returns a decimal for @@identity
					if (r.GetFieldType(0) == typeof(decimal) && typeof(T) != typeof(decimal) && typeof(T) != typeof(decimal?))
					    return (T)Convert.ChangeType(value, typeof(T));

					return (T)value;
				}
				catch (Exception ex) {
					throw new DataException(string.Format(@"Error casting ""{0}"" from [{1}] to [{2}]", value, r.GetFieldType(0).Name, typeof(T).Name), ex);
				}
			};
		}

		// query info
		internal static QueryInfo GetQueryInfo(QueryIdentity identity)
		{
			QueryInfo info;
			if (!cache.TryGetValue(identity, out info)) {
				info = new QueryInfo();
				cache[identity] = info;
			}

			return info;
		}
		internal static void DeleteQueryInfo(QueryIdentity identity)
		{
			QueryInfo info;
			cache.TryRemove(identity, out info);
		}

		// helpers
		private static IDbCommand SetupCommand(IDbConnection connection, string query, Action<IDbCommand, object> parameterGenerator = null, object parameters = null, IDbTransaction transaction = null)
		{
			IDbCommand command = connection.CreateCommand();
			command.Connection = connection;
			command.CommandText = query;
			command.CommandType = CommandType.Text;

			if (transaction != null)
				command.Transaction = transaction;

			if (parameterGenerator != null)
				parameterGenerator(command, parameters);

			return command;
		}
		private static DbType GetDbType(Type type)
		{
			DbType dbType;
			if (typeMap.TryGetValue(type.TypeHandle, out dbType))
				return dbType;

			if (typeof(IEnumerable).IsAssignableFrom(type))
				return DbType.Xml;

			if (type.IsEnum)
				return DbType.Int32;

			throw new NotSupportedException(string.Format(@"Type [{0}] is not supported", type));
		}
		private static void ThrowDataException(Exception ex, int index, IDataRecord reader)
		{
			if (reader != null && index >= 0 && index < reader.FieldCount) {
				string name = reader.GetName(index);
				object value = reader.GetValue(index);
				Type type = reader.GetFieldType(index);

				if (value == null || value is DBNull)
					value = "<null>";
				else
					value = string.Format("\"{0}\" [{1}]", Convert.ToString(value, CultureInfo.InvariantCulture), type);

				throw new DataException(string.Format(@"Error parsing column: {0} = {1}", name, value), ex);
			}

			throw new DataException(string.Format(@"Error parsing column {0}", index), ex);
		}
		private static object ConvertStringToChar(object value)
		{
			return (value as string)[0];
		}
	}
}