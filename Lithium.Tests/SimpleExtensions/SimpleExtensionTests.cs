using System.Data;
using System.Data.SqlServerCe;
using System.Linq;
using Lithium.SimpleExtensions;
using Lithium.Tests.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithium.Tests.SimpleExtensions
{
	[TestClass]
	public class SimpleExtensions
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
		public void FullCycle()
		{
			Member member;

			const string initialName = "Fabian";
			const string updatedName = "Jurian";

			// record inserten
			int id = Connection.Insert<int>("Member", new { Name = initialName });

			// record ophalen en controleren of waarde niet null is en de naam overeenkomt
			member = Connection.Query<Member>("select * from Member where ID = @id", new { ID = id }).FirstOrDefault();
			Assert.IsNotNull(member);
			Assert.AreEqual(initialName, member.Name);

			// record updaten op basis van het id
			Connection.Update("Member", new { Name = updatedName }, new { ID = id });

			// record weer ophalen en controleren of waarde niet null is en de naam overeenkomt met de nieuwe naam
			member = Connection.Query<Member>("select * from Member where ID = @id", new { ID = id }).FirstOrDefault();
			Assert.IsNotNull(member);
			Assert.AreEqual(updatedName, member.Name);

			// record verwijderen op basis van het id
			Connection.Delete("Member", new { ID = id });

			// record proberen op te halen en bevestigen dat het record niet gevonden kan worden
			member = Connection.Query<Member>("select * from Member where ID = @id", new { ID = id }).FirstOrDefault();
			Assert.IsNull(member);
		}
	}
}