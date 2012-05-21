using System.Data;

namespace Lithium.Extensions
{
	public static class IDbConnectionExtensions
	{
		public static ConnectionType GetConnectionType(this IDbConnection connection)
		{
			string type = connection.GetType().Name;

			switch (type) {
				case "SqlConnection":
					return ConnectionType.Sql;
				case "SqlCeConnection":
					return ConnectionType.SqlCe;
				default:
					return ConnectionType.Unknown;
			}
		}
	}
}