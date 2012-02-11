using System.Collections.Generic;
using System.Linq;
using Lithium.Extensions;
using Lithium.Tests.Models;
using NUnit.Framework;

namespace Lithium.Tests
{
	[TestFixture]
	public class SqlMapperTests : TestBase
	{
		[Test]
		public void DictionaryParameters()
		{
			const int id = 1;
			var parameters = new Dictionary<string, object> {
				{ "id", id }
			};

			var result = Connection.Query<int>("select @id", parameters).First();

			Assert.AreEqual(id, result);
		}

		[Test]
		public void LongString()
		{
			dynamic result;

			string a = string.Join("", Enumerable.Repeat("a", 3999).ToArray());
			string b = string.Join("", Enumerable.Repeat("b", 4000).ToArray());
			result = Connection.Query<dynamic>("select @a a, @b b", new { a, b }).First();
			Assert.AreEqual(a, result.a);
			Assert.AreEqual(b, result.b);

			// SQLCE doesn't support strings over 4000 characters
			if (Connection.GetConnectionType() != ConnectionType.SqlCe) {
				string c = string.Join("", Enumerable.Repeat("c", 8000).ToArray());
				string d = string.Join("", Enumerable.Repeat("d", 12000).ToArray());
				result = Connection.Query<dynamic>("select @c c, @d d", new { c, d }).First();
				Assert.AreEqual(c, result.c);
				Assert.AreEqual(d, result.d);
			}
		}

		[Test]
		public void Enums()
		{
			var input = new Member {
				ID = 1,
				Name = "Fabian",
				MemberType = MemberType.Administrator
			};

			var result = Connection.Query<Member>("select @id ID, @name Name, @memberType MemberType", input).Single();

			Assert.AreEqual(input.ID, result.ID);
			Assert.AreEqual(input.Name, result.Name);
			Assert.AreEqual(input.MemberType, result.MemberType);
		}

		[Test]
		public void EnumsCasted()
		{
			var input = new {
				ID = 1,
				Name = "Fabian",
				MemberType = 2
			};

			var result = Connection.Query<Member>("select @id ID, @name Name, @memberType MemberType", input).Single();

			Assert.AreEqual(input.ID, result.ID);
			Assert.AreEqual(input.Name, result.Name);
			Assert.AreEqual((MemberType)input.MemberType, result.MemberType);
		}

		[Test]
		public void EnumsMissing()
		{
			var result = Connection.Query<Member>("select @id ID, @name Name, @memberType MemberType", new {
				id = 1, 
				name ="Fabian", 
				MemberType = 4
			}).Single();

			Assert.AreEqual(1, result.ID);
			Assert.AreEqual("Fabian", result.Name);
			Assert.AreEqual(4, (int)result.MemberType);
		}
	}
}