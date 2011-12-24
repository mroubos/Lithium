using Lithium.EntityExtensions;
using Lithium.Tests.Models;
using NUnit.Framework;

namespace Lithium.Tests.EntityExtensions
{
	[TestFixture]
	public class EntityMapperTests : TestBase
	{
		public EntityMapperTests()
		{
			// map person entity
			Connection.Entity<Person>()
				.Table("Person")
				.Identity(p => p.ID);		
		}

		[Test]
		public void FullCycle()
		{
			const string newName = "Jurian";

			var person = new Person {
				Name = "Fabian"
			};

			// insert new person
			Connection.Insert(person);
			Assert.IsTrue(person.ID > 0);

			// assert insert
			Person inserted = Connection.Select<Person>(person.ID);
			Assert.AreEqual(person.ID, inserted.ID);
			Assert.AreEqual(person.Name, inserted.Name);

			// update person
			inserted.Name = newName;
			Connection.Update(inserted);

			// assert update
			Person updated = Connection.Select<Person>(inserted.ID);
			Assert.AreEqual(inserted.ID, updated.ID);
			Assert.AreEqual(inserted.Name, updated.Name);

			// delete person
			Connection.Delete(updated);

			// assert delete
			Person deleted = Connection.Select<Person>(person.ID);
			Assert.IsNull(deleted);
		}
	}
}