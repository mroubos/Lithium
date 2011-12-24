using System.Data;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Data.SqlServerCe;
using NUnit.Framework;

namespace Lithium.Tests
{
	public class TestBase
	{
		protected IDbConnection Connection { get; private set; }

		[SetUp]
		public void SetUp()
		{
			Connection = new SqlCeConnection(@"data source=Database\Tests.sdf");
			// Connection = new SqlConnection(@"data source=localhost;initial catalog=Lithium;trusted_connection=yes");

			Connection.Open();
		}

		[TearDown]
		public void TearDown()
		{
			Connection.Dispose();
		}
	}
}