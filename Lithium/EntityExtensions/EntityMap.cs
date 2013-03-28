using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace Lithium.EntityExtensions
{
	public class EntityMap<T>
	{
		internal string TableName { get; private set; }
		internal string IdentityName { get; private set; }
		internal Type IdentityType { get; private set; }
		internal bool AutoIncrement { get; private set; }

		internal Func<object, object> ParameterRemover { get; set; }
		internal Func<object, object> IdentityGetter { get; private set; }
		internal Action<object, object> IdentitySetter { get; private set; }

		private const string selectQuery = "select {0} from {1}";
		private const string selectWhereQuery = "select {0} from {1} where {2}";
		private const string selectWhereIdQuery = "select {0} from {1} where {2} = @id";
		private const string insertQuery = "insert into {0} ({1}) values ({2})";
		private const string updateQuery = "update {0} set {1} where {2}";
		private const string deleteQuery = "delete from {0} where {1} = @id";

		private static readonly MethodInfo addItem = typeof(IDictionary).GetMethod("Add", new[] { typeof(object), typeof(object) });

		private Type type;
		private List<string> ignores;
		private List<PropertyInfo> properties;
		private ConcurrentDictionary<int, string> selectWhereQueries { get; set; }

		public EntityMap()
		{
			type = typeof(T);

			selectWhereQueries = new ConcurrentDictionary<int, string>();
			ignores = new List<string>();

			properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
							 .Where(x => SqlMapper.SupportedTypes.ContainsKey(x.PropertyType))
							 .ToList();
		}

		// internal interface
		internal string GetSelectWhereQuery(Expression<Func<T, bool>> predicate)
		{
			int hash = predicate.ToString().GetHashCode();

			if (selectWhereQueries.ContainsKey(hash) == false)
				selectWhereQueries[hash] = GenerateSelectWhereQuery(predicate);

			return selectWhereQueries[hash];
		}
		internal void AddSelectWhereQuery(int hash, string query)
		{
			if (selectWhereQueries.ContainsKey(hash))
				return;

			selectWhereQueries[hash] = query;
		}

		// public interface
		public EntityMap<T> Table(string name)
		{
			TableName = name;
			return this;
		}
		public EntityMap<T> Identity(Expression<Func<T, object>> expression, bool autoIncrement = true)
		{
			MemberInfo member = GetMemberInfo(expression);

			IdentityName = member.Name;
			IdentityType = GetMemberUnderlyingType(member);
			AutoIncrement = autoIncrement;

			CreateIdentitySetter();
			CreateIdentityGetter();
			CreateParameterRemover();

			return this;
		}
		public EntityMap<T> Ignore(Expression<Func<T, object>> expression)
		{
			MemberInfo member = GetMemberInfo(expression);

			ignores.Add(member.Name);

			return this;
		}

		public IEnumerable<string> Columns
		{
			get
			{
				return properties.Select(x => x.Name)
								 .Except(ignores);
			}
		}

		public string SelectQuery
		{
			get
			{
				return string.Format(selectQuery, string.Join(",", Columns), TableName);
			}
		}
		public string SelectWhereIdQuery
		{
			get
			{
				return string.Format(selectWhereIdQuery, string.Join(",", Columns), TableName, IdentityName);
			}
		}
		public string InsertQuery
		{
			get
			{
				return string.Format(insertQuery,
									 TableName,
									 string.Join(",", Columns.Where(p => AutoIncrement == false || p != IdentityName)),
									 string.Join(",", Columns.Where(p => AutoIncrement == false || p != IdentityName).Select(l => "@" + l)));
			}
		}
		public string UpdateQuery
		{
			get
			{
				return string.Format(updateQuery,
									 TableName,
									 string.Join(",", Columns.Where(p => p != IdentityName).Select(l => string.Format(@"{0}=@{0}", l))),
									 string.Format(@"{0}=@{0}", IdentityName));
			}
		}
		public string DeleteQuery
		{
			get
			{
				return string.Format(deleteQuery, TableName, IdentityName);
			}
		}

		public string GenerateSelectWhereQuery(Expression<Func<T, bool>> predicate)
		{
			BinaryExpression expression = (BinaryExpression)predicate.Body;
			MemberExpression member = (MemberExpression)expression.Left;

			return string.Format(selectWhereQuery, string.Join(",", Columns), TableName, string.Format(@"{0}=@{0}", member.Member.Name));
		}

		// helpers
		private static MemberInfo GetMemberInfo<T>(Expression<Func<T, object>> expression)
		{
			MemberInfo result;

			if (expression.Body is MemberExpression) {
				// reference type
				result = ((MemberExpression)expression.Body).Member;
			}
			else if (expression.Body is UnaryExpression) {
				// value type
				var unaryExpression = (UnaryExpression)expression.Body;
				result = ((MemberExpression)unaryExpression.Operand).Member;
			}
			else {
				throw new ArgumentException("Invalid expression");
			}

			return result;
		}
		private static Type GetMemberUnderlyingType(MemberInfo member)
		{
			switch (member.MemberType) {
				case MemberTypes.Field:
					return ((FieldInfo)member).FieldType;
				case MemberTypes.Property:
					return ((PropertyInfo)member).PropertyType;
				default:
					throw new ArgumentException("MemberInfo must be of type FieldInfo or PropertyInfo", "member");
			}
		}
		private void CreateIdentitySetter()
		{
			var dm = new DynamicMethod("IdentitySetter_" + Guid.NewGuid(), null, new[] { typeof(object), typeof(object) }, type, true);
			var il = dm.GetILGenerator();

			var propertyInfo = type.GetProperty(IdentityName);
			var fieldInfo = type.GetField(IdentityName);

			il.Emit(OpCodes.Ldarg_0); // [untyped entity]
			il.Emit(OpCodes.Unbox_Any, type); // [entity]
			il.Emit(OpCodes.Ldarg_1); // [entity] [untyped value]

			if (propertyInfo != null) {
				il.Emit(OpCodes.Call, typeof(Convert).GetMethod("To" + propertyInfo.PropertyType.Name, new[] { typeof(object) })); // [entity] [typed value]
				il.Emit(OpCodes.Callvirt, propertyInfo.GetSetMethod()); // stack is empty
			}
			else {
				il.Emit(OpCodes.Call, typeof(Convert).GetMethod("To" + fieldInfo.FieldType.Name, new[] { typeof(object) })); // [entity] [typed value]
				il.Emit(OpCodes.Stfld, fieldInfo); // stack is empty
			}

			il.Emit(OpCodes.Ret);
			IdentitySetter = dm.CreateDelegate(typeof(Action<object, object>)) as Action<object, object>;
		}
		private void CreateIdentityGetter()
		{
			var dm = new DynamicMethod("IdentityGetter_" + Guid.NewGuid(), typeof(object), new[] { typeof(object) }, type, true);
			var il = dm.GetILGenerator();

			var propertyInfo = type.GetProperty(IdentityName);
			var fieldInfo = type.GetField(IdentityName);

			il.Emit(OpCodes.Ldarg_0); // [untyped entity]
			il.Emit(OpCodes.Unbox_Any, type); // [entity]

			if (propertyInfo != null) {
				il.Emit(OpCodes.Callvirt, propertyInfo.GetGetMethod()); // [typed value]
				il.Emit(OpCodes.Box, propertyInfo.PropertyType); // [boxed value]
			}
			else {
				il.Emit(OpCodes.Ldfld, fieldInfo); // [typed value]
				il.Emit(OpCodes.Box, fieldInfo.FieldType); // [boxed value]
			}

			il.Emit(OpCodes.Ret);
			IdentityGetter = dm.CreateDelegate(typeof(Func<object, object>)) as Func<object, object>;
		}
		private void CreateParameterRemover()
		{
			var returnType = typeof(Dictionary<string, object>);
			var dm = new DynamicMethod("ParameterRemover_" + Guid.NewGuid(), typeof(object), new[] { typeof(object) }, returnType, true);
			var il = dm.GetILGenerator();

			il.DeclareLocal(type);
			il.Emit(OpCodes.Ldarg_0); // [untyped parameters]
			il.Emit(OpCodes.Unbox_Any, type); // [typed parameters]
			il.Emit(OpCodes.Stloc_0); // stack is leeg

			il.DeclareLocal(returnType);
			il.Emit(OpCodes.Newobj, returnType.GetConstructor(Type.EmptyTypes)); // [dictionary]

			foreach (var property in properties.Where(p => p.Name != IdentityName)) {
				il.Emit(OpCodes.Dup); // [dictionary] [dictionary]
				il.Emit(OpCodes.Ldstr, property.Name); // [dictionary] [dictionary] [name]
				il.Emit(OpCodes.Ldloc_0); // [dictionary] [dictionary] [name] [parameters]
				il.Emit(OpCodes.Callvirt, property.GetGetMethod()); // [dictionary] [dictionary] [name] [typed value]
				il.Emit(OpCodes.Box, property.PropertyType); // [dictionary] [dictionary] [name] [boxed value]
				il.Emit(OpCodes.Callvirt, addItem); // [dictionary]
			}

			il.Emit(OpCodes.Ret);
			ParameterRemover = dm.CreateDelegate(typeof(Func<object, object>)) as Func<object, object>;
		}
	}
}