using System;

namespace Lithium
{
	internal class QueryIdentity : IEquatable<QueryIdentity>
	{
		private readonly int hashCode;

		private readonly string connectionString;
		private readonly string query;
		private readonly Type returnType;
		private readonly Type parametersType;
		private readonly Type additionalParametersType;
		private readonly int gridIndex;

		public QueryIdentity ForGrid(Type primaryType, int gridIndex)
		{
			return new QueryIdentity(query, connectionString, primaryType, parametersType, null, gridIndex);
		}

		public QueryIdentity(string connectionString, string query, Type returnType, Type parametersType = null, Type additionalParametersType = null, int gridIndex = 0)
		{
			this.connectionString = connectionString;
			this.query = query;
			this.returnType = returnType;
			this.parametersType = parametersType;
			this.additionalParametersType = additionalParametersType;
			this.gridIndex = gridIndex;

			unchecked {
				hashCode = 17;
				hashCode = hashCode * 23 + gridIndex.GetHashCode();
				hashCode = hashCode * 23 + query.GetHashCode();
				hashCode = hashCode * 23 + (connectionString == null ? 0 : connectionString.GetHashCode());
				hashCode = hashCode * 23 + (returnType == null ? 0 : returnType.GetHashCode());
				hashCode = hashCode * 23 + (parametersType == null ? 0 : parametersType.GetHashCode());
				hashCode = hashCode * 23 + (additionalParametersType == null ? 0 : additionalParametersType.GetHashCode());
			}
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as QueryIdentity);
		}

		public override int GetHashCode()
		{
			return hashCode;
		}

		public bool Equals(QueryIdentity other)
		{
			return
				other != null &&
				gridIndex == other.gridIndex &&
				query == other.query &&
				connectionString == other.connectionString &&
				returnType == other.returnType &&
				parametersType == other.parametersType &&
				additionalParametersType == other.additionalParametersType;
		}
	}
}