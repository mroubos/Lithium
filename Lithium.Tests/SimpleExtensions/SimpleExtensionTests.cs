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
			Assert.Null(member);
		}
	}
}