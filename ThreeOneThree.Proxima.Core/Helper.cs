using System;
using System.Linq.Expressions;

namespace ThreeOneThree.Proxima.Core
{
    public static class Helper
    {
        public static string GetPropertyName<TModel, TValue>(this Expression<Func<TModel, TValue>> propertySelector, char delimiter = '.', char endTrim = ')')
        {

            var asString = propertySelector.ToString(); // gives you: "o => o.Whatever"
            var firstDelim = asString.IndexOf(delimiter); // make sure there is a beginning property indicator; the "." in "o.Whatever" -- this may not be necessary?

            return firstDelim < 0
                       ? asString
                       : asString.Substring(firstDelim + 1).TrimEnd(endTrim);
        }
    }
}