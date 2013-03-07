using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Lithium.Linq
{
	public class Query<T> : IQueryable<T>, IOrderedQueryable<T>
	{
		private readonly IDbConnection connection;
		private readonly Expression expression;
		private readonly IQueryProvider provider;

		private IEnumerable<T> result;
		private static ConcurrentDictionary<Type, List<string>> propertyLists = new ConcurrentDictionary<Type, List<string>>();

		private List<string> properties;
		public List<string> Properties
		{
			get
			{
				if (properties == null) {
					Type type = typeof(T);

					if (propertyLists.ContainsKey(type) == false) {
						properties = new List<string>();

						foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
							properties.Add(property.Name);

						propertyLists[type] = properties;
					}
					else {
						properties = propertyLists[type];
					}
				}

				return properties;
			}
		}

		internal string TableName { get; private set; }
		internal string IdentityName { get; private set; }
		internal Type IdentityType { get; private set; }
		internal bool AutoIncrement { get; private set; }

		internal Func<object, object> ParameterRemover { get; set; }
		internal Func<object, object> IdentityGetter { get; private set; }
		internal Action<object, object> IdentitySetter { get; private set; }

		public Query(IDbConnection connection, Expression expression = null)
		{
			this.connection = connection;
			this.provider = new QueryProvider(connection);
			this.expression = expression ?? Expression.Constant(this);
		}

		private IEnumerable<T> GetResult()
		{
			if (result == null) {
				var builder = new QueryBuilder<T>(expression);
				result = connection.Query<T>(builder.Query, builder.Parameters);
			}

			return result;
		}

		public Query<T> Table(string name)
		{
			TableName = name;
			return this;
		}
		public Query<T> Identity(Expression<Func<T, object>> expression, bool autoIncrement = true)
		{
			if (expression == null)
				throw new ArgumentNullException("expression");

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

		#region IQueryable<T>
		public IEnumerator<T> GetEnumerator()
		{
			return GetResult().GetEnumerator();
		}
		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetResult().GetEnumerator();
		}
		Type IQueryable.ElementType { get { return typeof(T); } }
		public Expression Expression { get { return expression; } }
		public IQueryProvider Provider { get { return provider; } }
		#endregion
	}
}