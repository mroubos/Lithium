using System;
using System.Collections.Generic;
using System.Data;

namespace Lithium
{
	public class MultiResult : IDisposable
	{
		private readonly QueryIdentity identity;

		private IDataReader reader;
		private IDbCommand command;

		private int gridIndex;
		private bool consumed;

		internal MultiResult(IDbCommand command, IDataReader reader, QueryIdentity identity)
		{
			this.command = command;
			this.reader = reader;
			this.identity = identity;
		}

		public IEnumerable<T> Read<T>()
		{
			if (reader == null)
				throw new ObjectDisposedException(GetType().Name);

			if (consumed)
				throw new InvalidOperationException("Each grid can only be iterated once");

			QueryIdentity typedIdentity = identity.ForGrid(typeof(T), gridIndex);
			QueryInfo info = SqlMapper.GetQueryInfo(typedIdentity);

			var deserializer = (Func<IDataReader, T>)info.Deserializer;
			if (info.Deserializer == null) {
				deserializer = SqlMapper.GetDeserializer<T>(reader);
				info.Deserializer = deserializer;
			}

			consumed = true;
			return ReadDeferred(gridIndex, deserializer, typedIdentity);
		}
		private IEnumerable<T> ReadDeferred<T>(int index, Func<IDataReader, T> deserializer, QueryIdentity typedIdentity)
		{
			bool clean = true;

			try {
				while (index == gridIndex && reader.Read()) {
					clean = false;
					T next = deserializer(reader);
					clean = true;
					yield return next;
				}
			}
			finally // finally so that First etc progresses things even when multiple rows
			{
				if (!clean) {
					SqlMapper.DeleteQueryInfo(typedIdentity);
				}

				if (index == gridIndex) {
					NextResult();
				}
			}
		}

		private void NextResult()
		{
			if (reader.NextResult()) {
				gridIndex++;
				consumed = false;
			}
			else {
				Dispose();
			}
		}
		public void Dispose()
		{
			if (reader != null) {
				reader.Dispose();
				reader = null;
			}

			if (command != null) {
				command.Dispose();
				command = null;
			}
		}
	}
}