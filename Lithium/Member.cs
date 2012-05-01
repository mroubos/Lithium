using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Lithium
{
	internal class Member
	{
		public Type Type { get; set; }
		public MemberInfo MemberInfo { get; set; }
		public List<MemberInfo> Parents { get; set; }

		private string name;
		public string Name
		{
			get
			{
				if (name == null) {
					List<string> names = Parents.Select(p => p.Name).ToList();
					names.Add(MemberInfo.Name);
					name = string.Join(".", names);
				}

				return name;
			}
		}
	}
}