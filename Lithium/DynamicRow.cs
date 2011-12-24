using System.Collections.Generic;
using System.Dynamic;

namespace Lithium
{
	internal class DynamicRow : DynamicObject
	{
		private readonly IDictionary<string, object> data;

		public DynamicRow(IDictionary<string, object> data)
		{
			this.data = data;
		}

		public override bool TrySetMember(SetMemberBinder binder, object value)
		{
			data[binder.Name] = value;
			return true;
		}

		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			return data.TryGetValue(binder.Name, out result);
		}
	}
}