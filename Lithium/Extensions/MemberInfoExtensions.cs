using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Lithium.Extensions
{
	public static class MemberInfoExtensions
	{
		public static Type Type(this MemberInfo memberInfo)
		{
			if (memberInfo.MemberType == MemberTypes.Property)
				return (memberInfo as PropertyInfo).PropertyType;
			else if (memberInfo.MemberType == MemberTypes.Field)
				return (memberInfo as FieldInfo).FieldType;
			else
				throw new ArgumentException("MemberInfo not of type Property of Field", "memberInfo");
		}
	}
}