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
					return ConnectionType.SQL;
				case "SqlCeConnection":
					return ConnectionType.SQLCE;
				default:
					return ConnectionType.Unknown;
			}
		}
	}
}