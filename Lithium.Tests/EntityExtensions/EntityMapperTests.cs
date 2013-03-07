using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using Lithium.EntityExtensions;
using Lithium.Tests.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Data.SqlServerCe;
using System.Linq;

namespace Lithium.Tests.EntityExtensions
{
	[TestClass]
	public class EntityMapperTests
	{
		protected static IDbConnection Connection { get; private set; }

		[ClassInitialize]
		public static void Initialize(TestContext context)
		{
			//Connection = new SqlCeConnection(@"Data Source=Tests.sdf");
			Connection = new SqlConnection(ConfigurationManager.ConnectionStrings["Sql"].ConnectionString);

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

			var member = new Member {
				Name = "Fabian"
			};

			// insert new member
			Connection.Insert(member);
			Assert.IsTrue(member.ID > 0);

			// assert insert
			Member inserted = Connection.Select<Member>(member.ID);
			Assert.AreEqual(member.ID, inserted.ID);
			Assert.AreEqual(member.Name, inserted.Name);

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
			Member deleted = Connection.Select<Member>(member.ID);
			Assert.IsNull(deleted);
		}

		[TestMethod]
		[DeploymentItem("Database/Tests.sdf")]
		public void SelectWhereConstant()
		{
			Connection.Insert(new Member {
				Name = "Fabian"
			});

			var members = Connection.Select<Member>(x => x.Name == "Fabian");
			Assert.IsNotNull(members);
			Assert.AreEqual(1, members.Count());
			Assert.AreEqual("Fabian", members.First().Name);

			Connection.Delete(members.First());
		}

		[TestMethod]
		[DeploymentItem("Database/Tests.sdf")]
		public void SelectWhereProperty()
		{
			var member = new Member {
				Name = "Fabian"
			};

			Connection.Insert(member);

			var members = Connection.Select<Member>(x => x.Name == member.Name);
			Assert.IsNotNull(members);
			Assert.AreEqual(1, members.Count());
			Assert.AreEqual(member.Name, members.First().Name);

			Connection.Delete(members.First());
		}

		[TestMethod]
		[DeploymentItem("Database/Tests.sdf")]
		public void SelectAll()
		{
			Connection.Insert(new Member {
				Name = "Fabian"
			});

			Connection.Insert(new Member {
				Name = "Jurian"
			});

			var members = Connection.Select<Member>().ToList();
			Assert.IsNotNull(members);
			Assert.AreEqual(2, members.Count());
			Assert.AreEqual("Fabian", members[0].Name);
			Assert.AreEqual("Jurian", members[1].Name);

			foreach (var member in members)
				Connection.Delete(member);
		}

		[TestMethod]
		[DeploymentItem("Database/Tests.sdf")]
		public void SelectWhereTwoConstant()
		{
			//Connection.Insert(new Member {
			//	Name = "Fabian"
			//});

			var members = Connection.Select<Member>(x => (x.Name == "Fabian" || x.Name == "Jurian"));
			Assert.IsNotNull(members);
			Assert.AreEqual(1, members.Count());
			Assert.AreEqual("Fabian", members.First().Name);

			Connection.Delete(members.First());
		}
	}
}