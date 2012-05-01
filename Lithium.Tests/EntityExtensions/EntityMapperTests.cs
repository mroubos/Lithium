using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using Lithium.EntityExtensions;
using Lithium.Tests.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Data.SqlServerCe;

namespace Lithium.Tests.EntityExtensions
{
	[TestClass]
	public class EntityMapperTests
	{
		protected static IDbConnection Connection { get; private set; }

		[ClassInitialize]
		public static void Initialize(TestContext context)
		{
			Connection = new SqlCeConnection(@"Data Source=Tests.sdf");
			//Connection = new SqlConnection(ConfigurationManager.ConnectionStrings["Sql"].ConnectionString);

			// map person entity
			Connection.Entity<Member>()
				.Table("Member")
				.Identity(p => p.ID);

			Connection.Open();
		}

		[ClassCleanup]
		public static void Cleanup()
		{
			Connection.Dispose();
		}

		[TestMethod]
		[DeploymentItem("Database/Tests.sdf")]
		public void FullCycle()
		{
			const string newName = "Jurian";

			var person = new Member {
				Name = "Fabian"
			};

			// insert new member
			Connection.Insert(person);
			Assert.IsTrue(person.ID > 0);

			// assert insert
			Member inserted = Connection.Select<Member>(person.ID);
			Assert.AreEqual(person.ID, inserted.ID);
			Assert.AreEqual(person.Name, inserted.Name);

			// update member
			inserted.Name = newName;
			Connection.Update(inserted);

			// assert update
			Member updated = Connection.Select<Member>(inserted.ID);
			Assert.AreEqual(inserted.ID, updated.ID);
			Assert.AreEqual(inserted.Name, updated.Name);

			// delete member
			Connection.Delete(updated);

			// assert delete
			Member deleted = Connection.Select<Member>(person.ID);
			Assert.IsNull(deleted);
		}
	}
}