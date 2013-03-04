using System;
using System.Collections.Concurrent;
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

		private ConcurrentDictionary<int, string> SelectWhereQueries { get; set; }

		internal string SelectQuery { get; set; }
		internal string SelectWhereIdQuery { get; set; }
		internal string InsertQuery { get; set; }
		internal string UpdateQuery { get; set; }
		internal string DeleteQuery { get; set; }

		internal Func<object, object> ParameterRemover { get; set; }
		internal Func<object, object> IdentityGetter { get; private set; }
		internal Action<object, object> IdentitySetter { get; private set; }

		public EntityMap()
		{
			SelectWhereQueries = new ConcurrentDictionary<int, string>();
		}

		// interface
		public EntityMap<T> Table(string name)
		{
			TableName = name;
			return this;
		}
		public EntityMap<T> Identity(Expression<Func<T, object>> expression, bool autoIncrement = true)
		{
			if (expression == null) {
				throw new ArgumentNullException("expression");
			}

			MemberInfo member;
			if (expression.Body is MemberExpression) {
				// reference type
				member = ((MemberExpression)expression.Body).Member;
			}
			else if (expression.Body is UnaryExpression) {
				// value type
				var unaryExpression = (UnaryExpression)expression.Body;
				member = ((MemberExpression)unaryExpression.Operand).Member;
			}
			else {
				throw new ArgumentException("Invalid expression");
			}

			IdentityName = member.Name;
			IdentityType = GetMemberUnderlyingType(member);
			AutoIncrement = autoIncrement;

			CreateIdentitySetter();
			CreateIdentityGetter();

			return this;
		}

		// internal interface
		internal string GetSelectWhereQuery(int hash)
		{
			return SelectWhereQueries.ContainsKey(hash)
				? SelectWhereQueries[hash]
				: null;
		}
		internal void AddSelectWhereQuery(int hash, string query)
		{
			if (SelectWhereQueries.ContainsKey(hash))
				return;

			SelectWhereQueries[hash] = query;
		}

		// helpers
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
			var t = typeof(T);
			var dm = new DynamicMethod("IdentitySetter_" + Guid.NewGuid(), null, new[] { typeof(object), typeof(object) }, t, true);
			var il = dm.GetILGenerator();

			var propertyInfo = t.GetProperty(IdentityName);
			var fieldInfo = t.GetField(IdentityName);

			il.Emit(OpCodes.Ldarg_0); // [untyped entity]
			il.Emit(OpCodes.Unbox_Any, t); // [entity]
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
			var t = typeof(T);
			var dm = new DynamicMethod("IdentityGetter_" + Guid.NewGuid(), typeof(object), new[] { typeof(object) }, t, true);
			var il = dm.GetILGenerator();

			var propertyInfo = t.GetProperty(IdentityName);
			var fieldInfo = t.GetField(IdentityName);

			il.Emit(OpCodes.Ldarg_0); // [untyped entity]
			il.Emit(OpCodes.Unbox_Any, t); // [entity]

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
	}
}