using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace Lithium.Extensions
{
	public static class IDataRecordExtensions
	{
		public static List<string> GetColumnNames(this IDataRecord record)
		{
			var columns = new List<string>();
			for (var i = 0; i < record.FieldCount; i++)
				columns.Add(record.GetName(i));

			return columns;
		}
	}
}