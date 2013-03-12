using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Linq;

namespace Lithium
{
	public static class Proxy
	{
		private static MethodAttributes attributes = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Virtual;
		private static ModuleBuilder moduleBuilder;

		static Proxy()
		{
			AssemblyName assemblyName = new AssemblyName("Lithium.Proxies");
			AssemblyBuilder assemblyBuilder = Thread.GetDomain().DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
			moduleBuilder = assemblyBuilder.DefineDynamicModule("Proxies");
		}

		public static T Create<T>()
		{
			var baseType = typeof(T);

			// create a new type builder
			TypeBuilder typeBuilder = moduleBuilder.DefineType(baseType.Name + "_Proxy", TypeAttributes.Public | TypeAttributes.Class);
			typeBuilder.SetParent(baseType);

			List<FieldBuilder> isDirtyFields = new List<FieldBuilder>();

			// loop over the properties of T
			foreach (var property in baseType.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
				// private fields
				FieldBuilder field = typeBuilder.DefineField("_" + property.Name, property.PropertyType, FieldAttributes.Private);
				FieldBuilder fieldIsDirty = typeBuilder.DefineField("_" + property.Name + "_IsDirty", typeof(bool), FieldAttributes.Private);
				isDirtyFields.Add(fieldIsDirty);

				// get
				MethodBuilder getMethodBuilder = typeBuilder.DefineMethod("get_" + property.Name, attributes, property.PropertyType, Type.EmptyTypes);
				ILGenerator getIlGenerator = getMethodBuilder.GetILGenerator();
				getIlGenerator.Emit(OpCodes.Ldarg_0);
				getIlGenerator.Emit(OpCodes.Ldfld, field);
				getIlGenerator.Emit(OpCodes.Ret);

				// set
				MethodBuilder setMethodBuilder = typeBuilder.DefineMethod("set_" + property.Name, attributes, null, new Type[] { property.PropertyType });
				ILGenerator setIlGenerator = setMethodBuilder.GetILGenerator();
				// set isDirty
				setIlGenerator.Emit(OpCodes.Ldarg_0);
				setIlGenerator.Emit(OpCodes.Ldc_I4_1);
				setIlGenerator.Emit(OpCodes.Stfld, fieldIsDirty);

				setIlGenerator.Emit(OpCodes.Ldarg_0);
				setIlGenerator.Emit(OpCodes.Ldarg_1);
				setIlGenerator.Emit(OpCodes.Stfld, field);
				setIlGenerator.Emit(OpCodes.Ret);
			}

			// IsDirty Method
			MethodBuilder isDirtyMethodBuilder = typeBuilder.DefineMethod("GetIsDirty", attributes, typeof(bool), Type.EmptyTypes);
			ILGenerator isDirtyIlGenerator = isDirtyMethodBuilder.GetILGenerator();
			Label foundIsDirty = isDirtyIlGenerator.DefineLabel();
			
			foreach (var isDirtyField in isDirtyFields) {
				isDirtyIlGenerator.Emit(OpCodes.Ldarg_0);
				isDirtyIlGenerator.Emit(OpCodes.Ldfld, isDirtyField);
				isDirtyIlGenerator.Emit(OpCodes.Dup);
				isDirtyIlGenerator.Emit(OpCodes.Brtrue_S, foundIsDirty);

				isDirtyIlGenerator.Emit(OpCodes.Pop);
			}

			isDirtyIlGenerator.Emit(OpCodes.Ldc_I4_0); // load false as result if all are clean

			isDirtyIlGenerator.MarkLabel(foundIsDirty);
			isDirtyIlGenerator.Emit(OpCodes.Ret);


			// create the type, an instance of the type and return it
			Type generatedType = typeBuilder.CreateType();
			object generatedObject = Activator.CreateInstance(generatedType);

			return (T)generatedObject;
		}
	}
}