using System.Collections.Generic;
using System.Data;
using System.Data.SqlServerCe;
using System.Linq;
using Lithium.Extensions;
using Lithium.Tests.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithium.Tests
{
	[TestClass]
	public class SqlMapperTests
	{
		protected static IDbConnection Connection { get; private set; }

		[ClassInitialize]
		public static void SetUp(TestContext context)
		{
			Connection = new SqlCeConnection(@"Data Source=Database\Tests.sdf");
			// Connection = new SqlConnection(@"");

			Connection.Open();
		}

		[ClassCleanup]
		public static void TearDown()
		{
			Connection.Dispose();
		}

		[TestMethod]
		public void SubProperties()
		{
			var result = Connection.Query<Branch>("select 'Cafe' as Name, 'Horeca' as 'Category.Name', 'Asdf' as 'Category.Info.Name', 'Asdf2' as 'Category.Info2.Name'").First();

			Assert.IsNotNull(result.Category);
			Assert.IsNotNull(result.Category.Info);
			Assert.IsNotNull(result.Category.Info2);

			Assert.AreEqual("Cafe", result.Name);
			Assert.AreEqual("Horeca", result.Category.Name);
			Assert.AreEqual("Asdf", result.Category.Info.Name);
			Assert.AreEqual("Asdf2", result.Category.Info2.Name);
		}

		public class Branch
		{
			public string Name { get; set; }
			public Category Category { get; set; }
		}

		public class Category
		{
			public string Name { get; set; }
			public Info Info { get; set; }
			public Info Info2 { get; set; }
		}

		public class Info
		{
			public string Name { get; set; }
		}

		[TestMethod]
		public void DictionaryParameters()
		{
			const int id = 1;
			var parameters = new Dictionary<string, object> {
				{ "id", id }
			};

			var result = Connection.Query<int>("select @id", parameters).First();

			Assert.AreEqual(id, result);
		}

		[TestMethod]
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

		[TestMethod]
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

		[TestMethod]
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

		[TestMethod]
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