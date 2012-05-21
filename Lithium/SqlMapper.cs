using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using Lithium.Extensions;

namespace Lithium
{
	public static class SqlMapper
	{
		private static readonly ConcurrentDictionary<QueryIdentity, QueryInfo> cache = new ConcurrentDictionary<QueryIdentity, QueryInfo>();
		private static readonly Dictionary<RuntimeTypeHandle, DbType> typeMap = new Dictionary<RuntimeTypeHandle, DbType>();

		private static readonly MethodInfo createParameter;
		private static readonly MethodInfo createParameterList;
		private static readonly MethodInfo readChar;
		private static readonly MethodInfo readNullableChar;
		private static readonly MethodInfo throwDataException;
		private static readonly MethodInfo enumParse;
		private static readonly MethodInfo getTypeFromHandle;
		private static readonly MethodInfo getValueByIndex;
		private static readonly MethodInfo getValueByKey;

		static SqlMapper()
		{
			createParameter = typeof(SqlMapper).GetMethod("CreateParameter", BindingFlags.NonPublic | BindingFlags.Static);
			createParameterList = typeof(SqlMapper).GetMethod("CreateParameterList", BindingFlags.NonPublic | BindingFlags.Static);
			readChar = typeof(SqlMapper).GetMethod("ReadChar", BindingFlags.NonPublic | BindingFlags.Static);
			readNullableChar = typeof(SqlMapper).GetMethod("ReadNullableChar", BindingFlags.NonPublic | BindingFlags.Static);
			throwDataException = typeof(SqlMapper).GetMethod("ThrowDataException", BindingFlags.NonPublic | BindingFlags.Static);
			enumParse = typeof(Enum).GetMethod("Parse", new Type[] { typeof(Type), typeof(string), typeof(bool) });
			getTypeFromHandle = typeof(Type).GetMethod("GetTypeFromHandle");
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
				identity = new QueryIdentity(connection.ConnectionString, query, typeof(MultiResult), parameters != null ? parameters.GetType() : null);

			QueryInfo info = GetQueryInfo(identity);
			if (info.ParameterGenerator == null && parameters != null)
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

			foreach (var property in GetParameterInfo(parameters)) {
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

		// deserializers
		internal static Func<IDataReader, T> GetDeserializer<T>(IDataRecord dataRecord)
		{
			var t = typeof(T);

			if (t == typeof(object) || t == typeof(DynamicRow))
				return GetDynamicDeserializer<T>(dataRecord);

			if (typeMap.ContainsKey(t.TypeHandle) == false && t.IsEnum == false)
				return GetClassDeserializer<T>(dataRecord);

			return GetStructDeserializer<T>();
		}
		private static Func<IDataReader, T> GetClassDeserializer<T>(IDataRecord dataRecord)
		{
			Type t = typeof(T);

			// select all members
			var members = GetMemberInfo(t);

			// find a member for all the column names in the result set
			var setters = dataRecord.GetColumnNames()
									.Select(columnName =>
										members.FirstOrDefault(x => string.Equals(x.Name, columnName, StringComparison.InvariantCulture)) ??								 // case sensitive
										members.FirstOrDefault(x => string.Equals(x.Name, columnName, StringComparison.InvariantCultureIgnoreCase)) ??						 // case insensitive
										members.FirstOrDefault(x => string.Equals(x.Name + "ID", columnName, StringComparison.InvariantCulture) && x.Type.IsEnum) ??		 // enum with ID postfix case sensitive
										members.FirstOrDefault(x => string.Equals(x.Name + "ID", columnName, StringComparison.InvariantCultureIgnoreCase) && x.Type.IsEnum)) // enum with ID postfix case insensitive
									.ToList();

			bool haveEnumLocal = false;
			int index = -1;
			DynamicMethod dm = new DynamicMethod(string.Format("Deserializer_{0}_{1}", t.Name, Guid.NewGuid()), t, new[] { typeof(IDataReader) }, true);
			ILGenerator il = dm.GetILGenerator();

			il.DeclareLocal(typeof(int));
			il.Emit(OpCodes.Ldc_I4, index);
			il.Emit(OpCodes.Stloc_0);

			il.BeginExceptionBlock();
			{
				// create instance of T
				il.DeclareLocal(t);
				if (t.IsValueType == false) {
					il.Emit(OpCodes.Newobj, t.GetConstructor(Type.EmptyTypes)); // [result]
					il.Emit(OpCodes.Stloc_1);
					il.Emit(OpCodes.Ldloc_1);
				}
				else {
					il.Emit(OpCodes.Ldloca_S, (byte)1);
					il.Emit(OpCodes.Initobj, t);
					il.Emit(OpCodes.Ldloca_S, (byte)1);
				}

				List<string> instantiatedParents = new List<string>();
				foreach (var setter in setters.Where(s => s != null && s.Parents.Count > 0)) {
					List<string> names = new List<string>();
					for (int i = 0; i < setter.Parents.Count; i++) {
						names.Add(setter.Parents[i].Name);
						if (instantiatedParents.Contains(string.Join(".", names)) == false) {
							il.Emit(OpCodes.Dup); // [result] [result]

							for (int j = 0; j < i; j++) {
								if (setter.Parents[j].MemberType == MemberTypes.Property)
									il.Emit(OpCodes.Callvirt, (setter.Parents[j] as PropertyInfo).GetGetMethod());
								else if (setter.Parents[j].MemberType == MemberTypes.Field)
									il.Emit(OpCodes.Ldfld, setter.Parents[j] as FieldInfo);
							}

							il.Emit(OpCodes.Newobj, setter.Parents[i].Type().GetConstructor(Type.EmptyTypes));

							if (setter.Parents[i].MemberType == MemberTypes.Property)
								il.Emit(OpCodes.Callvirt, (setter.Parents[i] as PropertyInfo).GetSetMethod());
							else if (setter.Parents[i].MemberType == MemberTypes.Field)
								il.Emit(OpCodes.Stfld, setter.Parents[i] as FieldInfo);

							instantiatedParents.Add(string.Join(".", names));
						}
					}
				}

				foreach (var setter in setters) {
					index += 1;

					// continue if there is no mapping
					if (setter == null)
						continue;

					Type memberType = setter.MemberInfo.Type();
					Type nullUnderlyingType = Nullable.GetUnderlyingType(memberType);
					Type unboxType = nullUnderlyingType != null && nullUnderlyingType.IsEnum ? nullUnderlyingType : memberType;

					// create labels to jump to
					var nullLabel = il.DefineLabel();
					var nextLabel = il.DefineLabel();

					il.Emit(OpCodes.Dup); // [result] [result]

					// load the member on the stack via its parents if any
					foreach (var parent in setter.Parents) {
						if (parent.MemberType == MemberTypes.Property)
							il.Emit(OpCodes.Callvirt, (parent as PropertyInfo).GetGetMethod()); // [result] [result or nested-property]
						else if (parent.MemberType == MemberTypes.Field)
							il.Emit(OpCodes.Ldfld, parent as FieldInfo); // [result] [result or nested-property]
					}

					il.Emit(OpCodes.Ldarg_0); // [result] [result or nested-property] [reader]
					il.EmitInt32(index); // [result] [result or nested-property] [reader] [index]
					il.Emit(OpCodes.Dup); // [result] [result or nested-property] [reader] [index] [index]
					il.Emit(OpCodes.Stloc_0); // [result] [result or nested-property] [reader] [index]
					il.Emit(OpCodes.Callvirt, getValueByIndex); // [result] [result or nested-property] [untyped value]

					// a char value is returned as a string and its not possible to implicitly cast it, so we have to convert it
					if (memberType == typeof(char) || memberType == typeof(char?)) {
						il.Emit(OpCodes.Call, memberType == typeof(char) ? readChar : readNullableChar); // [result] [result or nested-property] [untyped char]
					}
					else {
						il.Emit(OpCodes.Dup); // [result] [result or nested-property] [untyped value] [untyped value]
						il.Emit(OpCodes.Isinst, typeof(DBNull)); // [result] [result or nested-property] [untyped value] [DBNull or null]
						il.Emit(OpCodes.Brtrue_S, nullLabel); // [result] [result or nested-property] [untyped value], value is null

						if (unboxType.IsEnum) {
							if (!haveEnumLocal) {
								il.DeclareLocal(typeof(string));
								haveEnumLocal = true;
							}

							Label isNotString = il.DefineLabel();
							il.Emit(OpCodes.Dup); // [result] [result or nested-property] [untyped value] [untyped value]
							il.Emit(OpCodes.Isinst, typeof(string)); // [result] [result or nested-property] [untyped value] [untyped value or null]
							il.Emit(OpCodes.Dup); // [result] [result or nested-property] [untyped value] [untyped value or null] [untyped value or null]
							il.Emit(OpCodes.Stloc_2); // [result] [result or nested-property] [untyped value] [untyped value or null]
							il.Emit(OpCodes.Brfalse_S, isNotString); // [result] [result or nested-property] [untyped value]

							il.Emit(OpCodes.Pop); // [result] [result or nested-property]

							il.Emit(OpCodes.Ldtoken, unboxType); // [result] [result or nested-property] [enum-type-token]
							il.EmitCall(OpCodes.Call, getTypeFromHandle, null); // [result] [result or nested-property] [enum-type]
							il.Emit(OpCodes.Ldloc_2); // [result] [result or nested-property] [enum-type] [untyped value]
							il.Emit(OpCodes.Ldc_I4_1); // [result] [result or nested-property] [enum-type] [untyped value] [1]
							il.EmitCall(OpCodes.Call, enumParse, null); // [result] [result or nested-property] [untyped enum value]

							il.MarkLabel(isNotString);

							il.Emit(OpCodes.Unbox_Any, unboxType); // [result] [result or nested-property] [enum value]

							if (nullUnderlyingType != null)
								il.Emit(OpCodes.Newobj, memberType.GetConstructor(new[] { nullUnderlyingType })); // [result] [result or nested-property] [nullable enum value]
						}
						else
							il.Emit(OpCodes.Unbox_Any, unboxType); // [result] [result or nested-property] [typed value]
					}					

					// set value
					if (setter.MemberInfo.MemberType == MemberTypes.Property)
						il.Emit(t.IsValueType ? OpCodes.Call : OpCodes.Callvirt, (setter.MemberInfo as PropertyInfo).GetSetMethod()); // [result]
					else if (setter.MemberInfo.MemberType == MemberTypes.Field)
						il.Emit(OpCodes.Stfld, setter.MemberInfo as FieldInfo); // [result]

					// jump to the next setter
					il.Emit(OpCodes.Br, nextLabel);

					// value was null so clear the stack and jump to the next setter
					il.MarkLabel(nullLabel); // [result] [result or nested-property] [untyped value]
					il.Emit(OpCodes.Pop); // [result] [result or nested-property]
					il.Emit(OpCodes.Pop); // [result]

					// end of the "loop"
					il.MarkLabel(nextLabel); // [result]
				}

				// store the result
				if (t.IsValueType)
					il.Emit(OpCodes.Pop);
				else
					il.Emit(OpCodes.Stloc_1);
			}
			il.BeginCatchBlock(typeof(Exception)); // [exception]
			{
				il.Emit(OpCodes.Ldloc_0); // [exception] [index]
				il.Emit(OpCodes.Ldarg_0); // [exception] [index] [reader]
				il.Emit(OpCodes.Call, throwDataException); // stack is empty
				il.Emit(OpCodes.Ldnull); // [null]
				il.Emit(OpCodes.Stloc_1); // store value null over the result
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
			Type t = typeof(T);

			if (t == typeof(char) || t == typeof(char?)) {
				return r => {
					object value = r.GetValue(0);
					if (value == DBNull.Value)
						return default(T); // (T)(null as object);

					return (T)(value.ToString()[0] as object);
				};
			}

			if (t.IsEnum) {
				return r => {
					object value = r.GetValue(0);
					if (value.GetType() == typeof(string))
						return (T)Enum.Parse(t, value as string, true);

					return (T)value;
				};
			}

			return r => {
				object value = r.GetValue(0);
				if (value == DBNull.Value)
					value = null;

				try {
					// TODO: replace this temp fix, sqlce returns a decimal for @@identity
					if (r.GetFieldType(0) == typeof(decimal) && t != typeof(decimal) && t != typeof(decimal?))
						return (T)Convert.ChangeType(value, t);

					return (T)value;
				}
				catch (Exception ex) {
					throw new DataException(string.Format(@"Error casting ""{0}"" from [{1}] to [{2}]", value, r.GetFieldType(0).Name, t.Name), ex);
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
		private static char ReadChar(object value)
		{
			if (value == null || value is DBNull) throw new ArgumentNullException("value");
			string s = value as string;
			if (s == null || s.Length != 1) throw new ArgumentException("A single-character was expected", "value");
			return s[0];
		}
		private static char? ReadNullableChar(object value)
		{
			if (value == null || value is DBNull) return null;
			string s = value as string;
			if (s == null || s.Length != 1) throw new ArgumentException("A single-character was expected", "value");
			return s[0];
		}
		private static IEnumerable<Property> GetParameterInfo(object parameters)
		{
			if (parameters is Dictionary<string, object>) {
				var dictionary = parameters as Dictionary<string, object>;
				return dictionary.Select(d => new Property {
					Name = d.Key,
					Type = d.Value != null ? d.Value.GetType() : typeof(string)
				});
			}

			return parameters.GetType().GetProperties().Select(p => new Property {
				Name = p.Name,
				Type = p.PropertyType,
				Getter = p.GetGetMethod()
			});
		}
		private static IEnumerable<Member> GetMemberInfo(Type type, List<MemberInfo> parents = null)
		{
			List<MemberInfo> members = new List<MemberInfo>();
			members.AddRange(type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
			members.AddRange(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(f => f.Name.EndsWith("k__BackingField") == false));

			List<Member> result = new List<Member>();
			foreach (MemberInfo member in members) {
				Type memberType = member.Type();

				if (typeMap.ContainsKey(memberType.TypeHandle) == false && memberType.IsEnum == false) {
					var newParents = parents == null ? new List<MemberInfo>() : new List<MemberInfo>(parents);
					newParents.Add(member);

					result.AddRange(GetMemberInfo(memberType, newParents));
					continue;
				}

				result.Add(new Member {
					Type = memberType,
					MemberInfo = member,
					Parents = parents == null ? new List<MemberInfo>() : new List<MemberInfo>(parents)
				});
			}

			return result;
		}
	}
}