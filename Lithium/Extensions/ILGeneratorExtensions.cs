using System.Reflection.Emit;
using System;
using System.Reflection;
using System.Linq;

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

		/// <summary>
		/// Initializes all properties on the instance of parameter Type
		/// </summary>
		/// <param name="type">Type (requires an empty constructor on the type and all the class type properties)</param>
		public static void InitializeProperties(this ILGenerator il, Type type)
		{
			var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
								 .Where(p => p.PropertyType.IsClass && p.PropertyType != typeof(string));

			foreach (var property in properties) {
				il.Emit(OpCodes.Dup); // [object] [object]
				il.Emit(OpCodes.Newobj, property.PropertyType.GetConstructor(Type.EmptyTypes)); // [object] [object] [new object]
				il.InitializeProperties(property.PropertyType); // [object] [object] [new object]				
				il.Emit(OpCodes.Callvirt, property.GetSetMethod()); // [object] 
			}
		}
	}
}