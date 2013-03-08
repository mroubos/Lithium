using Lithium.Linq.Evaluations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lithium.Linq
{
	public static class ExtensionEvaluator
	{
		public static IEvaluateExpression Process(string methodName) { return functionMapping[methodName]; }

		private static readonly Dictionary<string, IEvaluateExpression> functionMapping = new Dictionary<string, IEvaluateExpression>();

		static ExtensionEvaluator()
		{
			//var take = new ProcessTake();
			var where = new EvaluateWhere();
			//var single = new ProcessSingle();
			//var first = new ProcessFirst();
			var distinct = new EvaluateDistinct();
			//var holdLock = new ProcessWithHoldLock();
			//var noLock = new ProcessWithNoLock();
			//var rowAt = new ProcessRowAt();
			//var orderByAscending = new ProcessOrderBy(true);
			//var orderByDescending = new ProcessOrderBy(false);
			var notImplemented = new ProcessNotImplemented();

			functionMapping.Add("Aggregate", notImplemented); // aggregate
			functionMapping.Add("All", notImplemented); // can't see how this can be done
			functionMapping.Add("Any", notImplemented); // can't see how this can be done
			functionMapping.Add("AsQueryable", notImplemented); // can't see how this is necessary
			functionMapping.Add("Average", notImplemented); // aggregate
			functionMapping.Add("Cast", notImplemented); // can't see how this is necessary
			functionMapping.Add("Concat", notImplemented); // can't see how this is necessary
			functionMapping.Add("Contains", notImplemented); // can't see how this is necessary
			functionMapping.Add("Count", notImplemented); // aggregate
			functionMapping.Add("DefaultIfEmpty", notImplemented);// not even sure what this does
			functionMapping.Add("Distinct", distinct);
			functionMapping.Add("ElementAt", notImplemented); // can't see how this is necessary, unless do a TOP N and return the last item
			functionMapping.Add("ElementAtOrDefault", notImplemented); // can't see how this is necessary, unless do a TOP N and return the last item
			functionMapping.Add("Except", notImplemented);
			//functionMapping.Add("First", first);
			//functionMapping.Add("FirstOrDefault", first);
			functionMapping.Add("GroupBy", notImplemented); // aggregate
			functionMapping.Add("GroupJoin", notImplemented); // aggregate
			functionMapping.Add("Intersect", notImplemented); // can't see how this can be done
			functionMapping.Add("Join", notImplemented); // can't see how this can be done
			functionMapping.Add("Last", notImplemented); // rely on the user to do this manually
			functionMapping.Add("LastOrDefault", notImplemented); // rely on the user to do this manually
			functionMapping.Add("LongCount", notImplemented); // aggregate
			functionMapping.Add("Max", notImplemented); // aggregate
			functionMapping.Add("Min", notImplemented); // aggregate
			functionMapping.Add("OfType", notImplemented); // can't see how this is necessary
			//functionMapping.Add("OrderBy", orderByAscending);
			//functionMapping.Add("OrderByDescending", orderByDescending);
			functionMapping.Add("Reverse", notImplemented); // rely on the user to do this manually
			//functionMapping.Add("RowAt", rowAt);
			//functionMapping.Add("RowAtOrDefault", rowAt);
			functionMapping.Add("Select", notImplemented); // rely on the user to do this manually
			functionMapping.Add("SelectMany", notImplemented);
			functionMapping.Add("SequenceEqual", notImplemented); // not even sure what this does
			//functionMapping.Add("Single", single);
			//functionMapping.Add("SingleOrDefault", single);
			functionMapping.Add("Skip", notImplemented); // rely on the user to do this manually
			functionMapping.Add("SkipWhile", notImplemented); // rely on the user to do this manually
			functionMapping.Add("Sum", notImplemented); // aggregate
			//functionMapping.Add("Take", take); // not even sure what this does
			functionMapping.Add("TakeWhile", notImplemented); // not even sure what this does
			//functionMapping.Add("ThenBy", orderByAscending);
			//functionMapping.Add("ThenByDescending", orderByDescending);
			functionMapping.Add("Union", notImplemented); // rely on the user to do this manually
			functionMapping.Add("Where", where);
			//functionMapping.Add("WithNoLock", noLock);
			//functionMapping.Add("WithHoldLock", holdLock);
			functionMapping.Add("Zip", notImplemented); // rely on the user to do this manually
		}
	}
}