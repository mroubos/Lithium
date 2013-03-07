using System.Data;

namespace Lithium.Linq
{
	public static class Extensions
	{
		public static Query<T> Query<T>(this IDbConnection connection)
		{
			return new Query<T>(connection);
		}
	}
}