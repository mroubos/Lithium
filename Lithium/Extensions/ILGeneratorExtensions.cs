using System.Reflection.Emit;
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

namespace Lithium.Extensions
{
	public static class ILGeneratorExtensions
	{
		public static void EmitInt32(this ILGenerator il, int value)
		{
			switch (value) {
				case -1: il.Emit(OpCodes.Ldc_I4_M1); break;
				case 0: il.Emit(OpCodes.Ldc_I4_0); break;
				case 1: il.Emit(OpCodes.Ldc_I4_1); break;
				case 2: il.Emit(OpCodes.Ldc_I4_2); break;
				case 3: il.Emit(OpCodes.Ldc_I4_3); break;
				case 4: il.Emit(OpCodes.Ldc_I4_4); break;
				case 5: il.Emit(OpCodes.Ldc_I4_5); break;
				case 6: il.Emit(OpCodes.Ldc_I4_6); break;
				case 7: il.Emit(OpCodes.Ldc_I4_7); break;
				case 8: il.Emit(OpCodes.Ldc_I4_8); break;
				default:
					if (value >= -128 && value <= 127)
						il.Emit(OpCodes.Ldc_I4_S, (sbyte)value);
					else
						il.Emit(OpCodes.Ldc_I4, value);
					break;
			}
		}

		//public static void EmitReadValue(this ILGenerator il, MemberInfo memberInfo)
		//{
		//	if (memberInfo is PropertyInfo)
		//		il.Emit(memberInfo.Type().IsValueType ? OpCodes.Call : OpCodes.Callvirt, (memberInfo as PropertyInfo).GetGetMethod());
		//	else if (memberInfo is FieldInfo)
		//		il.Emit(OpCodes.Ldfld, memberInfo as FieldInfo);
		//}

		//public static void EmitWriteValue(this ILGenerator il, MemberInfo memberInfo)
		//{
		//	if (memberInfo is PropertyInfo)
		//		il.Emit(memberInfo.Type().IsValueType ? OpCodes.Call : OpCodes.Callvirt, (memberInfo as PropertyInfo).GetSetMethod());
		//	else if (memberInfo is FieldInfo)
		//		il.Emit(OpCodes.Stfld, memberInfo as FieldInfo);
		//}
	}
}