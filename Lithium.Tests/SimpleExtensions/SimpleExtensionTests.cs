using System.Linq;
using Lithium.Tests.Models;
using NUnit.Framework;
using Lithium.SimpleExtensions;

namespace Lithium.Tests.SimpleExtensions
{
	[TestFixture]
	public class SimpleExtensions : TestBase
	{
		[Test]
		public void FullCycle()
		{
			Person person;

			const string initialName = "Fabian";
			const string updatedName = "Jurian";

			// record inserten
			int id = Connection.Insert<int>("Person", new { Name = initialName });

			// record ophalen en controleren of waarde niet null is en de naam overeenkomt
			person = Connection.Query<Person>("select * from Person where ID = @id", new { ID = id }).FirstOrDefault();
			Assert.IsNotNull(person);
			Assert.AreEqual(initialName, person.Name);

			// record updaten op basis van het id
			Connection.Update("Person", new { Name = updatedName }, new { ID = id });

			// record weer ophalen en controleren of waarde niet null is en de naam overeenkomt met de nieuwe naam
			person = Connection.Query<Person>("select * from Person where ID = @id", new { ID = id }).FirstOrDefault();
			Assert.IsNotNull(person);
			Assert.AreEqual(updatedName, person.Name);

			// record verwijderen op basis van het id
			Connection.Delete("Person", new { ID = id });

			// record proberen op te halen en bevestigen dat het record niet gevonden kan worden
			person = Connection.Query<Person>("select * from Person where ID = @id", new { ID = id }).FirstOrDefault();
			Assert.Null(person);
		}
	}
}