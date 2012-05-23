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
			Connection = new SqlCeConnection(@"Data Source=Tests.sdf");
			//Connection = new SqlConnection(ConfigurationManager.ConnectionStrings["Sql"].ConnectionString);

			Connection.Open();
		}

		[ClassCleanup]
		public static void TearDown()
		{
			Connection.Dispose();
		}

		[TestMethod]
		public void NestedProperties()
		{
			// SQLCE doesn't support periods in column names
			if (Connection.GetConnectionType() == ConnectionType.SqlCe)
				return;

			Person person = Connection.Query<Person>("select 'Fabian' as Name, 'fabian@mail.com' as 'ContactInfo1.Email', 'Gouda' as 'ContactInfo1.City.Name', 'mail@fabian.com' as 'ContactInfo2.Email'").First();

			Assert.IsNotNull(person);
			Assert.IsNotNull(person.ContactInfo1);
			Assert.IsNotNull(person.ContactInfo2);

			Assert.AreEqual("Fabian", person.Name);
			Assert.AreEqual("fabian@mail.com", person.ContactInfo1.Email);
			Assert.AreEqual("mail@fabian.com", person.ContactInfo2.Email);
			Assert.AreEqual("Gouda", person.ContactInfo1.City.Name);
		}

		[TestMethod]
		public void CircularReferencingTypes()
		{
			CircularPerson circularPerson = Connection.Query<CircularPerson>("select 'Fabian' as Name").First();
			Assert.AreEqual("Fabian", circularPerson.Name);
		}

		[TestMethod]
		public void PrivateProperties()
		{
			Person person = Connection.Query<Person>("select 'Fabian' as Name, 'fbdegroot' as Username").First();
			Assert.AreEqual("Fabian", person.Name);
		}

		[TestMethod]
		public void DoesntInstantiatePropertiesWhichAreNotInTheResultSet()
		{
			Person person = Connection.Query<Person>("select 'Fabian' as Name").First();

			Assert.IsNotNull(person);
			Assert.IsNull(person.ContactInfo1);
			Assert.IsNull(person.ContactInfo2);
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
			var resultA = Connection.Query<SomeEnum>("select @a", new { a = SomeEnum.Two }).Single();
			Assert.AreEqual(SomeEnum.Two, resultA);

			var resultB = Connection.Query<SomeEnum>("select 2").Single();
			Assert.AreEqual(SomeEnum.Two, resultB);

			var resultC = Connection.Query<SomeEnum>("select 'Two'").Single();
			Assert.AreEqual(SomeEnum.Two, resultC);

			var resultD = Connection.Query<SomeEnum>("select 'tWo'").Single();
			Assert.AreEqual(SomeEnum.Two, resultD);

			var resultE = Connection.Query<SomeEnum?>("select null").Single();
			Assert.AreEqual(null, resultE);

			var resultF = Connection.Query<EnumTest>("select @a SomeEnum", new { a = SomeEnum.Two }).Single();
			Assert.AreEqual(SomeEnum.Two, resultF.SomeEnum);

			var resultG = Connection.Query<EnumTest>("select 2 SomeEnum").Single();
			Assert.AreEqual(SomeEnum.Two, resultG.SomeEnum);

			var resultH = Connection.Query<EnumTest>("select 'Two' SomeEnum").Single();
			Assert.AreEqual(SomeEnum.Two, resultH.SomeEnum);

			var resultI = Connection.Query<EnumTest>("select 'tWo' SomeEnum").Single();
			Assert.AreEqual(SomeEnum.Two, resultI.SomeEnum);

			var resultJ = Connection.Query<EnumTest>("select null SomeEnumNullable").Single();
			Assert.AreEqual(null, resultJ.SomeEnumNullable);

			var resultK = Connection.Query<EnumTest>("select 2 SomeEnumID").Single();
			Assert.AreEqual(SomeEnum.Two, resultK.SomeEnum);

			var resultL = Connection.Query<EnumTest>("select 'Two' SomeEnumID").Single();
			Assert.AreEqual(SomeEnum.Two, resultL.SomeEnum);

			var resultM = Connection.Query<EnumTest>("select 'tWo' SomeEnumID").Single();
			Assert.AreEqual(SomeEnum.Two, resultM.SomeEnum);

			var resultN = Connection.Query<EnumTest>("select 4 SomeEnum").Single();
			Assert.AreEqual((SomeEnum)4, resultN.SomeEnum);
		}

		[TestMethod]
		public void Structs()
		{
			var member = Connection.Query<MemberStruct>("select 1 ID, 'Fabian' Name").First();

			Assert.AreEqual(1, member.ID);
			Assert.AreEqual("Fabian", member.Name);
		}
	}
}