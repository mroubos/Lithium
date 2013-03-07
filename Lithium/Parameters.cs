using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Lithium
{
	public class Parameters : IEnumerable<Parameter>
	{
		public List<Parameter> TList { get; private set; }

		public Parameters()
		{
			TList = new List<Parameter>();
		}

		public void Add<T>(string name, T value)
		{
			Add(name, value, ParameterDirection.Input);
		}

		public void Add<T>(string name, ParameterDirection direction)
		{
			Add(name, null, typeof(T), direction);
		}

		public void Add(string name, Type type, ParameterDirection direction)
		{
			Add(name, null, type, direction);
		}

		public void Add<T>(string name, T value, ParameterDirection direction)
		{
			Add(name, value, typeof(T), direction);
		}

		public void Add(string name, object value, Type type)
		{
			Add(name, value, type, ParameterDirection.Input);
		}

		public void Add(string name, object value, Type type, ParameterDirection direction)
		{
			TList.Add(new Parameter {
				Name = name,
				Value = value,
				Type = type,
				Direction = direction
			});
		}

		public T Get<T>(string name)
		{
			var parameter = TList.Single(x => x.Name.ToLower() == name.ToLower());
			if (parameter.Value == DBNull.Value) {
				if (default(T) != null)
					throw new ApplicationException("Attempting to cast a DBNull to a non nullable type!");

				return default(T);
			}

			return (T)parameter.AttachedParameter.Value;
		}

		public IEnumerator<Parameter> GetEnumerator()
		{
			return TList.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return TList.GetEnumerator();
		}
	}
}