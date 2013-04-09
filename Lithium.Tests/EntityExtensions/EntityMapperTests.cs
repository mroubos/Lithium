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
				Name = "Fabian",
				SomeEnum = SomeEnum.One
			};

			// insert new member
			Connection.Insert(member);
			Assert.IsTrue(member.ID > 0);

			// assert insert
			Member inserted = Connection.Select<Member>(member.ID);
			Assert.AreEqual(member.ID, inserted.ID);
			Assert.AreEqual(member.Name, inserted.Name);
			Assert.AreEqual(member.SomeEnum, inserted.SomeEnum);

			// update member
			inserted.Name = newName;
			inserted.SomeEnum = SomeEnum.Three;
			Connection.Update(inserted);

			// assert update
			Member updated = Connection.Select<Member>(inserted.ID);
			Assert.AreEqual(inserted.ID, updated.ID);
			Assert.AreEqual(inserted.Name, updated.Name);
			Assert.AreEqual(inserted.SomeEnum, updated.SomeEnum);

			Member copy = Connection.Select<Member>(member.ID);
			Assert.AreEqual(updated.ID, copy.ID);
			Assert.AreEqual(updated.Name, copy.Name);
			Assert.AreEqual(updated.SomeEnum, copy.SomeEnum);

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
		public void Ignore()
		{
			// arrange
			Connection.Entity<MemberIgnore>()
					  .Table("MemberIgnore")
					  .Identity(p => p.ID)
					  .Ignore(x => x.Username);

			var insertedMemberIgnore = new MemberIgnore {
				Name = "Fabian",
				Username = "fb"
			};

			Connection.Insert(insertedMemberIgnore);

			// act
			var selectedMemberIgnore = Connection.Select<MemberIgnore>(insertedMemberIgnore.ID);

			// assert
			Assert.AreEqual(insertedMemberIgnore.Name, selectedMemberIgnore.Name);
			Assert.IsNull(selectedMemberIgnore.Username);

			// clean
			Connection.Delete(insertedMemberIgnore);
		}
	}
}