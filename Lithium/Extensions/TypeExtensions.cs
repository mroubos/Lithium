using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lithium.Extensions
{
	public static class TypeExtensions
	{
		public static bool IsNullableEnum(this Type t)
		{
			Type u = Nullable.GetUnderlyingType(t);
			return u != null && u.IsEnum;
		}
	}
}
