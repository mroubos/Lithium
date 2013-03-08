using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Lithium.Linq;
using System.Linq;
using System.Data.SqlClient;
using Lithium.Tests.Models;
using System.Configuration;
using System.Data;

namespace Lithium.Tests.Linq
{
	[TestClass]
	public class LinqTests
	{
		protected static IDbConnection Connection { get; private set; }

		[ClassInitialize]
		public static void SetUp(TestContext context)
		{
			//Connection = new SqlCeConnection(@"Data Source=Tests.sdf");
			Connection = new SqlConnection(ConfigurationManager.ConnectionStrings["Sql"].ConnectionString);

			Connection.Open();
		}

		[ClassCleanup]
		public static void TearDown()
		{
			Connection.Dispose();
		}

		[TestMethod]
		public void TestMethod1()
		{
			var member = new Member {
				Name = "Fabian"
			};

			var members = Connection.Query<Member>()
									.Where(x => x.Name.Equals(member.Name))
									.Distinct()
									.ToList();
		}
	}
}