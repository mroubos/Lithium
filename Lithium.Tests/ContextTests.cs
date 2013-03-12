using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Lithium.Extensions;
using Lithium.Tests.Models;
using Lithium.SimpleExtensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Lithium.Tests
{
	[TestClass]
	public class ContextTests
	{
		static Context context;

		[ClassInitialize]
		public static void SetUp(TestContext textContext)
		{
			//context = new Context();

			//context.Entity<Member>()
			//	.TableName("Member")
			//	.PrimaryKey(x => x.ID);
		}

		[TestMethod]
		public void Test1()
		{
			Member x = Proxy.Create<Member>();
			x.ID = 1;
		}
	}
}