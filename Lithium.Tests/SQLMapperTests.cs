using System.Collections.Generic;
using System.Linq;
using Lithium.Extensions;
using NUnit.Framework;

namespace Lithium.Tests
{
	[TestFixture]
	public class SQLMapperTests : TestBase
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
			if (Connection.GetConnectionType() != ConnectionType.SQLCE) {
				string c = string.Join("", Enumerable.Repeat("c", 8000).ToArray());
				string d = string.Join("", Enumerable.Repeat("d", 12000).ToArray());
				result = Connection.Query<dynamic>("select @c c, @d d", new { c, d }).First();
				Assert.AreEqual(c, result.c);
				Assert.AreEqual(d, result.d);
			}
		}
	}
}